using System;
using System.ComponentModel.DataAnnotations;

namespace TimeLedger.Models;

public enum NoticeKind
{
    Update = 0,
    Incident = 1
}

public class AppNotice
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public NoticeKind Kind { get; set; }

    [MaxLength(32)]
    public string? Version { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Highlights { get; set; } // 改行区切りで箇条書きを格納

    [Required]
    public DateTime OccurredAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    [MaxLength(64)]
    public string? Status { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
