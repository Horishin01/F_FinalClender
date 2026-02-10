namespace TimeLedger.Models
{
    public class CalendarSettings
    {
        public string DefaultTimeZoneId { get; set; } = "Asia/Tokyo";
        public string? ClientTimeZoneId { get; set; }
        public string[] SupportedTimeZoneIds { get; set; } = new[] { "Asia/Tokyo" };
    }
}
