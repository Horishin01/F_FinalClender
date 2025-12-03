using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pit2Hi022052.Models
{
    public enum ExternalCalendarProvider
    {
        Outlook,
        Google
    }

    public class ExternalCalendarAccount
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public ExternalCalendarProvider Provider { get; set; }

        [MaxLength(256)]
        public string? AccountEmail { get; set; }

        [MaxLength(1024)]
        public string AccessToken { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string? RefreshToken { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }

        [MaxLength(256)]
        public string? Scope { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }
    }
}
