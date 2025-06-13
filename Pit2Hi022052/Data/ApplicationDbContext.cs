/*----------------------------------------------------------
 ApplicationDbContext.cs
----------------------------------------------------------*/
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pit2Hi022052.Models;
namespace Pit2Hi022052.Data;
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

    //--------
    // テーブルプロパティ
    //カレンダー
    public virtual DbSet<Event>? Events { get; set; }
    public virtual DbSet<ICCard>? ICCards { get; set; }


    //--------
    // END
    //--------
}

//==========================================================
// END
//==========================================================