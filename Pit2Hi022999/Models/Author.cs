using System.ComponentModel.DataAnnotations.Schema;

namespace Pit2Hi022999.Models
{
    public class Author
    {
        public virtual string Id { get; set; } = string.Empty; 
        public virtual string Name { get; set; } = string.Empty;

        [InverseProperty(nameof(TextbookAuthor.Author))]
        public virtual List<TextbookAuthor>? TextbookAuthors { get; set; }

    }
}
