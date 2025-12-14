using Microsoft.EntityFrameworkCore.Migrations;

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
