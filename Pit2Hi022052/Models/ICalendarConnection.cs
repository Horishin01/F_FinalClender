using System;

namespace Pit2Hi022052.Models
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
