namespace TimeLedger.Extensions
{
    public static class CalendarAuthDefaults
    {
        public const string OutlookScheme = "OutlookCalendar";
        public const string GoogleScheme = "GoogleCalendar";

        public static readonly string[] OutlookScopes = new[]
        {
            "offline_access",
            "Calendars.ReadWrite",
            "User.Read"
        };

        public static readonly string[] GoogleScopes = new[]
        {
            "openid",
            "email",
            "profile",
            "https://www.googleapis.com/auth/calendar"
        };
    }
}
