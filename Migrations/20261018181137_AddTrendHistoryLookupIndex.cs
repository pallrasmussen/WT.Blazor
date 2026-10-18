using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddTrendHistoryLookupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaitingTimeHistoryEntries_WaitingTimeId",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingTimeHistoryEntries_WaitingTimeId_Action_Timestamp",
                table: "WaitingTimeHistoryEntries",
                columns: new[] { "WaitingTimeId", "Action", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaitingTimeHistoryEntries_WaitingTimeId_Action_Timestamp",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingTimeHistoryEntries_WaitingTimeId",
                table: "WaitingTimeHistoryEntries",
                column: "WaitingTimeId");
        }
    }
}
