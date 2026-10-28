using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralEstimateDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimateSeries",
                table: "WaitingTimes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "EstimateStatus",
                table: "WaitingTimes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReferralEndpoint",
                table: "WaitingTimes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleDepartment",
                table: "WaitingTimes",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "WaitingTimes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimateSeries",
                table: "WaitingTimeHistoryEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "EstimateStatus",
                table: "WaitingTimeHistoryEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReferralEndpoint",
                table: "WaitingTimeHistoryEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleDepartment",
                table: "WaitingTimeHistoryEntries",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
            // A metadata edit is not a verification. Backfill only from recorded
            // creation, estimate-change and review events; leave missing evidence null.
            migrationBuilder.Sql("""
                UPDATE WaitingTimes
                SET VerifiedAt = (
                    SELECT MAX(Timestamp) FROM WaitingTimeHistoryEntries h
                    WHERE h.WaitingTimeId = WaitingTimes.Id AND h.Action IN (0, 1, 3)
                        AND h.NewWaitValue IS NOT NULL AND datetime(h.Timestamp) <= datetime('now')
                        AND coalesce(h.Username, '') <> 'demo' AND coalesce(h.Detail, '') <> 'Demo'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimateSeries",
                table: "WaitingTimes");

            migrationBuilder.DropColumn(
                name: "EstimateStatus",
                table: "WaitingTimes");

            migrationBuilder.DropColumn(
                name: "ReferralEndpoint",
                table: "WaitingTimes");

            migrationBuilder.DropColumn(
                name: "ResponsibleDepartment",
                table: "WaitingTimes");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "WaitingTimes");

            migrationBuilder.DropColumn(
                name: "EstimateSeries",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropColumn(
                name: "EstimateStatus",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropColumn(
                name: "ReferralEndpoint",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropColumn(
                name: "ResponsibleDepartment",
                table: "WaitingTimeHistoryEntries");
        }
    }
}
