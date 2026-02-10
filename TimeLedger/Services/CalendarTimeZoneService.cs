using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using TimeLedger.Models;

namespace TimeLedger.Services
{
    public sealed class CalendarTimeZoneService : ICalendarTimeZoneService
    {
        private static readonly string[] IsoOffsetFormats =
        {
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mmK"
        };

        private static readonly string[] IsoLocalFormats =
        {
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm"
        };

        private static readonly Dictionary<string, string> IanaToWindowsMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Asia/Tokyo", "Tokyo Standard Time" },
            { "UTC", "UTC" },
            { "Etc/UTC", "UTC" },
            { "America/New_York", "Eastern Standard Time" },
            { "America/Chicago", "Central Standard Time" },
            { "America/Denver", "Mountain Standard Time" },
            { "America/Los_Angeles", "Pacific Standard Time" },
            { "Europe/London", "GMT Standard Time" },
            { "Europe/Paris", "Romance Standard Time" },
            { "Asia/Seoul", "Korea Standard Time" },
            { "Asia/Shanghai", "China Standard Time" },
            { "Australia/Sydney", "AUS Eastern Standard Time" },
            { "Pacific/Auckland", "New Zealand Standard Time" }
        };

        private static readonly Dictionary<string, string> WindowsToIanaMap = IanaToWindowsMap
            .GroupBy(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        private readonly TimeZoneInfo _appTimeZone;

        public CalendarTimeZoneService(IOptions<CalendarSettings> options)
        {
            var settings = options.Value ?? new CalendarSettings();
            var configuredTimeZoneId = string.IsNullOrWhiteSpace(settings.DefaultTimeZoneId)
                ? "Asia/Tokyo"
                : settings.DefaultTimeZoneId;
            _appTimeZone = ResolveTimeZone(configuredTimeZoneId);
            ClientTimeZoneId = string.IsNullOrWhiteSpace(settings.ClientTimeZoneId)
                ? NormalizeToIana(configuredTimeZoneId)
                : settings.ClientTimeZoneId!.Trim();
            DisplayName = _appTimeZone.DisplayName;
        }

        public string ClientTimeZoneId { get; }
        public string DisplayName { get; }
        public TimeZoneInfo AppTimeZone => _appTimeZone;

        public DateTime? ConvertUtcToLocal(DateTime? utcValue)
        {
            if (!utcValue.HasValue) return null;
            var utc = DateTime.SpecifyKind(utcValue.Value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _appTimeZone);
        }

        public string FormatUtc(DateTime? utcValue, string format)
        {
            var local = ConvertUtcToLocal(utcValue);
            return local.HasValue ? local.Value.ToString(format, CultureInfo.InvariantCulture) : string.Empty;
        }

        public DateTime? ParseClientDate(string? value, int? clientOffsetMinutes = null)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();

            if (HasExplicitOffset(trimmed) &&
                DateTimeOffset.TryParseExact(trimmed, IsoOffsetFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var dtoExact))
            {
                var local = TimeZoneInfo.ConvertTime(dtoExact, _appTimeZone);
                return local.DateTime;
            }

            if (HasExplicitOffset(trimmed) &&
                DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                    out var dtoWithOffset))
            {
                var local = TimeZoneInfo.ConvertTime(dtoWithOffset, _appTimeZone);
                return local.DateTime;
            }

            if (DateTime.TryParseExact(trimmed, IsoLocalFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var localExact))
            {
                return DateTime.SpecifyKind(localExact, DateTimeKind.Unspecified);
            }

            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var localLike))
            {
                return DateTime.SpecifyKind(localLike, DateTimeKind.Unspecified);
            }

            if (clientOffsetMinutes.HasValue &&
                DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dtoFallback))
            {
                var clientOffset = TimeSpan.FromMinutes(-clientOffsetMinutes.Value);
                var dtoAsClient = new DateTimeOffset(dtoFallback.DateTime, clientOffset);
                var local = TimeZoneInfo.ConvertTime(dtoAsClient, _appTimeZone);
                return local.DateTime;
            }

            return null;
        }

        public DateTimeOffset? ToOffset(DateTime? localValue)
        {
            if (!localValue.HasValue) return null;
            var local = DateTime.SpecifyKind(localValue.Value, DateTimeKind.Unspecified);
            if (_appTimeZone.IsInvalidTime(local))
            {
                local = local.AddHours(1);
            }

            var offset = _appTimeZone.GetUtcOffset(local);
            if (_appTimeZone.IsAmbiguousTime(local))
            {
                var offsets = _appTimeZone.GetAmbiguousTimeOffsets(local);
                if (offsets.Length > 0)
                {
                    offset = offsets.OrderByDescending(o => o).First();
                }
            }

            return new DateTimeOffset(local, offset);
        }

        public string? ToOffsetIso(DateTime? localValue)
        {
            var dto = ToOffset(localValue);
            return dto?.ToString("o", CultureInfo.InvariantCulture);
        }

        private static bool HasExplicitOffset(string value)
        {
            var tPos = value.IndexOf('T');
            if (tPos < 0) return false;
            if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) return true;
            var tail = value.Substring(tPos + 1);
            return tail.Contains("+") || tail.LastIndexOf('-') > tail.IndexOf(':');
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            if (TryFindTimeZone(timeZoneId, out var tz)) return tz;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (IanaToWindowsMap.TryGetValue(timeZoneId, out var windowsId) &&
                    TryFindTimeZone(windowsId, out tz))
                {
                    return tz;
                }
            }
            else
            {
                if (WindowsToIanaMap.TryGetValue(timeZoneId, out var ianaId) &&
                    TryFindTimeZone(ianaId, out tz))
                {
                    return tz;
                }
            }

            return TimeZoneInfo.Utc;
        }

        private static bool TryFindTimeZone(string timeZoneId, out TimeZoneInfo tz)
        {
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.Utc;
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                tz = TimeZoneInfo.Utc;
                return false;
            }
        }

        private static string NormalizeToIana(string timeZoneId)
        {
            if (IanaToWindowsMap.ContainsKey(timeZoneId)) return timeZoneId;
            if (WindowsToIanaMap.TryGetValue(timeZoneId, out var iana)) return iana;
            return timeZoneId;
        }
    }
}
