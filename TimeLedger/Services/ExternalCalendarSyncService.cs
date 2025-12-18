// ExternalCalendarSyncService & related clients
// Outlook/Google から外部イベントを取得し、ローカル DB へ upsert する同期パイプライン。クライアント実装と DTO を同居させている。

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Services
{
    public record ExternalCalendarEventDto(
        string Uid,
        string Title,
        DateTime? Start,
        DateTime? End,
        bool AllDay,
        string? Description,
        string? Location);

    public interface IExternalCalendarClient
    {
        ExternalCalendarProvider Provider { get; }
        Task<IReadOnlyList<ExternalCalendarEventDto>> FetchEventsAsync(ICalendarConnection connection, DateTime from, DateTime to, CancellationToken ct = default);
    }

    public class OutlookCalendarClient : IExternalCalendarClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<OutlookCalendarClient> _logger;
        public ExternalCalendarProvider Provider => ExternalCalendarProvider.Outlook;

        public OutlookCalendarClient(HttpClient http, ILogger<OutlookCalendarClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ExternalCalendarEventDto>> FetchEventsAsync(ICalendarConnection connection, DateTime from, DateTime to, CancellationToken ct = default)
        {
            // TODO: Implement Microsoft Graph call with access token (Calendars.Read)
            _logger.LogInformation("Outlook fetch stub: {User}", connection.UserId);
            await Task.CompletedTask;
            return Array.Empty<ExternalCalendarEventDto>();
        }
    }

    public class GoogleCalendarClient : IExternalCalendarClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<GoogleCalendarClient> _logger;
        public ExternalCalendarProvider Provider => ExternalCalendarProvider.Google;

        public GoogleCalendarClient(HttpClient http, ILogger<GoogleCalendarClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ExternalCalendarEventDto>> FetchEventsAsync(ICalendarConnection connection, DateTime from, DateTime to, CancellationToken ct = default)
        {
            // TODO: Implement Google Calendar API call with access token (calendar.readonly)
            _logger.LogInformation("Google fetch stub: {User}", connection.UserId);
            await Task.CompletedTask;
            return Array.Empty<ExternalCalendarEventDto>();
        }
    }

    public class ExternalCalendarSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEnumerable<IExternalCalendarClient> _clients;
        private readonly ILogger<ExternalCalendarSyncService> _logger;
        private readonly IOutlookCalendarService _outlookService;
        private readonly IGoogleCalendarService _googleService;

        public ExternalCalendarSyncService(
            ApplicationDbContext db,
            IEnumerable<IExternalCalendarClient> clients,
            ILogger<ExternalCalendarSyncService> logger,
            IOutlookCalendarService outlookService,
            IGoogleCalendarService googleService)
        {
            _db = db;
            _clients = clients;
            _logger = logger;
            _outlookService = outlookService;
            _googleService = googleService;
        }

        public async Task<int> SyncAsync(string userId, ExternalCalendarProvider provider, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var connection = await GetConnectionAsync(userId, provider, ct);
            if (connection == null) throw new InvalidOperationException("連携設定が見つかりません。");

            var client = _clients.FirstOrDefault(c => c.Provider == provider);
            if (client == null) throw new InvalidOperationException("クライアントが登録されていません。");

            var remoteEvents = await client.FetchEventsAsync(connection, from, to, ct);
            if (remoteEvents.Count == 0)
            {
                await UpdateLastSyncedAsync(userId, provider, DateTime.UtcNow, ct);
                return 0;
            }

            var existing = await _db.Events
                .Where(e => e.UserId == userId && e.Source.ToString() == provider.ToString())
                .ToListAsync(ct);
            var existingByUid = existing.Where(e => !string.IsNullOrWhiteSpace(e.UID)).ToDictionary(e => e.UID!, StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            int saved = 0;
            foreach (var dto in remoteEvents)
            {
                if (string.IsNullOrWhiteSpace(dto.Uid)) continue;
                if (existingByUid.TryGetValue(dto.Uid, out var ev))
                {
                    ev.Title = dto.Title;
                    ev.StartDate = dto.Start;
                    ev.EndDate = dto.End;
                    ev.AllDay = dto.AllDay;
                    ev.Description = dto.Description ?? string.Empty;
                    ev.Location = dto.Location ?? string.Empty;
                    ev.LastModified = now;
                }
                else
                {
                    _db.Events.Add(new Event
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        UserId = userId,
                        UID = dto.Uid,
                        Title = dto.Title,
                        StartDate = dto.Start,
                        EndDate = dto.End,
                        AllDay = dto.AllDay,
                        Description = dto.Description ?? string.Empty,
                        Location = dto.Location ?? string.Empty,
                        Source = provider == ExternalCalendarProvider.Outlook ? EventSource.Outlook : EventSource.Google,
                        CategoryId = null,
                        Priority = EventPriority.Normal,
                        Recurrence = EventRecurrence.None,
                        LastModified = now
                    });
                }
                saved++;
            }

            await _db.SaveChangesAsync(ct);
            await UpdateLastSyncedAsync(userId, provider, now, ct);
            return saved;
        }

        private async Task<ICalendarConnection?> GetConnectionAsync(string userId, ExternalCalendarProvider provider, CancellationToken ct)
        {
            return provider switch
            {
                ExternalCalendarProvider.Outlook => await _outlookService.EnsureValidAccessTokenAsync(userId, ct),
                ExternalCalendarProvider.Google => await _googleService.EnsureValidAccessTokenAsync(userId, ct),
                _ => null
            };
        }

        private Task UpdateLastSyncedAsync(string userId, ExternalCalendarProvider provider, DateTime time, CancellationToken ct)
        {
            return provider switch
            {
                ExternalCalendarProvider.Outlook => _outlookService.UpdateLastSyncedAtAsync(userId, time, ct),
                ExternalCalendarProvider.Google => _googleService.UpdateLastSyncedAtAsync(userId, time, ct),
                _ => Task.CompletedTask
            };
        }
    }
}
