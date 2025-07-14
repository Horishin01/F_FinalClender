using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using Pit2Hi022052.Models;

public class ICloudCalDavService
{
    private readonly ILogger<ICloudCalDavService> _logger;
    private readonly string _username;
    private readonly string _password;
    private readonly HttpClient _client;

    public ICloudCalDavService(
        ILogger<ICloudCalDavService> logger,
        string username,
        string password)
    {
        _logger = logger;
        _username = username;
        _password = password;

        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(_username, _password)
        };

        _client = new HttpClient(handler);
    }

    /// ユーザー固有のHome URLを取得
    public async Task<string> GetHomeSetUrlAsync()
    {
        _logger.LogInformation("ユーザーPrincipal URL取得開始");

        var request1 = new HttpRequestMessage(new HttpMethod("PROPFIND"), "https://caldav.icloud.com/");
        request1.Headers.Add("Depth", "0");
        request1.Content = new StringContent(
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<d:propfind xmlns:d=""DAV:"">
  <d:prop>
    <d:current-user-principal/>
  </d:prop>
</d:propfind>",
System.Text.Encoding.UTF8, "application/xml");

        var response1 = await _client.SendAsync(request1);
        response1.EnsureSuccessStatusCode();
        var xml1 = await response1.Content.ReadAsStringAsync();
        _logger.LogInformation("ユーザーPrincipal取得XML:\n{Xml}", xml1);

        var doc1 = XDocument.Parse(xml1);
        XNamespace dav = "DAV:";

        var principalHref = doc1
            .Descendants(dav + "current-user-principal")
            .Descendants(dav + "href")
            .FirstOrDefault()?.Value;

        if (string.IsNullOrEmpty(principalHref))
            throw new Exception("ユーザーPrincipal URLが取得できませんでした");

        _logger.LogInformation("Principal URL: {Href}", principalHref);

        // iCloud用にHomeSetURLを固定生成
        var userIdPart = principalHref.Trim('/').Split('/').FirstOrDefault();
        if (string.IsNullOrEmpty(userIdPart))
            throw new Exception("ユーザーIDがPrincipal URLから取得できませんでした");

        var homeUrl = $"https://caldav.icloud.com/{userIdPart}/calendars/";
        _logger.LogInformation("✅ カレンダーHome URL(固定): {Url}", homeUrl);

        return homeUrl;
    }

    /// 全カレンダーURL取得
    public async Task<List<string>> GetAllCalendarUrlsAsync(string homeUrl)
    {
        _logger.LogInformation("カレンダーURL取得開始: {HomeUrl}", homeUrl);

        var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), homeUrl);
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<propfind xmlns=""DAV:"">
  <prop>
    <resourcetype/>
    <displayname/>
  </prop>
</propfind>",
System.Text.Encoding.UTF8, "application/xml");

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("PROPFIND取得成功");

        var doc = XDocument.Parse(xml);
        XNamespace dav = "DAV:";

        var hrefs = doc.Descendants(dav + "response")
            .Where(x =>
            {
                var resourcetype = x.Descendants(dav + "resourcetype").FirstOrDefault();
                if (resourcetype == null)
                    return false;

                var isCalendar = resourcetype.Elements().Any(e => e.Name.LocalName == "calendar");
                if (!isCalendar)
                    return false;

                var href = x.Element(dav + "href")?.Value;
                if (href == null)
                    return false;

                // システム用フォルダは除外
                if (href.EndsWith("/inbox/") ||
                    href.EndsWith("/outbox/") ||
                    href.EndsWith("/notification/"))
                {
                    return false;
                }

                return true;
            })
            .Select(x => x.Element(dav + "href")?.Value?.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        _logger.LogInformation("有効カレンダーURL数: {Count}", hrefs.Count);
        foreach (var url in hrefs)
        {
            _logger.LogInformation("✅ 対象カレンダーURL: {Url}", url);
        }

        return hrefs;
    }

    /// カレンダーURLからICS取得
    public async Task<List<string>> GetAllCalendarIcsAsync(List<string> calendarUrls)
    {
        _logger.LogInformation("各カレンダーのICS取得開始");

        var allIcs = new List<string>();

        foreach (var href in calendarUrls)
        {
            var fullUrl = "https://caldav.icloud.com" + href;

            _logger.LogInformation("取得対象URL: {FullUrl}", fullUrl);

            var reportRequest = new HttpRequestMessage(new HttpMethod("REPORT"), fullUrl)
            {
                Content = new StringContent(
@"<?xml version=""1.0"" encoding=""utf-8""?>
<c:calendar-query xmlns:c=""urn:ietf:params:xml:ns:caldav"" xmlns:d=""DAV:"">
  <d:prop>
    <d:getetag/>
    <c:calendar-data/>
  </d:prop>
  <c:filter>
    <c:comp-filter name=""VCALENDAR""/>
  </c:filter>
</c:calendar-query>",
System.Text.Encoding.UTF8, "application/xml")
            };
            reportRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml");
            reportRequest.Headers.Add("Depth", "1");

            try
            {
                var reportResponse = await _client.SendAsync(reportRequest);
                if (reportResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("⚠️ アクセス拒否(403): {Url}", fullUrl);
                    continue;
                }
                reportResponse.EnsureSuccessStatusCode();
                var reportXml = await reportResponse.Content.ReadAsStringAsync();

                _logger.LogInformation("REPORT取得成功: {Url}", fullUrl);

                var doc = XDocument.Parse(reportXml);
                XNamespace c = "urn:ietf:params:xml:ns:caldav";

                var icsList = doc.Descendants(c + "calendar-data")
                    .Select(x => x.Value.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();

                _logger.LogInformation("ICS件数: {Count}", icsList.Count);

                allIcs.AddRange(icsList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ICS取得中にエラーが発生しました: {Url}", fullUrl);
            }
        }

        _logger.LogInformation("ICS取得完了 合計件数: {TotalCount}", allIcs.Count);

        return allIcs;
    }

    /// 全イベント取得
    public async Task<List<Event>> GetAllEventsAsync()
    {
        var homeUrl = await GetHomeSetUrlAsync();

        var urls = await GetAllCalendarUrlsAsync(homeUrl);

        if (urls.Count == 0)
        {
            _logger.LogWarning("カレンダーURLが見つかりませんでした。");
            return new List<Event>();
        }

        var allIcs = await GetAllCalendarIcsAsync(urls);

        if (allIcs.Count == 0)
        {
            _logger.LogWarning("ICSデータが0件です。");
            return new List<Event>();
        }

        var combinedIcs = string.Join("\r\n", allIcs);

        _logger.LogInformation("ICS結合完了。テキスト長さ: {Length}", combinedIcs.Length);

        var parser = new IcalParserService();
        var events = parser.ParseIcsToEventList(combinedIcs);

        _logger.LogInformation("イベント変換完了 件数: {Count}", events.Count);

        return events;
    }
}
