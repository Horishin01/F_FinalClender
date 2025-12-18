// AnalyticsController
// 管理者向けのアクセスログ分析エンドポイント。直近のユーザーアクセスを集計し、ビュー用の統計モデルを組み立てる。

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeLedger.Data;
using TimeLedger.Models;
using TimeLedger.ViewModels;

namespace TimeLedger.Controllers
{
    [Authorize(Roles = RoleNames.Admin)]
    public class AnalyticsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AnalyticsController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> UserAccess(CancellationToken cancellationToken)
        {
            var nowLocal = DateTime.Now;
            var rangeStartUtc = nowLocal.AddDays(-30).ToUniversalTime();

            var logs = await _db.UserAccessLogs
                .Where(l => l.AccessedAtUtc >= rangeStartUtc)
                .OrderByDescending(l => l.AccessedAtUtc)
                .Take(800)
                .ToListAsync(cancellationToken);

            var topUsers = logs
                .GroupBy(l => l.UserId)
                .Select(g => new UserAccessSummary
                {
                    UserId = g.Key,
                    UserLabel = BuildSafeUserLabel(g.Key),
                    AccessCount = g.Count(),
                    LastAccessAtUtc = g.Max(x => x.AccessedAtUtc)
                })
                .OrderByDescending(x => x.AccessCount)
                .ThenByDescending(x => x.LastAccessAtUtc)
                .Take(8)
                .ToList();

            var dailySeries = BuildDailySeries(logs, nowLocal);
            var hourlySeries = BuildHourlySeries(logs, nowLocal);

            var model = new UserAccessAnalyticsViewModel
            {
                TotalUsers = await _db.Users.CountAsync(cancellationToken),
                ActiveUsersLast30Days = logs.Select(l => l.UserId).Distinct().Count(),
                TotalLogs = await _db.UserAccessLogs.CountAsync(cancellationToken),
                TopUsers = topUsers,
                RecentLogs = logs
                    .Take(80)
                    .Select(l => new AccessLogRow
                    {
                        UserId = l.UserId,
                        UserLabel = BuildSafeUserLabel(l.UserId),
                        Path = l.Path,
                        HttpMethod = l.HttpMethod,
                        AccessedAtUtc = l.AccessedAtUtc,
                        UserAgent = l.UserAgent,
                        RemoteIp = l.RemoteIp,
                        StatusCode = l.StatusCode,
                        DurationMs = l.DurationMs,
                        IsError = l.IsError,
                        ErrorType = l.ErrorType,
                        ErrorHash = l.ErrorHash
                    })
                    .ToList(),
                DailySeries = dailySeries,
                HourlySeries = hourlySeries,
                GeneratedAt = nowLocal
            };

            return View(model);
        }

        private static List<AccessChartPoint> BuildDailySeries(IEnumerable<UserAccessLog> logs, DateTime nowLocal)
        {
            var start = nowLocal.Date.AddDays(-13);

            return Enumerable.Range(0, 14)
                .Select(offset =>
                {
                    var date = start.AddDays(offset);
                    var count = logs.Count(l => l.AccessedAtUtc.ToLocalTime().Date == date.Date);
                    return new AccessChartPoint
                    {
                        Label = date.ToString("MM/dd"),
                        Count = count
                    };
                })
                .ToList();
        }

        private static List<AccessChartPoint> BuildHourlySeries(IEnumerable<UserAccessLog> logs, DateTime nowLocal)
        {
            var start = nowLocal.AddHours(-23);

            return Enumerable.Range(0, 24)
                .Select(offset =>
                {
                    var bucketStart = start.AddHours(offset);
                    var bucketEnd = bucketStart.AddHours(1);
                    var count = logs.Count(l =>
                    {
                        var local = l.AccessedAtUtc.ToLocalTime();
                        return local >= bucketStart && local < bucketEnd;
                    });

                    return new AccessChartPoint
                    {
                        Label = bucketStart.ToString("HH:mm"),
                        Count = count
                    };
                })
                .ToList();
        }

        private static string BuildSafeUserLabel(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return "User-unknown";
            }

            return $"User-{userId[..Math.Min(userId.Length, 8)]}";
        }
    }
}
