using System;

namespace TimeLedger.Services
{
    public interface ICalendarTimeZoneService
    {
        string ClientTimeZoneId { get; }
        string DisplayName { get; }
        TimeZoneInfo AppTimeZone { get; }

        DateTime? ConvertUtcToLocal(DateTime? utcValue);
        string FormatUtc(DateTime? utcValue, string format);
        DateTime? ParseClientDate(string? value, int? clientOffsetMinutes = null);
        DateTimeOffset? ToOffset(DateTime? localValue);
        string? ToOffsetIso(DateTime? localValue);
    }
}
