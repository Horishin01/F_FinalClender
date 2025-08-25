using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;

namespace Pit2Hi022052.Services
{
    public interface ICloudCalDavService
    {
        Task<List<Event>> GetAllEventsAsync(string userId);
    }

    public class CloudCalDavService : ICloudCalDavService
    {
        private readonly ILogger<CloudCalDavService> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IcalParserService _parser;

        public CloudCalDavService(
            ILogger<CloudCalDavService> logger,
            ApplicationDbContext db,
            IHttpContextAccessor httpContext,
            IcalParserService parser)
        {
            _logger = logger;
            _db = db;
            _httpContext = httpContext;
            _parser = parser;
        }

        public async Task<List<Event>> GetAllEventsAsync(string userId)
        {
            var events = new List<Event>();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UserIdが空です。");
                return events;
            }

            // iCloud資格情報
            var icloud = _db.ICloudSettings.FirstOrDefault(x => x.UserId == userId);
            if (icloud == null)
            {
                _logger.LogWarning("iCloud設定が見つかりません。");
                return events;
            }

            var handler = new HttpClientHandler
            {
                PreAuthenticate = true,
                Credentials = new System.Net.NetworkCredential(icloud.Username, icloud.Password)
            };

            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://caldav.icloud.com/")
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{icloud.Username}:{icloud.Password}"))
            );

            // current-user-principal
            var propfindHome = new HttpRequestMessage(new HttpMethod("PROPFIND"), "/.well-known/caldav");
            propfindHome.Headers.Add("Depth", "0");
            propfindHome.Content = new StringContent(@"<?xml version='1.0' encoding='utf-8' ?>
<propfind xmlns='DAV:'>
  <prop>
    <current-user-principal />
  </prop>
</propfind>", Encoding.UTF8, "application/xml");

            HttpResponseMessage homeResponse;
            try
            {
                homeResponse = await client.SendAsync(propfindHome);
                if (!homeResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("calendar-home-set取得失敗: {Status}", homeResponse.StatusCode);
                    return events;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "calendar-home-set取得通信エラー");
                return events;
            }

            var homeXml = XDocument.Parse(await homeResponse.Content.ReadAsStringAsync());
            XNamespace dav = "DAV:";
            var principalHref = homeXml
                .Descendants(dav + "current-user-principal")
                .Descendants(dav + "href")
                .FirstOrDefault()?.Value;

            _logger.LogInformation("取得した principalHref: {Href}", principalHref);
            if (string.IsNullOrEmpty(principalHref))
            {
                _logger.LogWarning("current-user-principal の href が取得できませんでした。");
                return events;
            }

            // principal → calendars
            var calendarHomeUrl = principalHref.Replace("principal/", "calendars/");
            _logger.LogInformation("使用する calendarHomeUrl: {Href}", calendarHomeUrl);

            // カレンダー一覧
            var calendarQuery = new HttpRequestMessage(new HttpMethod("PROPFIND"), calendarHomeUrl);
            calendarQuery.Headers.Add("Depth", "1");
            calendarQuery.Content = new StringContent(@"<?xml version='1.0' encoding='utf-8'?>
<propfind xmlns='DAV:' xmlns:cs='http://calendarserver.org/ns/' xmlns:cal='urn:ietf:params:xml:ns:caldav'>
  <prop>
    <displayname/>
    <resourcetype/>
    <cs:getctag/>
    <cal:calendar-description/>
    <cal:supported-calendar-component-set/>
  </prop>
</propfind>", Encoding.UTF8, "application/xml");

            HttpResponseMessage calListResponse;
            try
            {
                calListResponse = await client.SendAsync(calendarQuery);
                if (!calListResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("カレンダーリスト取得失敗: {Status}", calListResponse.StatusCode);
                    return events;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "カレンダーリスト取得中の通信エラー");
                return events;
            }

            var calListXml = XDocument.Parse(await calListResponse.Content.ReadAsStringAsync());
            XNamespace calNs = "urn:ietf:params:xml:ns:caldav";

            var calendars = calListXml.Descendants(dav + "response")
                .Where(r => r.Descendants(dav + "resourcetype").Any(rt => rt.Elements(calNs + "calendar").Any()))
                .Select(r => new
                {
                    Href = r.Element(dav + "href")?.Value,
                    Name = r.Descendants(dav + "displayname").FirstOrDefault()?.Value
                })
                .Where(x => !string.IsNullOrEmpty(x.Href) && x.Name != null)
                .ToList();

            if (!calendars.Any())
            {
                _logger.LogWarning("有効なカレンダーが見つかりませんでした。");
                return events;
            }

            // 各カレンダーから期間内のVEVENTを取得
            foreach (var cal in calendars)
            {
                _logger.LogInformation("カレンダー取得対象: {Name} ({Href})", cal.Name, cal.Href);

                var now = DateTime.UtcNow;
                var past = now.AddMonths(-1);
                var future = now.AddMonths(5);

                var report = new HttpRequestMessage(new HttpMethod("REPORT"), cal.Href);
                report.Headers.Add("Depth", "1");
                report.Content = new StringContent($@"<?xml version='1.0' encoding='utf-8'?>
<c:calendar-query xmlns:d='DAV:' xmlns:c='urn:ietf:params:xml:ns:caldav'>
  <d:prop>
    <d:getetag/>
    <c:calendar-data/>
  </d:prop>
  <c:filter>
    <c:comp-filter name='VCALENDAR'>
      <c:comp-filter name='VEVENT'>
        <c:time-range start='{past:yyyyMMdd}T000000Z' end='{future:yyyyMMdd}T235959Z'/>
      </c:comp-filter>
    </c:comp-filter>
  </c:filter>
</c:calendar-query>", Encoding.UTF8, "application/xml");

                try
                {
                    var resp = await client.SendAsync(report);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("CalDAV応答エラー: {Status}", resp.StatusCode);
                        continue;
                    }

                    var xml = XDocument.Parse(await resp.Content.ReadAsStringAsync());
                    var icsList = xml.Descendants().Where(x => x.Name.LocalName == "calendar-data").Select(x => x.Value);

                    foreach (var ics in icsList)
                    {
                        var parsed = _parser.ParseIcsToEventList(ics);
                        events.AddRange(parsed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "カレンダー {Name} のイベント取得で例外が発生", cal.Name);
                }
            }

            // メトリクス
            _logger.LogInformation("iCloudパース結果: {Total} 件（UID空 {Empty} 件）",
                events.Count, events.Count(e => string.IsNullOrWhiteSpace(e.UID)));

            // 既存UID（空は除外）
            var existingUIDs = _db.Events
                .Where(e => e.UserId == userId && !string.IsNullOrEmpty(e.UID))
                .Select(e => e.UID!)
                .ToHashSet(StringComparer.Ordinal);

            var newEvents = new List<Event>();

            // 保存対象（UID必須）
            foreach (var ev in events.Where(x => !string.IsNullOrWhiteSpace(x.UID)))
            {
                if (!existingUIDs.Contains(ev.UID))
                {
                    if (string.IsNullOrEmpty(ev.Id))
                        ev.Id = Guid.NewGuid().ToString("N");

                    ev.UserId = userId;
                    newEvents.Add(ev);
                }
            }

            if (newEvents.Any())
            {
                _db.Events.AddRange(newEvents);
                await _db.SaveChangesAsync();
                _logger.LogInformation("新規CalDAVイベントを {Count} 件保存しました。", newEvents.Count);
            }
            else
            {
                _logger.LogInformation("新規保存対象のCalDAVイベントはありませんでした。");
            }

            return events;
        }
    }
}
