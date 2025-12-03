using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeLedger.Models
{
    public class UserAccessLog
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public DateTime AccessedAtUtc { get; set; }

        [MaxLength(16)]
        public string HttpMethod { get; set; } = "GET";

        [MaxLength(512)]
        public string Path { get; set; } = "/";

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        [MaxLength(64)]
        public string? RemoteIp { get; set; }

        public int StatusCode { get; set; }

        public long? DurationMs { get; set; }

        public bool IsError { get; set; }

        [MaxLength(128)]
        public string? ErrorType { get; set; }

        [MaxLength(128)]
        public string? ErrorHash { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }
    }
}
