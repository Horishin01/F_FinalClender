using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeLedger.Models
{
    public class ICloudSetting
    {
        [Key]
        public virtual string Id { get; set; } = Guid.NewGuid().ToString("N"); // 主キー

        [Required]
        public virtual string UserId { get; set; } = string.Empty; // ユーザーID

        [Required]
        [MaxLength(200)]
        public virtual string Username { get; set; } = string.Empty; // Apple ID (メールアドレス)

        [Required]
        [MaxLength(200)]
        public virtual string Password { get; set; } = string.Empty; // iCloudアプリパスワード

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }
    }
}
