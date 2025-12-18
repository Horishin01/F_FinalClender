// CalendarAuthDefaults
// 外部カレンダー連携で使う認証スキーム名と必要スコープの定数置き場。Program.cs で登録する際に参照される。

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
