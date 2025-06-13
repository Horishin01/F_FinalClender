/*----------------------------------------------------------
 Textbook.cs
 ----------------------------------------------------------*/

 using System.ComponentModel.DataAnnotations.Schema;
 namespace Pit2Hi022999.Models;

//==========================================================
// Textbook クラス

public class Textbook
 {
 //--------
 // フィールドプロパティ

 public virtual string Id { get; set; } = string.Empty;
 public virtual string Name { get; set; } = string.Empty;
 public virtual uint Price { get; set; }
 public virtual string PublisherId { get; set; } = string.Empty;

 //--------
 // ナビゲーションプロパティ

 [ForeignKey(nameof(PublisherId))]
 public virtual Publisher? Publisher { get; set; }

    [InverseProperty(nameof(TextbookAuthor.Textbook))]
    public virtual List<TextbookAuthor>? TextbookAuthors { get; set; }
    //--------
    // END
    //--------

}

//==========================================================
// END
//==========================================================
