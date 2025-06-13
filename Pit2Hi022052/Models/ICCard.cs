using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Pit2Hi022052.Models
{
    public class ICCard
    {
        [Key]
        public virtual string Id { get; set; } = string.Empty; //主キー

        [Required]
        public virtual　string UserId { get; set; } = string.Empty; // ユーザーID

        [Required]
        [MaxLength(50)]
        public virtual string Uid { get; set; } = string.Empty; // ICカードUID

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }
    }
}
