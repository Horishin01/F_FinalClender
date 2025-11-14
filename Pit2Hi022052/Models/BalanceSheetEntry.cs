using System.ComponentModel.DataAnnotations;

namespace Pit2Hi022052.Models
{
    public enum BalanceSheetEntryType
    {
        [Display(Name = "資産")]
        Asset = 1,

        [Display(Name = "負債")]
        Liability = 2
    }

    public enum BalanceSheetTag
    {
        [Display(Name = "流動資産")]
        CurrentAsset,

        [Display(Name = "固定資産")]
        NonCurrentAsset,

        [Display(Name = "流動負債")]
        CurrentLiability,

        [Display(Name = "固定負債")]
        NonCurrentLiability
    }

    public class BalanceSheetEntry
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = default!;

        [DataType(DataType.Date)]
        public DateTime AsOfDate { get; set; }

        [Display(Name = "区分")]
        [Required]
        public BalanceSheetEntryType Type { get; set; }

        [Display(Name = "科目名")]
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "タグ")]
        public BalanceSheetTag? Tag { get; set; }

        [Display(Name = "金額")]
        [Range(0, 100000000000)]
        public decimal Amount { get; set; }

        public bool IsDeleted { get; set; }
    }
}
