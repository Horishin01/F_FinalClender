using System.ComponentModel.DataAnnotations;
using Pit2Hi022052.Models;

namespace Pit2Hi022052.ViewModels
{
    public class BalanceSheetEntryInputModel
    {
        [Display(Name = "区分")]
        [Required]
        public BalanceSheetEntryType Type { get; set; } = BalanceSheetEntryType.Asset;

        [Display(Name = "科目名")]
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "タグ")]
        [Required]
        public BalanceSheetTag Tag { get; set; } = BalanceSheetTag.CurrentAsset;

        [Display(Name = "金額")]
        [Range(0, 100000000000)]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }
    }
}
