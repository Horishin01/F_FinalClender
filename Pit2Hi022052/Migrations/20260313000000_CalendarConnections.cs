using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pit2Hi022052.Migrations
{
    public partial class CalendarConnections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleCalendarConnections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AccessTokenEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RefreshTokenEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Scope = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleCalendarConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleCalendarConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutlookCalendarConnections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AccessTokenEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RefreshTokenEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Scope = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutlookCalendarConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutlookCalendarConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarConnections_UserId",
                table: "GoogleCalendarConnections",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutlookCalendarConnections_UserId",
                table: "OutlookCalendarConnections",
                column: "UserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleCalendarConnections");

            migrationBuilder.DropTable(
                name: "OutlookCalendarConnections");
        }
    }
}
