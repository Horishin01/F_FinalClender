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
    public DbSet<BalanceSheetEntry> BalanceSheetEntries { get; set; } = default!;

    //--------
    // icouldプロパティ
    public DbSet<ICloudSetting> ICloudSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BalanceSheetEntry>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.AsOfDate });
        });
    }

    //--------
    // END
    //--------
}

//==========================================================
// END
//==========================================================
