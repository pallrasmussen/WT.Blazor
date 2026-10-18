using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalSettingsAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalSettingsAuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OldGreenMaximumAgeMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    OldYellowMaximumAgeMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    OldLongWaitMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    NewGreenMaximumAgeMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    NewYellowMaximumAgeMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    NewLongWaitMonths = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalSettingsAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalSettingsAuditEntries_Timestamp",
                table: "PortalSettingsAuditEntries",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalSettingsAuditEntries");
        }
    }
}
