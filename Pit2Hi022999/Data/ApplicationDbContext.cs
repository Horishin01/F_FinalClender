/*----------------------------------------------------------
 ApplicationDbContext.cs
----------------------------------------------------------*/
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022999.Models;
namespace Pit2Hi022999.Data;
//==========================================================
// ApplicationDbContext クラス
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    //--------
    // 基幹処理
    public ApplicationDbContext
    (DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        return;
    }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // -- 基底処理 -- 

        base.OnModelCreating(builder);
        // -- TextbookAuthor の主キー設定 -- 

        //複合主キーなので設定
        builder.Entity<TextbookAuthor>()
         .HasKey(e => new { e.TextbookId, e.AuthorId });
    }
    //--------
    // テーブルプロパティ
    public virtual DbSet<Publisher>? Publishers { get; set; }
    public virtual DbSet<Textbook>? Textbooks { get; set; }
    public virtual DbSet<Author>? Authors { get; set; }
    public virtual DbSet<TextbookAuthor>? TextbookAuthors { get; set; }
    //カレンダー
    public virtual DbSet<Event>? Events { get; set; }

    //--------
    // END
    //--------
}

//==========================================================
// END
//==========================================================