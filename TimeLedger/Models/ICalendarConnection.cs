// ICalendarConnection
// 各外部カレンダー接続モデルの共通インターフェース。同期処理でポリモーフィックに扱えるよう主要プロパティを標準化。
using System;

namespace TimeLedger.Models
{
    public interface ICalendarConnection
    {
        string Id { get; }
        string UserId { get; }
        string? AccountEmail { get; }
        string AccessToken { get; }
        string? RefreshToken { get; }
        DateTime? ExpiresAtUtc { get; }
        string? Scope { get; }
        DateTime? LastSyncedAtUtc { get; }
    }
}
