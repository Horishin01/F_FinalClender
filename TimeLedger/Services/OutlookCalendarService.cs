using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TimeLedger.Data;
using TimeLedger.Extensions;
using TimeLedger.Models;

namespace TimeLedger.Services
{
    public interface IOutlookCalendarService
    {
        Task<OutlookCalendarConnection?> GetConnectionAsync(string userId, CancellationToken ct = default);
        Task<OutlookCalendarConnection?> EnsureValidAccessTokenAsync(string userId, CancellationToken ct = default);
        Task<OutlookCalendarConnection> SaveTokensAsync(string userId, string? accountEmail, string accessToken, string? refreshToken, DateTime? expiresAtUtc, string scope, CancellationToken ct = default);
        Task RemoveConnectionAsync(string userId, CancellationToken ct = default);
        Task UpdateLastSyncedAtAsync(string userId, DateTime syncedAtUtc, CancellationToken ct = default);
    }

    public class OutlookCalendarService : IOutlookCalendarService
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OutlookCalendarService> _logger;

        public OutlookCalendarService(
            ApplicationDbContext db,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<OutlookCalendarService> logger)
        {
            _db = db;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public Task<OutlookCalendarConnection?> GetConnectionAsync(string userId, CancellationToken ct = default) =>
            _db.OutlookCalendarConnections.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct);

        public async Task<OutlookCalendarConnection?> EnsureValidAccessTokenAsync(string userId, CancellationToken ct = default)
        {
            var connection = await _db.OutlookCalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId, ct);
            if (connection == null) return null;

            if (NeedsRefresh(connection) && !string.IsNullOrWhiteSpace(connection.RefreshTokenEncrypted))
            {
                var refreshed = await RefreshAccessTokenAsync(connection.RefreshTokenEncrypted!, ct);
                if (refreshed != null)
                {
                    connection.AccessTokenEncrypted = refreshed.AccessToken;
                    if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                    {
                        connection.RefreshTokenEncrypted = refreshed.RefreshToken;
                    }
                    connection.ExpiresAtUtc = refreshed.ExpiresAtUtc;
                    connection.Scope = refreshed.Scope;
                    connection.UpdatedAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
            }

            return connection;
        }

        public async Task<OutlookCalendarConnection> SaveTokensAsync(string userId, string? accountEmail, string accessToken, string? refreshToken, DateTime? expiresAtUtc, string scope, CancellationToken ct = default)
        {
            var connection = await _db.OutlookCalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId, ct);
            var now = DateTime.UtcNow;

            if (connection == null)
            {
                connection = new OutlookCalendarConnection
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    CreatedAtUtc = now
                };
                _db.OutlookCalendarConnections.Add(connection);
            }

            if (!string.IsNullOrWhiteSpace(accountEmail))
            {
                connection.AccountEmail = accountEmail;
            }

            // TODO: encrypt tokens before persistence when data protection is configured.
            connection.AccessTokenEncrypted = accessToken;
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                connection.RefreshTokenEncrypted = refreshToken;
            }

            connection.ExpiresAtUtc = expiresAtUtc;
            connection.Scope = scope;
            connection.UpdatedAtUtc = now;

            await _db.SaveChangesAsync(ct);
            return connection;
        }

        public async Task RemoveConnectionAsync(string userId, CancellationToken ct = default)
        {
            var connection = await _db.OutlookCalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId, ct);
            if (connection == null) return;

            _db.OutlookCalendarConnections.Remove(connection);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateLastSyncedAtAsync(string userId, DateTime syncedAtUtc, CancellationToken ct = default)
        {
            var connection = await _db.OutlookCalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId, ct);
            if (connection == null) return;

            connection.LastSyncedAtUtc = syncedAtUtc;
            connection.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        private static bool NeedsRefresh(ICalendarConnection connection)
        {
            if (!connection.ExpiresAtUtc.HasValue) return false;
            return connection.ExpiresAtUtc.Value <= DateTime.UtcNow.AddMinutes(1);
        }

        private string GetClientId() =>
            _configuration["Authentication:Outlook:ClientId"]
            ?? throw new InvalidOperationException("TODO: Authentication:Outlook:ClientId を appsettings に設定してください。");

        private string GetClientSecret() =>
            _configuration["Authentication:Outlook:ClientSecret"]
            ?? throw new InvalidOperationException("TODO: Authentication:Outlook:ClientSecret を appsettings に設定してください。");

        private async Task<TokenRefreshResult?> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();

            var payload = new Dictionary<string, string>
            {
                ["client_id"] = GetClientId(),
                ["client_secret"] = GetClientSecret(),
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["scope"] = string.Join(" ", CalendarAuthDefaults.OutlookScopes)
            };

            var response = await client.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", new FormUrlEncodedContent(payload), ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Outlook token refresh failed: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<TokenRefreshPayload>(cancellationToken: ct);
            if (json == null || string.IsNullOrWhiteSpace(json.AccessToken))
            {
                _logger.LogWarning("Outlook token refresh returned empty payload");
                return null;
            }

            var expiresAt = DateTime.UtcNow.AddSeconds(json.ExpiresInSeconds ?? 3600);
            return new TokenRefreshResult(json.AccessToken, json.RefreshToken ?? refreshToken, expiresAt, json.Scope ?? string.Join(" ", CalendarAuthDefaults.OutlookScopes));
        }

        private record TokenRefreshResult(string AccessToken, string? RefreshToken, DateTime? ExpiresAtUtc, string Scope);

        private class TokenRefreshPayload
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int? ExpiresInSeconds { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }
        }
    }
}
