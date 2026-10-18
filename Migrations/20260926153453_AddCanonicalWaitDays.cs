using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalWaitDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewWaitDays",
                table: "WaitingTimeHistoryEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OldWaitDays",
                table: "WaitingTimeHistoryEntries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewWaitDays",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropColumn(
                name: "OldWaitDays",
                table: "WaitingTimeHistoryEntries");
        }
    }
}
