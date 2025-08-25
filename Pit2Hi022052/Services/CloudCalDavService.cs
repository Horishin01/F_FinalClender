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

        public async Task<List<Event>> GetAllEventsAsync()
        {
            var events = new List<Event>();
            var user = _httpContext.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(user))
            {
                _logger.LogWarning("ユーザー情報が取得できません。");
                return events;
            }

            var icloudSetting = _db.ICloudSettings.FirstOrDefault(x => x.User.UserName == user);
            if (icloudSetting == null)
            {
                _logger.LogWarning("iCloud設定が見つかりません。");
                return events;
            }

            var username = icloudSetting.Username;
            var password = icloudSetting.Password;

            var handler = new HttpClientHandler
            {
                PreAuthenticate = true,
                Credentials = new System.Net.NetworkCredential(username, password)
            };

            using var client = new HttpClient(handler);
            client.BaseAddress = new Uri("https://caldav.icloud.com/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));

            // Step1: principalHrefの取得
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
                    _logger.LogWarning("calendar-home-set取得失敗: {StatusCode}", homeResponse.StatusCode);
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
            var principalHref = homeXml.Descendants(dav + "current-user-principal").Descendants(dav + "href").FirstOrDefault()?.Value;
            _logger.LogInformation("取得した principalHref: {Href}", principalHref);

            if (string.IsNullOrEmpty(principalHref))
            {
                _logger.LogWarning("current-user-principal の href が取得できませんでした。");
                return events;
            }

            // iCloudでは "principal" → "calendars" に変換
            var calendarHomeUrl = principalHref.Replace("principal/", "calendars/");
            _logger.LogInformation("使用する calendarHomeUrl: {Href}", calendarHomeUrl);

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
                    _logger.LogWarning("カレンダーリスト取得失敗: {StatusCode}", calListResponse.StatusCode);
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

            var validCalendars = calListXml.Descendants(dav + "response")
                .Where(resp => resp.Descendants(dav + "resourcetype").Any(rt => rt.Elements(calNs + "calendar").Any()))
                .Select(resp => new
                {
                    Href = resp.Element(dav + "href")?.Value,
                    Name = resp.Descendants(dav + "displayname").FirstOrDefault()?.Value
                })
                .Where(x => !string.IsNullOrEmpty(x.Href) && x.Name != null)
                .ToList();

            if (!validCalendars.Any())
            {
                _logger.LogWarning("有効なカレンダーが見つかりませんでした。");
                return events;
            }

            foreach (var cal in validCalendars)
            {
                _logger.LogInformation("カレンダー取得対象: {Name} ({Href})", cal.Name, cal.Href);

                var now = DateTime.UtcNow;
                var past = now.AddMonths(-1);
                var future = now.AddMonths(5);

                var eventQuery = new HttpRequestMessage(new HttpMethod("REPORT"), cal.Href);
                eventQuery.Headers.Add("Depth", "1");
                eventQuery.Content = new StringContent($@"<?xml version='1.0' encoding='utf-8'?>
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
                    var response = await client.SendAsync(eventQuery);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("CalDAV応答エラー（イベント取得）: {StatusCode}", response.StatusCode);
                        continue;
                    }

                    var responseXml = XDocument.Parse(await response.Content.ReadAsStringAsync());
                    var icalData = responseXml.Descendants().Where(x => x.Name.LocalName == "calendar-data").Select(x => x.Value);

                    foreach (var ical in icalData)
                    {
                        var parsedEvents = _parser.ParseIcsToEventList(ical);
                        events.AddRange(parsedEvents);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "カレンダー {Name} のイベント取得で例外が発生しました", cal.Name);
                }
            }

            return events;
        }
    }
}
