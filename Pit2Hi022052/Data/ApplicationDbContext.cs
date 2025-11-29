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
    public virtual DbSet<CalendarCategory>? Categories { get; set; }
    public virtual DbSet<ExternalCalendarAccount>? ExternalCalendarAccounts { get; set; }
    public virtual DbSet<OutlookCalendarConnection> OutlookCalendarConnections { get; set; } = default!;
    public virtual DbSet<GoogleCalendarConnection> GoogleCalendarConnections { get; set; } = default!;
    public DbSet<UserAccessLog> UserAccessLogs { get; set; } = default!;
    public DbSet<AppNotice> AppNotices { get; set; } = default!;

    //--------
    // icouldプロパティ
    public DbSet<ICloudSetting> ICloudSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<UserAccessLog>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.AccessedAtUtc });
        });

        builder.Entity<AppNotice>(entity =>
        {
            entity.HasIndex(x => new { x.Kind, x.OccurredAt });
        });
    }

    //--------
    // END
    //--------
}

//==========================================================
// END
//==========================================================
