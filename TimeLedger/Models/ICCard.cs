// ICCard
// IC カード UID をユーザーと紐付けるモデル。将来の物理カード認証や連携に備え、UserId 外部キーを持つ。

﻿using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace TimeLedger.Models
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
