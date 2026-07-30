using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeLedger.Data;
using TimeLedger.Models;

namespace TimeLedger.Services;

/// <summary>
/// ローカルの単発・時間指定予定について、既存の ReminderMinutesBefore を基に
/// Discord Webhook へ開始前通知を送るバックグラウンドワーカー。
/// </summary>
public sealed class DiscordReminderWorker : BackgroundService
{
    private const string ReminderKind = "event-reminder";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<DiscordNotificationSettings> _settings;
    private readonly ICalendarTimeZoneService _timeZone;
    private readonly ILogger<DiscordReminderWorker> _logger;

    public DiscordReminderWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<DiscordNotificationSettings> settings,
        ICalendarTimeZoneService timeZone,
        ILogger<DiscordReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _timeZone = timeZone;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _settings.Value;
            if (settings.Enabled && TryGetWebhookUri(settings.WebhookUrl, out var webhookUri))
            {
                try
                {
                    await SendDueRemindersAsync(webhookUri, settings.PollIntervalSeconds, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Discord予定リマインダーの処理に失敗しました。");
                }
            }
            else if (settings.Enabled)
            {
                _logger.LogWarning("Discord通知は有効ですが、Webhook URLが未設定または許可されない形式です。");
            }

            var intervalSeconds = Math.Clamp(settings.PollIntervalSeconds, 15, 300);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task SendDueRemindersAsync(Uri webhookUri, int pollIntervalSeconds, CancellationToken cancellationToken)
    {
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone.AppTimeZone).DateTime;
        var lookback = TimeSpan.FromMinutes(Math.Max(5, Math.Clamp(pollIntervalSeconds, 15, 300) / 60.0 * 2));
        var earliestDue = nowLocal.Subtract(lookback);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 長期間先の予定を全件走査しないため、次の7日間だけを候補にする。
        var candidates = await db.Events!
            .AsNoTracking()
            .Where(e => e.StartDate.HasValue
                && e.ReminderMinutesBefore.HasValue
                && !e.AllDay
                && e.Recurrence == EventRecurrence.None
                && e.StartDate.Value >= earliestDue
                && e.StartDate.Value <= nowLocal.AddDays(7))
            .ToListAsync(cancellationToken);

        foreach (var calendarEvent in candidates)
        {
            var reminderAtLocal = calendarEvent.StartDate!.Value.AddMinutes(-calendarEvent.ReminderMinutesBefore!.Value);
            if (reminderAtLocal > nowLocal || reminderAtLocal < earliestDue)
            {
                continue;
            }

            var scheduledAtUtc = ToUtc(reminderAtLocal);
            var alreadySent = await db.DiscordNotificationDeliveries
                .AnyAsync(x => x.EventId == calendarEvent.Id
                    && x.NotificationKind == ReminderKind
                    && x.ScheduledAtUtc == scheduledAtUtc, cancellationToken);
            if (alreadySent)
            {
                continue;
            }

            var delivery = new DiscordNotificationDelivery
            {
                EventId = calendarEvent.Id,
                UserId = calendarEvent.UserId,
                NotificationKind = ReminderKind,
                ScheduledAtUtc = scheduledAtUtc,
                SentAtUtc = DateTime.UnixEpoch
            };

            db.DiscordNotificationDeliveries.Add(delivery);
            try
            {
                // 先に一意キーを確保し、複数ワーカーが同一予定を送らないようにする。
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                continue;
            }

            if (await SendWebhookAsync(webhookUri, calendarEvent, cancellationToken))
            {
                delivery.SentAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            // 送信失敗は次回のポーリングで再試行できるよう記録を取り消す。
            db.DiscordNotificationDeliveries.Remove(delivery);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> SendWebhookAsync(Uri webhookUri, Event calendarEvent, CancellationToken cancellationToken)
    {
        var start = calendarEvent.StartDate!.Value;
        var title = Truncate(calendarEvent.Title, 1_700);
        var content = $"📅 予定リマインダー\n**{title}**\n開始: {start:yyyy-MM-dd HH:mm} ({_timeZone.AppTimeZone.StandardName})\n{calendarEvent.ReminderMinutesBefore}分後に開始します。";

        using var request = new HttpRequestMessage(HttpMethod.Post, webhookUri)
        {
            Content = JsonContent.Create(new
            {
                content,
                allowed_mentions = new { parse = Array.Empty<string>() }
            })
        };

        var client = _httpClientFactory.CreateClient(nameof(DiscordReminderWorker));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        _logger.LogWarning("Discord予定リマインダーの送信に失敗しました。状態コード: {StatusCode}", (int)response.StatusCode);
        return false;
    }

    private DateTime ToUtc(DateTime localTime)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), _timeZone.AppTimeZone);

    private static bool TryGetWebhookUri(string? value, out Uri uri)
    {
        uri = null!;
        return Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            && candidate.Scheme == Uri.UriSchemeHttps
            && string.Equals(candidate.Host, "discord.com", StringComparison.OrdinalIgnoreCase)
            && candidate.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);
    }

    private static string Truncate(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "（無題の予定）" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..(maxLength - 1)] + "…";
    }
}
