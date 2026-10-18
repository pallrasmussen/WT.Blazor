using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WT.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaitingTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Hospital = table.Column<int>(type: "INTEGER", nullable: false),
                    Department = table.Column<int>(type: "INTEGER", nullable: true),
                    WaitValue = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitingTimes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaitingTimeHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WaitingTimeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Hospital = table.Column<int>(type: "INTEGER", nullable: false),
                    Department = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: true),
                    OldWaitValue = table.Column<int>(type: "INTEGER", nullable: true),
                    OldUnit = table.Column<int>(type: "INTEGER", nullable: true),
                    NewWaitValue = table.Column<int>(type: "INTEGER", nullable: true),
                    NewUnit = table.Column<int>(type: "INTEGER", nullable: true),
                    Username = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitingTimeHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitingTimeHistoryEntries_WaitingTimes_WaitingTimeId",
                        column: x => x.WaitingTimeId,
                        principalTable: "WaitingTimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingTimeHistoryEntries_WaitingTimeId",
                table: "WaitingTimeHistoryEntries",
                column: "WaitingTimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitingTimeHistoryEntries");

            migrationBuilder.DropTable(
                name: "WaitingTimes");
        }
    }
}
