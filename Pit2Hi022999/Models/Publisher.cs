/*----------------------------------------------------------
 Publisher.cs
----------------------------------------------------------*/
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace Pit2Hi022999.Models;
//==========================================================
// Publisher クラス
public class Publisher
{
    //--------
    // フィールドプロパティ
    public virtual string Id { get; set; } = string.Empty;
    public virtual string Name { get; set; } = string.Empty;
    public virtual string Address { get; set; } = string.Empty;

    public virtual string IdName => $"{Id} {Name}";

    [NotMapped]
    [Display(Name = "書籍出版数")]
    public int? TextbooksCount => Textbooks?.Count;

    //NULL許容型（無回答で良い）
    //public virtual string? Address { get; set; };


    //--------
    // インバースナビゲーションプロパティ
    [InverseProperty(nameof(Textbook.Publisher))]
    public virtual List<Textbook>? Textbooks { get; set; }
    //--------
    // END
    //--------
}
//==========================================================
// END
//==========================================================