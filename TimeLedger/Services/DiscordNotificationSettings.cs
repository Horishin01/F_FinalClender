namespace TimeLedger.Services;

/// <summary>
/// Discord Webhook 通知の設定。WebhookUrl は appsettings へ保存せず、
/// 環境変数または Secret Manager から与える。
/// </summary>
public sealed class DiscordNotificationSettings
{
    public const string SectionName = "DiscordNotifications";

    public bool Enabled { get; set; }

    public string? WebhookUrl { get; set; }

    public int PollIntervalSeconds { get; set; } = 60;
}
