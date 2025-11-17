using System.ComponentModel.DataAnnotations;

namespace Pit2Hi022052.Models
{
    public enum BalanceSheetCategory
    {
        Asset,
        Liability
    }

    public class BalanceSheetItem
    {
        [Required]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Range(-1000000000, 1000000000)]
        public decimal Amount { get; set; }

        public BalanceSheetCategory Category { get; set; } = BalanceSheetCategory.Asset;

        [MaxLength(60)]
        public string Tag { get; set; } = string.Empty;
    }
}
