using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRequestIdAndImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "WaitingTimeHistoryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                CREATE TRIGGER "WaitingTimeHistoryEntries_PreventUpdate"
                BEFORE UPDATE ON "WaitingTimeHistoryEntries"
                BEGIN
                    SELECT RAISE(ABORT, 'Audit log entries cannot be modified.');
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "WaitingTimeHistoryEntries_PreventDelete"
                BEFORE DELETE ON "WaitingTimeHistoryEntries"
                BEGIN
                    SELECT RAISE(ABORT, 'Audit log entries cannot be deleted.');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"WaitingTimeHistoryEntries_PreventUpdate\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"WaitingTimeHistoryEntries_PreventDelete\";");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "WaitingTimeHistoryEntries");
        }
    }
}
