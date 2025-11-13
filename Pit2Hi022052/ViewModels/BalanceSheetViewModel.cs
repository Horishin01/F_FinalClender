using Pit2Hi022052.Models;
using System.Collections.Generic;
using System.Linq;

namespace Pit2Hi022052.ViewModels
{
    public class BalanceSheetViewModel
    {
        public IReadOnlyList<BalanceSheetItem> Assets { get; set; } = Array.Empty<BalanceSheetItem>();
        public IReadOnlyList<BalanceSheetItem> Liabilities { get; set; } = Array.Empty<BalanceSheetItem>();

        public decimal TotalAssets => Assets.Sum(a => a.Amount);
        public decimal TotalLiabilities => Liabilities.Sum(l => l.Amount);
        public decimal NetWorth => TotalAssets - TotalLiabilities;
    }
}
