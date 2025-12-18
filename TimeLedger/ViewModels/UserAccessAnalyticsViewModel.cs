// UserAccessAnalyticsViewModel
// アクセスログ分析ビューで使う集計結果セット。サマリー値とグラフ用シーケンスを保持する DTO。
using System;
using System.Collections.Generic;

namespace TimeLedger.ViewModels
{
    public class UserAccessAnalyticsViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsersLast30Days { get; set; }
        public int TotalLogs { get; set; }
        public IReadOnlyList<UserAccessSummary> TopUsers { get; set; } = Array.Empty<UserAccessSummary>();
        public IReadOnlyList<AccessLogRow> RecentLogs { get; set; } = Array.Empty<AccessLogRow>();
        public IReadOnlyList<AccessChartPoint> DailySeries { get; set; } = Array.Empty<AccessChartPoint>();
        public IReadOnlyList<AccessChartPoint> HourlySeries { get; set; } = Array.Empty<AccessChartPoint>();
        public DateTime GeneratedAt { get; set; }
    }

    public class UserAccessSummary
    {
        public string UserId { get; set; } = string.Empty;
        public string UserLabel { get; set; } = string.Empty;
        public int AccessCount { get; set; }
        public DateTime? LastAccessAtUtc { get; set; }
    }

    public class AccessLogRow
    {
        public string UserId { get; set; } = string.Empty;
        public string UserLabel { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = "GET";
        public DateTime AccessedAtUtc { get; set; }
        public string? UserAgent { get; set; }
        public string? RemoteIp { get; set; }
        public int StatusCode { get; set; }
        public long? DurationMs { get; set; }
        public bool IsError { get; set; }
        public string? ErrorType { get; set; }
        public string? ErrorHash { get; set; }
    }

    public class AccessChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
