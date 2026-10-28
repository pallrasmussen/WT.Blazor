using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimateExpiryAndRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WaitUpperValue",
                table: "WaitingTimes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewWaitUpperValue",
                table: "WaitingTimeHistoryEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OldWaitUpperValue",
                table: "WaitingTimeHistoryEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewEstimateExpiryMonths",
                table: "PortalSettingsAuditEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OldEstimateExpiryMonths",
                table: "PortalSettingsAuditEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EstimateExpiryMonths",
                table: "PortalSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 12);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WaitUpperValue",
                table: "WaitingTimes");

            migrationBuilder.DropColumn(
                name: "NewWaitUpperValue",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropColumn(
                name: "OldWaitUpperValue",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropColumn(
                name: "NewEstimateExpiryMonths",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "OldEstimateExpiryMonths",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "EstimateExpiryMonths",
                table: "PortalSettings");
        }
    }
}
