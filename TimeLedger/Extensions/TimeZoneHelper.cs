using System;
using System.Runtime.InteropServices;

namespace TimeLedger.Extensions
{
    public static class TimeZoneHelper
    {
        private static readonly Lazy<TimeZoneInfo> JapanTimeZone = new Lazy<TimeZoneInfo>(() =>
        {
            var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[] { "Tokyo Standard Time", "Asia/Tokyo" }
                : new[] { "Asia/Tokyo", "Tokyo Standard Time" };

            foreach (var id in candidates)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Utc;
        });

        public static DateTime? ToJapanTime(this DateTime? utcValue)
        {
            if (utcValue == null) return null;
            var utc = DateTime.SpecifyKind(utcValue.Value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, JapanTimeZone.Value);
        }

        public static string ToJapanTimeString(this DateTime? utcValue, string format)
        {
            var jst = utcValue.ToJapanTime();
            return jst.HasValue ? jst.Value.ToString(format) : string.Empty;
        }
    }
}
