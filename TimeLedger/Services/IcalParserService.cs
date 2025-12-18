using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
// IcalParserService
// 外部 ICS（iCal）ファイルを Event モデルへ変換するヘルパー。微調整しやすいようメソッドを小分けにしている。

using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Logging;
using TimeLedger.Models;

// Ical.Net 型のエイリアス
using IcalCalendar = Ical.Net.Calendar;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;
using IcalCalDateTime = Ical.Net.DataTypes.CalDateTime;

namespace TimeLedger.Services
{
    public class IcalParserService
    {
        private readonly ILogger<IcalParserService>? _logger;
        public IcalParserService(ILogger<IcalParserService>? logger = null) { _logger = logger; }

        public List<Event> ParseIcsToEventList(string icsData)
        {
            var result = new List<Event>();
            if (string.IsNullOrWhiteSpace(icsData)) return result;

            IcalCalendar cal;
            try
            {
                cal = IcalCalendar.Load(icsData);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ICSの読み込みに失敗しました。");
                return result;
            }

            foreach (var ev in cal.Events ?? Enumerable.Empty<IcalEvent>())
            {
                var uid = (ev.Uid ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(uid)) continue;

                var start = (ev.DtStart as IcalCalDateTime)?.Value;
                var end = (ev.DtEnd as IcalCalDateTime)?.Value;
                var last = (ev.LastModified as IcalCalDateTime)?.Value;

                var allDay = ev.DtStart is IcalCalDateTime s && s.HasTime == false;

                result.Add(new Event
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UID = uid,
                    Title = ev.Summary?.Trim() ?? string.Empty,
                    Description = ev.Description?.Trim() ?? string.Empty,
                    StartDate = start,
                    EndDate = end,
                    LastModified = last,
                    AllDay = allDay
                });
            }

            _logger?.LogInformation("ParseIcsToEventList 出力: {Count} 件", result.Count);
            return result;
        }
    }
}
