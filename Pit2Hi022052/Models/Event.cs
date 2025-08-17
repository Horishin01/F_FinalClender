using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Pit2Hi022052.Models
{
    public class Event
    {
        [Key]
        public virtual string Id { get; set; } = string.Empty;            // イベントID
        public virtual string UserId { get; set; } = string.Empty;       //ユーザと紐づけ
        public virtual string Title { get; set; } = string.Empty;       // イベントのタイトル
        public DateTime? StartDate { get; set; } // 開始日 // Nullableにする

        public DateTime? EndDate { get; set; }   // 終了日 // Nullableにする

        public virtual string Description { get; set; } = string.Empty;// イベントの説明


        public virtual bool AllDay { get; set; } = false;

        [ForeignKey(nameof(UserId))] 
        public virtual ApplicationUser? User { get; set; }
    }
    
}
