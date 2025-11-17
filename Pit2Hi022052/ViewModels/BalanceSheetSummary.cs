using Pit2Hi022052.Models;

namespace Pit2Hi022052.ViewModels
{
    public class BalanceSheetSummary
    {
        public static BalanceSheetSummary Empty { get; } = new BalanceSheetSummary();

        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public DateTime? AsOfDate { get; set; }

        public decimal NetWorth => TotalAssets - TotalLiabilities;
        public decimal? DebtRatio => TotalAssets == 0 ? (decimal?)null : TotalLiabilities / TotalAssets;

        public IReadOnlyList<BalanceSheetEntry> Assets { get; set; } = Array.Empty<BalanceSheetEntry>();
        public IReadOnlyList<BalanceSheetEntry> Liabilities { get; set; } = Array.Empty<BalanceSheetEntry>();
    }
}
