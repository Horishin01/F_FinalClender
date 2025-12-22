// CalendarCategory
// ユーザーごとのカテゴリ設定モデル。色・アイコン・並び順を保持し、イベント紐付け時の分類に利用する。

﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeLedger.Models
{
    public class CalendarCategory
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(32)]
        public string Icon { get; set; } = "fa-briefcase"; // FontAwesomeクラス名など

        [MaxLength(16)]
        public string Color { get; set; } = "#667eea";

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }
    }
}
