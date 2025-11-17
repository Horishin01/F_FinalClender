namespace Pit2Hi022052.ViewModels
{
    public class BalanceSheetPageViewModel
    {
        public BalanceSheetSummary Summary { get; set; } = BalanceSheetSummary.Empty;
        public BalanceSheetEntryInputModel EntryInput { get; set; } = new BalanceSheetEntryInputModel();
    }
}
