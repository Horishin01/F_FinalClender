using System.ComponentModel.DataAnnotations.Schema;

namespace Pit2Hi022999.Models
{
    public class TextbookAuthor
    {
        //---------
        // フィールドプロパティ

        public virtual string TextbookId { get; set; } = string.Empty;
        public virtual string AuthorId { get; set; } = string.Empty;


        //---------
        // ナビゲーションプロパティ

        [ForeignKey(nameof(TextbookId))]
        public virtual Textbook? Textbook { get; set; }

        [ForeignKey(nameof(AuthorId))]
        public virtual Author? Author { get; set; }

    }
}
