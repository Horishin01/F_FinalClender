using Pit2Hi022052.Models;

namespace Pit2Hi022052.Services
{
    public static class BalanceSheetSampleProvider
    {
        public static IReadOnlyList<BalanceSheetItem> GetSeed()
        {
            var items = new List<BalanceSheetItem>
            {
                new BalanceSheetItem { Id = "cash", Name = "現金・預金", Amount = 1250000m, Category = BalanceSheetCategory.Asset, Tag = "流動資産" },
                new BalanceSheetItem { Id = "securities", Name = "投資信託・株式", Amount = 1850000m, Category = BalanceSheetCategory.Asset, Tag = "流動資産" },
                new BalanceSheetItem { Id = "pension", Name = "iDeCo / DC", Amount = 780000m, Category = BalanceSheetCategory.Asset, Tag = "年金" },
                new BalanceSheetItem { Id = "property", Name = "不動産 (持家)", Amount = 3200000m, Category = BalanceSheetCategory.Asset, Tag = "固定資産" },
                new BalanceSheetItem { Id = "mortgage", Name = "住宅ローン残高", Amount = 2400000m, Category = BalanceSheetCategory.Liability, Tag = "長期借入" },
                new BalanceSheetItem { Id = "card", Name = "クレジット支払予定", Amount = 120000m, Category = BalanceSheetCategory.Liability, Tag = "短期負債" },
                new BalanceSheetItem { Id = "education", Name = "教育ローン", Amount = 380000m, Category = BalanceSheetCategory.Liability, Tag = "中期負債" }
            };

            return items;
        }
    }
}
