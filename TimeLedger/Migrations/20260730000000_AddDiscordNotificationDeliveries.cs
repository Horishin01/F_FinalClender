using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TimeLedger.Data;

#nullable disable

namespace TimeLedger.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260730000000_AddDiscordNotificationDeliveries")]
public partial class AddDiscordNotificationDeliveries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DiscordNotificationDeliveries",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                EventId = table.Column<string>(type: "text", nullable: false),
                UserId = table.Column<string>(type: "text", nullable: false),
                NotificationKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DiscordNotificationDeliveries", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_DiscordNotificationDeliveries_EventId_NotificationKind_ScheduledAtUtc",
            table: "DiscordNotificationDeliveries",
            columns: new[] { "EventId", "NotificationKind", "ScheduledAtUtc" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DiscordNotificationDeliveries");
    }
}
