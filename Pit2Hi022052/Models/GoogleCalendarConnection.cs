using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Pit2Hi022052.Models
{
    [Index(nameof(UserId), IsUnique = true)]
    public class GoogleCalendarConnection : ICalendarConnection
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(32)]
        public string Provider { get; set; } = "Google";

        [MaxLength(256)]
        public string? AccountEmail { get; set; }

        [MaxLength(1024)]
        public string AccessTokenEncrypted { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string? RefreshTokenEncrypted { get; set; }

        public DateTime? ExpiresAtUtc { get; set; }

        [MaxLength(512)]
        public string? Scope { get; set; }

        public DateTime? LastSyncedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        [NotMapped]
        public string AccessToken => AccessTokenEncrypted;

        [NotMapped]
        public string? RefreshToken => RefreshTokenEncrypted;
    }
}
