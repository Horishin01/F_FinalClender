using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pit2Hi022052.Data;
using Pit2Hi022052.Models;
using Microsoft.EntityFrameworkCore;

namespace Pit2Hi022052.Services
{
    public class CloudCalDavService : ICloudCalDavService
    {
        private readonly ILogger<CloudCalDavService> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IcalParserService _icalParser;

        public CloudCalDavService(
            ILogger<CloudCalDavService> logger,
            ApplicationDbContext db,
            IHttpContextAccessor httpContext,
            IcalParserService icalParser)
        {
            _logger = logger;
            _db = db;
            _httpContext = httpContext;
            _icalParser = icalParser;
        }

        public async Task<List<Event>> GetAllEventsAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                _logger.LogWarning("❌ ユーザー情報が取得できませんでした。");
                return new List<Event>();
            }

            string username = user.Username; // ← @icloud.com付きでOK

            var authBytes = Encoding.UTF8.GetBytes($"{username}:{user.Password}");
            var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = authHeader;

            try
            {
                // Step 1: current-user-principal
                _logger.LogInformation("🌐 Step1: current-user-principal を取得中...");
                var propfindXml = new StringContent(
@"<?xml version='1.0' encoding='UTF-8' ?>
<d:propfind xmlns:d='DAV:'>
  <d:prop>
    <d:current-user-principal />
  </d:prop>
</d:propfind>", Encoding.UTF8, "application/xml");

                var propfindRequest = new HttpRequestMessage(new HttpMethod("PROPFIND"), "https://caldav.icloud.com/");
                propfindRequest.Headers.Add("Depth", "0");
                propfindRequest.Content = propfindXml;

                var principalResponse = await httpClient.SendAsync(propfindRequest);
                var principalContent = await principalResponse.Content.ReadAsStringAsync();

                if (!principalResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ Step1失敗: {principalResponse.StatusCode} {principalResponse.ReasonPhrase}");
                    _logger.LogDebug($"レスポンス内容: {principalContent}");
                    return new List<Event>();
                }

                var xml = XDocument.Parse(principalContent);
                var nsDav = XNamespace.Get("DAV:");
                var principalHref = xml.Descendants(nsDav + "current-user-principal")
                                       .Descendants(nsDav + "href")
                                       .FirstOrDefault()?.Value;

                if (string.IsNullOrEmpty(principalHref))
                {
                    _logger.LogError("❌ current-user-principal の href が取得できませんでした。");
                    _logger.LogDebug(principalContent);
                    return new List<Event>();
                }

                _logger.LogInformation($"✅ principal: {principalHref}");

                // Step 2: calendar-home-set
                _logger.LogInformation("📁 Step2: calendar-home-set を取得中...");
                var homeRequestXml = new StringContent(
@"<?xml version='1.0' encoding='UTF-8' ?>
<d:propfind xmlns:d='DAV:' xmlns:cal='urn:ietf:params:xml:ns:caldav'>
  <d:prop>
    <cal:calendar-home-set />
  </d:prop>
</d:propfind>", Encoding.UTF8, "application/xml");

                var homeRequest = new HttpRequestMessage(new HttpMethod("PROPFIND"), $"https://caldav.icloud.com{principalHref}");
                homeRequest.Headers.Add("Depth", "0");
                homeRequest.Content = homeRequestXml;

                var homeResponse = await httpClient.SendAsync(homeRequest);
                var homeContent = await homeResponse.Content.ReadAsStringAsync();

                if (!homeResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ Step2失敗: {homeResponse.StatusCode} {homeResponse.ReasonPhrase}");
                    _logger.LogDebug($"レスポンス内容: {homeContent}");
                    return new List<Event>();
                }

                var homeXml = XDocument.Parse(homeContent);
                var nsCal = XNamespace.Get("urn:ietf:params:xml:ns:caldav");
                var calendarHome = homeXml.Descendants(nsCal + "calendar-home-set")
                                          .Descendants(nsDav + "href")
                                          .FirstOrDefault()?.Value;

                if (string.IsNullOrEmpty(calendarHome))
                {
                    _logger.LogError("❌ calendar-home-set href が取得できませんでした。");
                    _logger.LogDebug(homeContent);
                    return new List<Event>();
                }

                // 修正箇所：calendarHome が絶対URLか相対URLかを判別
                string calendarRootUrl = calendarHome.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? calendarHome
                    : $"https://caldav.icloud.com{calendarHome}";

                _logger.LogInformation($"calendar-home: {calendarRootUrl}");

                // Step 3: カレンダー一覧取得
                _logger.LogInformation(" Step3: カレンダー一覧取得中...");
                var calendarListXml = new StringContent(
@"<?xml version='1.0' encoding='UTF-8' ?>
<d:propfind xmlns:d='DAV:' xmlns:cal='urn:ietf:params:xml:ns:caldav'>
  <d:prop>
    <d:displayname />
  </d:prop>
</d:propfind>", Encoding.UTF8, "application/xml");

                var calendarListRequest = new HttpRequestMessage(new HttpMethod("PROPFIND"), calendarRootUrl);
                calendarListRequest.Headers.Add("Depth", "1");
                calendarListRequest.Content = calendarListXml;

                var calendarListResponse = await httpClient.SendAsync(calendarListRequest);
                var calendarListContent = await calendarListResponse.Content.ReadAsStringAsync();

                if (!calendarListResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"カレンダー一覧取得失敗: {calendarListResponse.StatusCode}");
                    _logger.LogDebug(calendarListContent);
                    return new List<Event>();
                }

                var calendarListDoc = XDocument.Parse(calendarListContent);
                var calendarUrls = calendarListDoc.Descendants(nsDav + "response")
                    .Select(x => x.Element(nsDav + "href")?.Value)
                    .Where(x => !string.IsNullOrEmpty(x) && x.EndsWith("/"))
                    .Select(x => $"https://caldav.icloud.com{x}")
                    .ToList();

                _logger.LogInformation($" カレンダー件数: {calendarUrls.Count}");

                var allEvents = new List<Event>();

                foreach (var calendarUrl in calendarUrls)
                {
                    var reportXml = new StringContent(@"<?xml version='1.0' encoding='utf-8' ?>
<calendar-query xmlns='urn:ietf:params:xml:ns:caldav'
                xmlns:d='DAV:' xmlns:cs='http://calendarserver.org/ns/'>
  <d:prop>
    <d:getetag/>
    <calendar-data/>
  </d:prop>
  <filter>
    <comp-filter name='VCALENDAR'>
      <comp-filter name='VEVENT' />
    </comp-filter>
  </filter>
</calendar-query>", Encoding.UTF8, "application/xml");

                    var reportRequest = new HttpRequestMessage(new HttpMethod("REPORT"), calendarUrl);
                    reportRequest.Headers.Add("Depth", "1");
                    reportRequest.Content = reportXml;

                    _logger.LogInformation($"イベント取得中: {calendarUrl}");
                    var reportResponse = await httpClient.SendAsync(reportRequest);
                    var reportContent = await reportResponse.Content.ReadAsStringAsync();

                    if (!reportResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"CalDAV REPORT失敗: {reportResponse.StatusCode} @ {calendarUrl}");
                        _logger.LogDebug(reportContent);
                        continue;
                    }

                    var reportXmlDoc = XDocument.Parse(reportContent);
                    var calendarDataList = reportXmlDoc.Descendants(nsCal + "calendar-data").Select(x => x.Value).ToList();

                    foreach (var ics in calendarDataList)
                        allEvents.AddRange(_icalParser.ParseIcsToEventList(ics));
                }

                _logger.LogInformation($" iCloudからの取得イベント数: {allEvents.Count}");
                return allEvents;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ CalDAV通信エラー: {ex.Message}");
                return new List<Event>();
            }
        }

        private async Task<ICloudSetting> GetCurrentUserAsync()
        {
            var userId = _httpContext.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return null;
            return await _db.ICloudSettings.FirstOrDefaultAsync(x => x.UserId == userId);
        }
    }
}
