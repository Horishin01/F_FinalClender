// EF Core Migration: AddRecurrenceExceptionsFix
// 自動生成されたマイグレーション。再帰例外の処理を補正したスキーマ変更で、手動編集は非推奨。

﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeLedger.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrenceExceptionsFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecurrenceExceptions",
                table: "Events",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurrenceExceptions",
                table: "Events");
        }
    }
}
