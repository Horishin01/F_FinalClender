using System;
using System.Collections.Generic;

namespace Pit2Hi022052.ViewModels
{
    public class HomeIndexViewModel
    {
        public bool IsAuthenticated { get; set; }
        public IReadOnlyList<CalendarPreviewEvent> Events { get; set; } = Array.Empty<CalendarPreviewEvent>();
        public IReadOnlyList<Models.BalanceSheetItem> BalanceSheetSeed { get; set; } = Array.Empty<Models.BalanceSheetItem>();
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
