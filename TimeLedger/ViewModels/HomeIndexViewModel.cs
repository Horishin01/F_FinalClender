// HomeIndexViewModel / CalendarPreviewEvent
// ホーム画面のプレビュー用に、最小限のイベント情報と認証状態を運ぶビューモデル。
using System;
using System.Collections.Generic;

namespace TimeLedger.ViewModels
{
    public class HomeIndexViewModel
    {
        public bool IsAuthenticated { get; set; }
        public IReadOnlyList<CalendarPreviewEvent> Events { get; set; } = Array.Empty<CalendarPreviewEvent>();
    }

    public class CalendarPreviewEvent
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool AllDay { get; set; }
    }
}
