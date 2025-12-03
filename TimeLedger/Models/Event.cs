using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace TimeLedger.Models
{
    // 統合カレンダー管理機能追加 (2025-11) 用の拡張フィールドを含むイベントモデル
    public enum EventSource
    {
        Local,
        Google,
        ICloud,
        Outlook,
        Work
    }

    public enum EventPriority
    {
        Low,
        Normal,
        High
    }

    public enum EventRecurrence
    {
        None,
        Daily,
        Weekly,
        Biweekly,
        Monthly
    }

    public class Event
    {
        [Key]
        [Display (Name ="DBID")]
        public virtual string Id { get; set; } = string.Empty;            // イベントID
       
        [Display(Name = "ユーザID")]
        public virtual string UserId { get; set; } = string.Empty;       //ユーザと紐づけ
        
        [Display(Name = "UID")]
        public string? UID { get; set; } = null;                 // iCloud識別子（空可）
        
        [Display(Name = "タイトル")]
        public virtual string Title { get; set; } = string.Empty;       // イベントのタイトル

        [Display(Name = "予定開始日時")]
        public DateTime? StartDate { get; set; } // 開始日 // Nullableにする

        [Display(Name = "予定終了日時")]
        public DateTime? EndDate { get; set; }   // 終了日 // Nullableにする

        [Display(Name = "同期用")]
        public DateTime? LastModified { get; set; } // 差分同期将来用途 // Nullableにする

        [Display(Name = "詳細")]
        public virtual string? Description { get; set; } = string.Empty;// イベントの説明（空可）

        // 統合カレンダー: 拡張メタ
        [Display(Name = "ソース")]
        public EventSource Source { get; set; } = EventSource.Local;

        [Display(Name = "カテゴリ")]
        public string? CategoryId { get; set; }

        [Display(Name = "優先度")]
        public EventPriority Priority { get; set; } = EventPriority.Normal;

        [Display(Name = "場所")]
        public string? Location { get; set; } = string.Empty;

        [Display(Name = "参加者")]
        public string? AttendeesCsv { get; set; } = string.Empty; // カンマ区切り

        [Display(Name = "繰り返し")]
        public EventRecurrence Recurrence { get; set; } = EventRecurrence.None;

        [Display(Name = "リマインダー(分前)")]
        public int? ReminderMinutesBefore { get; set; }

        // 既存のAllDayをIsAllDayとしても扱えるようにする（API整合用）
        public virtual bool AllDay { get; set; } = false;
        [NotMapped]
        public bool IsAllDay
        {
            get => AllDay;
            set => AllDay = value;
        }

        [ForeignKey(nameof(UserId))] 
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public virtual CalendarCategory? Category { get; set; }
    }
    
}
