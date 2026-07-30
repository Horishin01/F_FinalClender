using System;
using System.ComponentModel.DataAnnotations;

namespace TimeLedger.Models;

/// <summary>
/// Discord へ送信済みの予定リマインダーを記録し、同じ予定の重複送信を防ぐ。
/// Webhook URL や本文は保存しない。
/// </summary>
public class DiscordNotificationDelivery
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public string EventId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string NotificationKind { get; set; } = string.Empty;

    public DateTime ScheduledAtUtc { get; set; }

    public DateTime SentAtUtc { get; set; }
}
