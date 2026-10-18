using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WaitingTimes_IsActive_Hospital_Department",
                table: "WaitingTimes",
                columns: new[] { "IsActive", "Hospital", "Department" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingTimeHistoryEntries_Action_Timestamp",
                table: "WaitingTimeHistoryEntries",
                columns: new[] { "Action", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingTimeHistoryEntries_Hospital_Department_Timestamp",
                table: "WaitingTimeHistoryEntries",
                columns: new[] { "Hospital", "Department", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingTimeHistoryEntries_Timestamp",
                table: "WaitingTimeHistoryEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingTimeHistoryEntries_Username_Timestamp",
                table: "WaitingTimeHistoryEntries",
                columns: new[] { "Username", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaitingTimes_IsActive_Hospital_Department",
                table: "WaitingTimes");

            migrationBuilder.DropIndex(
                name: "IX_WaitingTimeHistoryEntries_Action_Timestamp",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitingTimeHistoryEntries_Hospital_Department_Timestamp",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitingTimeHistoryEntries_Timestamp",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitingTimeHistoryEntries_Username_Timestamp",
                table: "WaitingTimeHistoryEntries");
        }
    }
}
