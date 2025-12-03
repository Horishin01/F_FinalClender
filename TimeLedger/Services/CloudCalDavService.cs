using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Services
{
    // 同期APIの戻り値
    public record CalDavSyncResult(int Scanned, int Saved);
    public record CalDavUpsertResult(bool Success, string? Uid, bool Created, string? ResourceUrl, HttpStatusCode? StatusCode = null, string? ResponseBody = null);

    public interface ICloudCalDavService
    {
        // 既存：CalDAVから取得（内部で新規だけDB保存する実装のままでOK）
        Task<List<Event>> GetAllEventsAsync(string userId);

        // 追加：手動同期用の薄いラッパ
        Task<CalDavSyncResult> SyncAsync(string userId, CancellationToken ct = default);

        // 追加：iCloudへイベントを書き戻す
        Task<CalDavUpsertResult> UpsertEventAsync(string userId, Event ev, CancellationToken ct = default);
        Task<bool> DeleteEventAsync(string userId, Event ev, CancellationToken ct = default);
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

        private sealed record CalDavCalendar(string Href, string Name, bool CanWrite);

        private sealed class CalDavContext : IDisposable
        {
            public CalDavContext(HttpClient client, List<CalDavCalendar> calendars)
            {
                Client = client;
                Calendars = calendars;
            }

            public HttpClient Client { get; }
            public List<CalDavCalendar> Calendars { get; }

            public void Dispose() => Client.Dispose();
        }

        // ★ 追加：同期（GetAllEventsAsync を呼び、その前後の件数差分を返すだけ）
        public async Task<CalDavSyncResult> SyncAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new CalDavSyncResult(0, 0);

            var eventsSet = _db.Events;
            if (eventsSet == null)
            {
                _logger.LogWarning("Events DbSet が初期化されていません。");
                return new CalDavSyncResult(0, 0);
            }

            var before = await eventsSet.Where(e => e.UserId == userId).CountAsync(ct);

            var fetched = await GetAllEventsAsync(userId); // 内部で新規保存する実装のままでOK

            var after = await eventsSet.Where(e => e.UserId == userId).CountAsync(ct);
            var saved = Math.Max(0, after - before);

            _logger.LogInformation("手動同期完了: scanned={Scanned}, saved={Saved}", fetched.Count, saved);
            return new CalDavSyncResult(fetched.Count, saved);
        }

        public async Task<List<Event>> GetAllEventsAsync(string userId)
        {
            var events = new List<Event>();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UserIdが空です。");
                return events;
            }

            var eventsSet = _db.Events;
            if (eventsSet == null)
            {
                _logger.LogWarning("Events DbSet が初期化されていません。");
                return events;
            }

            var (context, ctxError) = await CreateContextAsync(userId, CancellationToken.None);
            if (context == null)
            {
                _logger.LogWarning("iCloud取得用コンテキストの作成に失敗しました。{Error}", ctxError);
                return events;
            }
            using var ctx = context;

            // 各カレンダーから期間内のVEVENTを取得
            foreach (var cal in ctx.Calendars)
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
                    var resp = await ctx.Client.SendAsync(report);
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
                        foreach (var ev in parsed)
                        {
                            ev.Source = EventSource.ICloud;
                        }
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
            var existingUIDs = eventsSet
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
                eventsSet.AddRange(newEvents);
                await _db.SaveChangesAsync();
                _logger.LogInformation("新規CalDAVイベントを {Count} 件保存しました。", newEvents.Count);
            }
            else
            {
                _logger.LogInformation("新規保存対象のCalDAVイベントはありませんでした。");
            }

            return events;
        }

        public async Task<CalDavUpsertResult> UpsertEventAsync(string userId, Event ev, CancellationToken ct = default)
        {
            if (ev == null) return new CalDavUpsertResult(false, null, false, null, null, "Event payload is null.");
            var (context, ctxError) = await CreateContextAsync(userId, ct);
            if (context == null) return new CalDavUpsertResult(false, ev.UID, false, null, null, ctxError ?? "CalDAV 接続に失敗しました。");

            using var ctx = context;
            var uid = string.IsNullOrWhiteSpace(ev.UID) ? Guid.NewGuid().ToString("N") : ev.UID.Trim();
            ev.UID = uid;

            var existingHref = await FindEventHrefAsync(ctx, uid, ct);
            string targetPath;
            CalDavCalendar targetCalendar;
            if (existingHref.HasValue)
            {
                targetCalendar = existingHref.Value.CalendarHref;
                targetPath = existingHref.Value.EventHref;
            }
            else
            {
                targetCalendar = ctx.Calendars.FirstOrDefault(c => c.CanWrite) ?? ctx.Calendars.First();
                targetPath = CombineHref(targetCalendar.Href, $"{uid}.ics");
            }

            if (!targetCalendar.CanWrite)
            {
                _logger.LogWarning("書き込み不可のカレンダーを検出しました。Name={Name}, Href={Href}", targetCalendar.Name, targetCalendar.Href);
                return new CalDavUpsertResult(false, uid, false, targetPath, HttpStatusCode.Forbidden, "このカレンダーは書き込み権限がありません。");
            }

            var (ics, normalizedUid) = BuildIcsPayload(ev, existingHref.HasValue);
            ev.UID = normalizedUid;

            var absoluteTarget = ctx.Client.BaseAddress != null
                ? new Uri(ctx.Client.BaseAddress, targetPath)
                : new Uri(targetPath, UriKind.RelativeOrAbsolute);

            var request = new HttpRequestMessage(HttpMethod.Put, absoluteTarget)
            {
                Content = new StringContent(ics, Encoding.UTF8)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/calendar")
            {
                CharSet = "utf-8"
            };
            if (existingHref == null)
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", "*"); // 新規作成を期待
            }
            else if (!string.IsNullOrEmpty(existingHref.Value.Etag))
            {
                request.Headers.TryAddWithoutValidation("If-Match", existingHref.Value.Etag);
            }

            try
            {
                var resp = await ctx.Client.SendAsync(request, ct);
                if (!resp.IsSuccessStatusCode && existingHref.HasValue &&
                    (resp.StatusCode == HttpStatusCode.PreconditionFailed || resp.StatusCode == HttpStatusCode.Conflict))
                {
                    // ETag 不一致などで弾かれた場合は一度だけ条件なしで再試行
                    var fallback = new HttpRequestMessage(HttpMethod.Put, absoluteTarget)
                    {
                        Content = new StringContent(ics, Encoding.UTF8)
                    };
                    fallback.Content.Headers.ContentType = new MediaTypeHeaderValue("text/calendar")
                    {
                        CharSet = "utf-8"
                    };
                    resp = await ctx.Client.SendAsync(fallback, ct);
                }

                var success = resp.IsSuccessStatusCode;
                string? bodyForResult = null;
                if (!success)
                {
                    bodyForResult = await SafeReadAsync(resp);
                    _logger.LogWarning("iCloudイベントの書き込みに失敗しました。Status={Status} Url={Url} Body={Body}", resp.StatusCode, absoluteTarget, bodyForResult);
                }
                else
                {
                    _logger.LogInformation("iCloudイベントを書き込みました。UID={UID}, Created={Created}, Url={Url}", normalizedUid, existingHref == null, absoluteTarget);
                }

                return new CalDavUpsertResult(success, normalizedUid, existingHref == null, absoluteTarget.ToString(), resp.StatusCode, bodyForResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iCloudイベントの書き込み中に例外が発生しました。");
                return new CalDavUpsertResult(false, normalizedUid, existingHref == null, absoluteTarget.ToString(), null, ex.Message);
            }
        }

        public async Task<bool> DeleteEventAsync(string userId, Event ev, CancellationToken ct = default)
        {
            if (ev == null || string.IsNullOrWhiteSpace(ev.UID)) return false;

            var (context, ctxError) = await CreateContextAsync(userId, ct);
            if (context == null)
            {
                _logger.LogWarning("iCloud削除用コンテキストの作成に失敗しました。{Error}", ctxError);
                return false;
            }

            using var ctx = context;
            var target = await FindEventHrefAsync(ctx, ev.UID, ct);
            if (target == null)
            {
                _logger.LogWarning("iCloud上に UID={UID} のイベントが見つかりません。", ev.UID);
                return false;
            }

            try
            {
                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, target.Value.EventHref);
                if (!string.IsNullOrEmpty(target.Value.Etag))
                {
                    deleteRequest.Headers.TryAddWithoutValidation("If-Match", target.Value.Etag);
                }

                var resp = await ctx.Client.SendAsync(deleteRequest, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await SafeReadAsync(resp);
                    _logger.LogWarning("iCloudイベント削除失敗: UID={UID}, Status={Status}, Body={Body}", ev.UID, resp.StatusCode, body);
                    return false;
                }

                _logger.LogInformation("iCloudイベントを削除しました。UID={UID}", ev.UID);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iCloudイベント削除中に例外が発生しました。UID={UID}", ev.UID);
                return false;
            }
        }

        private async Task<(CalDavContext? Context, string? Error)> CreateContextAsync(string userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UserIdが空です。");
                return (null, "UserId が空です。");
            }

            var icloud = await _db.ICloudSettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (icloud == null)
            {
                _logger.LogWarning("iCloud設定が見つかりません。");
                return (null, "iCloud設定が見つかりません。");
            }

            var handler = new HttpClientHandler
            {
                PreAuthenticate = true,
                Credentials = new NetworkCredential(icloud.Username, icloud.Password)
            };

            var client = new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://caldav.icloud.com/")
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{icloud.Username}:{icloud.Password}"))
            );

            // current-user-principal
            var propfindPrincipal = new HttpRequestMessage(new HttpMethod("PROPFIND"), "/.well-known/caldav");
            propfindPrincipal.Headers.Add("Depth", "0");
            propfindPrincipal.Content = new StringContent(@"<?xml version='1.0' encoding='utf-8' ?>
<propfind xmlns='DAV:'>
  <prop>
    <current-user-principal />
  </prop>
</propfind>", Encoding.UTF8, "application/xml");

            HttpResponseMessage principalResponse;
            try
            {
                principalResponse = await client.SendAsync(propfindPrincipal, ct);
                if (!principalResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("current-user-principal取得失敗: {Status}", principalResponse.StatusCode);
                    client.Dispose();
                    return (null, $"current-user-principal 取得に失敗しました。Status={principalResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "current-user-principal取得通信エラー");
                client.Dispose();
                return (null, $"current-user-principal 通信エラー: {ex.Message}");
            }

            var principalXml = XDocument.Parse(await principalResponse.Content.ReadAsStringAsync(ct));
            XNamespace dav = "DAV:";
            var principalHref = principalXml
                .Descendants(dav + "current-user-principal")
                .Descendants(dav + "href")
                .FirstOrDefault()?.Value;

            _logger.LogInformation("取得した principalHref: {Href}", principalHref);
            if (string.IsNullOrEmpty(principalHref))
            {
                _logger.LogWarning("current-user-principal の href が取得できませんでした。");
                client.Dispose();
                return (null, "current-user-principal の href が取得できません。");
            }

            // principal から calendar-home-set を正規に取得
            var propfindHomeSet = new HttpRequestMessage(new HttpMethod("PROPFIND"), principalHref);
            propfindHomeSet.Headers.Add("Depth", "0");
            propfindHomeSet.Content = new StringContent(@"<?xml version='1.0' encoding='utf-8' ?>
<propfind xmlns='DAV:' xmlns:cal='urn:ietf:params:xml:ns:caldav'>
  <prop>
    <cal:calendar-home-set />
  </prop>
</propfind>", Encoding.UTF8, "application/xml");

            HttpResponseMessage homeSetResponse;
            try
            {
                homeSetResponse = await client.SendAsync(propfindHomeSet, ct);
                if (!homeSetResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("calendar-home-set取得失敗: {Status}", homeSetResponse.StatusCode);
                    client.Dispose();
                    return (null, $"calendar-home-set 取得に失敗しました。Status={homeSetResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "calendar-home-set取得通信エラー");
                client.Dispose();
                return (null, $"calendar-home-set 通信エラー: {ex.Message}");
            }

            // iCloud が示すホストへ BaseAddress を合わせる
            var effectiveBase = ExtractBaseUri(homeSetResponse);
            if (!string.IsNullOrEmpty(effectiveBase))
            {
                client.Dispose();
                client = new HttpClient(handler, disposeHandler: false)
                {
                    BaseAddress = new Uri(effectiveBase)
                };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"{icloud.Username}:{icloud.Password}"))
                );
                _logger.LogInformation("CalDAV BaseAddress を {Base} に設定しました。", effectiveBase);
            }

            var homeSetXml = XDocument.Parse(await homeSetResponse.Content.ReadAsStringAsync(ct));
            var calendarHomeUrl = homeSetXml
                .Descendants(XName.Get("calendar-home-set", "urn:ietf:params:xml:ns:caldav"))
                .Descendants(dav + "href")
                .FirstOrDefault()?.Value;

            _logger.LogInformation("使用する calendarHomeUrl: {Href}", calendarHomeUrl);
            if (string.IsNullOrEmpty(calendarHomeUrl))
            {
                _logger.LogWarning("calendar-home-set の href が取得できませんでした。");
                client.Dispose();
                return (null, "calendar-home-set の href が取得できません。");
            }

            // カレンダー一覧
            if (Uri.TryCreate(calendarHomeUrl, UriKind.Absolute, out var calendarHomeAbsolute))
            {
                client.BaseAddress = new Uri(calendarHomeAbsolute.GetLeftPart(UriPartial.Authority));
                calendarHomeUrl = calendarHomeAbsolute.PathAndQuery;
                _logger.LogInformation("BaseAddress をカレンダーホスト {Base} に設定しました。", client.BaseAddress);
            }

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
    <current-user-privilege-set/>
  </prop>
</propfind>", Encoding.UTF8, "application/xml");

            HttpResponseMessage calListResponse;
            try
            {
                calListResponse = await client.SendAsync(calendarQuery, ct);
                if (!calListResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("カレンダーリスト取得失敗: {Status}", calListResponse.StatusCode);
                    client.Dispose();
                    return (null, $"カレンダーリスト取得に失敗しました。Status={calListResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "カレンダーリスト取得中の通信エラー");
                client.Dispose();
                return (null, $"カレンダーリスト取得中に通信エラー: {ex.Message}");
            }

            var calListXml = XDocument.Parse(await calListResponse.Content.ReadAsStringAsync(ct));
            XNamespace calNs = "urn:ietf:params:xml:ns:caldav";

            var calendars = calListXml.Descendants(dav + "response")
                .Where(r => r.Descendants(dav + "resourcetype").Any(rt => rt.Elements(calNs + "calendar").Any()))
                .Select(r =>
                {
                    var href = r.Element(dav + "href")?.Value ?? string.Empty;
                    var name = r.Descendants(dav + "displayname").FirstOrDefault()?.Value ?? string.Empty;
                    var privileges = r.Descendants(dav + "current-user-privilege-set")
                        .Elements()
                        .Select(e => e.Name.LocalName)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var canWrite = privileges.Contains("write") ||
                                   privileges.Contains("write-content") ||
                                   privileges.Contains("write-properties") ||
                                   privileges.Contains("all");

                    return new CalDavCalendar(href, name, canWrite);
                })
                .Where(x => !string.IsNullOrEmpty(x.Href) && !string.IsNullOrEmpty(x.Name))
                .ToList();

            // 書き込み可能カレンダーを優先。無ければ全てを戻す
            var writable = calendars.Where(c => c.CanWrite).ToList();
            _logger.LogInformation("取得カレンダー: {AllCount} 件、書込可: {WritableCount} 件。書込可リスト: {WritableNames}", calendars.Count, writable.Count, string.Join(", ", writable.Select(c => c.Name)));
            if (writable.Any())
            {
                calendars = writable;
            }

            if (!calendars.Any())
            {
                _logger.LogWarning("有効なカレンダーが見つかりませんでした。");
                client.Dispose();
                return (null, "有効なカレンダーが見つかりませんでした。");
            }

            return (new CalDavContext(client, calendars), null);
        }

        private async Task<(string EventHref, CalDavCalendar CalendarHref, string? Etag)?> FindEventHrefAsync(CalDavContext ctx, string uid, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(uid)) return null;

            XNamespace dav = "DAV:";

            foreach (var cal in ctx.Calendars)
            {
                var query = new HttpRequestMessage(new HttpMethod("REPORT"), cal.Href);
                query.Headers.Add("Depth", "1");
                query.Content = new StringContent($@"<?xml version='1.0' encoding='utf-8'?>
<c:calendar-query xmlns:d='DAV:' xmlns:c='urn:ietf:params:xml:ns:caldav'>
  <d:prop>
    <d:getetag/>
  </d:prop>
  <c:filter>
    <c:comp-filter name='VCALENDAR'>
      <c:comp-filter name='VEVENT'>
        <c:prop-filter name='UID'>
          <c:text-match collation='i;octet'>{uid}</c:text-match>
        </c:prop-filter>
      </c:comp-filter>
    </c:comp-filter>
  </c:filter>
</c:calendar-query>", Encoding.UTF8, "application/xml");

                HttpResponseMessage resp;
                try
                {
                    resp = await ctx.Client.SendAsync(query, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "UID検索中に例外が発生しました。Calendar={Cal}", cal.Href);
                    continue;
                }

                if (!resp.IsSuccessStatusCode) continue;

                var xml = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var response = xml.Descendants(dav + "response").FirstOrDefault();
                var href = response?.Element(dav + "href")?.Value;
                var etag = response?.Descendants(dav + "getetag").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(href))
                {
                    return (href!, cal, etag);
                }
            }

            return null;
        }

        private static string ExtractBaseUri(HttpResponseMessage resp)
        {
            // X-Apple-MMe-Host があればそれを優先
            if (resp.Headers.TryGetValues("X-Apple-MMe-Host", out var hosts))
            {
                var host = hosts.FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
                if (!string.IsNullOrWhiteSpace(host))
                {
                    return $"https://{host.TrimEnd('/')}/";
                }
            }

            var requestUri = resp.RequestMessage?.RequestUri;
            if (requestUri != null)
            {
                return requestUri.GetLeftPart(UriPartial.Authority) + "/";
            }
            return string.Empty;
        }

        private static string CombineHref(string baseHref, string relative)
        {
            if (string.IsNullOrEmpty(baseHref)) return relative;
            if (string.IsNullOrEmpty(relative)) return baseHref;
            return $"{baseHref.TrimEnd('/')}/{relative.TrimStart('/')}";
        }

        private static (string Ics, string Uid) BuildIcsPayload(Event ev, bool isUpdate)
        {
            var uid = string.IsNullOrWhiteSpace(ev.UID) ? Guid.NewGuid().ToString("N") : ev.UID.Trim();
            var now = DateTime.UtcNow;
            ev.LastModified = now;
            var start = ev.StartDate ?? now;
            var end = ev.EndDate ?? start;
            var sequence = Math.Min(int.MaxValue, (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var sb = new StringBuilder();
            AppendIcs(sb, "BEGIN:VCALENDAR");
            AppendIcs(sb, "VERSION:2.0");
            AppendIcs(sb, "PRODID:-//TimeLedger//CalDav Client//JA");
            AppendIcs(sb, "CALSCALE:GREGORIAN");
            AppendIcs(sb, "BEGIN:VEVENT");
            AppendIcs(sb, $"UID:{uid}");
            AppendIcs(sb, $"DTSTAMP:{now:yyyyMMdd'T'HHmmss'Z'}");
            AppendIcs(sb, $"LAST-MODIFIED:{now:yyyyMMdd'T'HHmmss'Z'}");
            AppendIcs(sb, $"SEQUENCE:{sequence}");

            if (ev.AllDay)
            {
                var startDate = start.Date;
                var lastDay = end.Date < startDate ? startDate : end.Date;
                var endExclusive = lastDay.AddDays(1);
                AppendIcs(sb, $"DTSTART;VALUE=DATE:{startDate:yyyyMMdd}");
                AppendIcs(sb, $"DTEND;VALUE=DATE:{endExclusive:yyyyMMdd}");
            }
            else
            {
                var startUtc = EnsureUtc(start);
                var endUtcCandidate = EnsureUtc(end);
                var endUtc = endUtcCandidate <= startUtc ? startUtc.AddHours(1) : endUtcCandidate;
                AppendIcs(sb, $"DTSTART:{startUtc:yyyyMMdd'T'HHmmss'Z'}");
                AppendIcs(sb, $"DTEND:{endUtc:yyyyMMdd'T'HHmmss'Z'}");
            }

            AppendIcs(sb, $"SUMMARY:{EscapeIcs(ev.Title)}");

            if (!string.IsNullOrWhiteSpace(ev.Description))
                AppendIcs(sb, $"DESCRIPTION:{EscapeIcs(ev.Description)}");

            if (!string.IsNullOrWhiteSpace(ev.Location))
                AppendIcs(sb, $"LOCATION:{EscapeIcs(ev.Location)}");

            AppendIcs(sb, "END:VEVENT");
            AppendIcs(sb, "END:VCALENDAR");

            return (sb.ToString(), uid);
        }

        private static void AppendIcs(StringBuilder sb, string line)
        {
            sb.Append(line);
            sb.Append("\r\n");
        }

        private static DateTime EnsureUtc(DateTime dt)
        {
            return dt.Kind == DateTimeKind.Utc
                ? dt
                : DateTime.SpecifyKind(dt, dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : dt.Kind).ToUniversalTime();
        }

        private static string EscapeIcs(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace(@"\", @"\\")
                .Replace(";", @"\;")
                .Replace(",", @"\,")
                .Replace("\r\n", @"\n")
                .Replace("\n", @"\n");
        }

        private static async Task<string> SafeReadAsync(HttpResponseMessage resp)
        {
            try
            {
                return await resp.Content.ReadAsStringAsync();
            }
            catch
            {
                return "(response-body-read-error)";
            }
        }
    }
}
