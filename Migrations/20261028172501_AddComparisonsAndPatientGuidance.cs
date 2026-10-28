using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddComparisonsAndPatientGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComparisonCode",
                table: "WaitingTimes",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComparisonCode",
                table: "WaitingTimeHistoryEntries",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewContactGuidanceEn",
                table: "PortalSettingsAuditEntries",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewContactGuidanceFo",
                table: "PortalSettingsAuditEntries",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NewPublishPatientGuidance",
                table: "PortalSettingsAuditEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NewReferralInstructionsEn",
                table: "PortalSettingsAuditEntries",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewReferralInstructionsFo",
                table: "PortalSettingsAuditEntries",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldContactGuidanceEn",
                table: "PortalSettingsAuditEntries",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldContactGuidanceFo",
                table: "PortalSettingsAuditEntries",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OldPublishPatientGuidance",
                table: "PortalSettingsAuditEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OldReferralInstructionsEn",
                table: "PortalSettingsAuditEntries",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldReferralInstructionsFo",
                table: "PortalSettingsAuditEntries",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactGuidanceEn",
                table: "PortalSettings",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactGuidanceFo",
                table: "PortalSettings",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublishPatientGuidance",
                table: "PortalSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferralInstructionsEn",
                table: "PortalSettings",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralInstructionsFo",
                table: "PortalSettings",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComparisonCode",
                table: "WaitingTimes");

            migrationBuilder.DropColumn(
                name: "ComparisonCode",
                table: "WaitingTimeHistoryEntries");

            migrationBuilder.DropColumn(
                name: "NewContactGuidanceEn",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "NewContactGuidanceFo",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "NewPublishPatientGuidance",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "NewReferralInstructionsEn",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "NewReferralInstructionsFo",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "OldContactGuidanceEn",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "OldContactGuidanceFo",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "OldPublishPatientGuidance",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "OldReferralInstructionsEn",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "OldReferralInstructionsFo",
                table: "PortalSettingsAuditEntries");

            migrationBuilder.DropColumn(
                name: "ContactGuidanceEn",
                table: "PortalSettings");

            migrationBuilder.DropColumn(
                name: "ContactGuidanceFo",
                table: "PortalSettings");

            migrationBuilder.DropColumn(
                name: "PublishPatientGuidance",
                table: "PortalSettings");

            migrationBuilder.DropColumn(
                name: "ReferralInstructionsEn",
                table: "PortalSettings");

            migrationBuilder.DropColumn(
                name: "ReferralInstructionsFo",
                table: "PortalSettings");
        }
    }
}
