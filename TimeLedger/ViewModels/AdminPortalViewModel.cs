namespace TimeLedger.ViewModels
{
    public class AdminPortalViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalRoles { get; set; }
        public int EventsLast30Days { get; set; }
        public int Categories { get; set; }
        public int AccessLogsLast7Days { get; set; }
        public int OutlookConnections { get; set; }
        public int GoogleConnections { get; set; }
        public int ExternalLegacyAccounts { get; set; }
    }
}
