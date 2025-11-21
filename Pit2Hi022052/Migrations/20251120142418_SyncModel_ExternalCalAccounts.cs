using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pit2Hi022052.Migrations
{
    public partial class SyncModel_ExternalCalAccounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalCalendarAccounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    AccountEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AccessToken = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Scope = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalCalendarAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalCalendarAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarAccounts_UserId",
                table: "ExternalCalendarAccounts",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalCalendarAccounts");
        }
    }
}
