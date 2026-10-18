using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitingTimeConcurrencyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "WaitingTimes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "WaitingTimes");
        }
    }
}
