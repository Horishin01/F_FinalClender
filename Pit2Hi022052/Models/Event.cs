using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Pit2Hi022052.Models
{
    public class Event
    {
        [Key]
        [Display (Name ="DBID")]
        public virtual string Id { get; set; } = string.Empty;            // イベントID
       
        [Display(Name = "ユーザID")]
        public virtual string UserId { get; set; } = string.Empty;       //ユーザと紐づけ
        
        [Display(Name = "UID")]
        public string UID { get; set; } = string.Empty;                 // iCloud識別子
        
        [Display(Name = "タイトル")]
        public virtual string Title { get; set; } = string.Empty;       // イベントのタイトル

        [Display(Name = "予定開始日時")]
        public DateTime? StartDate { get; set; } // 開始日 // Nullableにする

        [Display(Name = "予定終了日時")]
        public DateTime? EndDate { get; set; }   // 終了日 // Nullableにする

        [Display(Name = "同期用")]
        public DateTime? LastModified { get; set; } // 差分同期将来用途 // Nullableにする

        [Display(Name = "詳細")]
        public virtual string Description { get; set; } = string.Empty;// イベントの説明


        public virtual bool AllDay { get; set; } = false;

        [ForeignKey(nameof(UserId))] 
        public virtual ApplicationUser? User { get; set; }
    }
    
}
