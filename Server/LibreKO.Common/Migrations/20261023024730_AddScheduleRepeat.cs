using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibreKO.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleRepeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "TempleEventSchedules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DayOfMonth",
                table: "TempleEventSchedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Repeat",
                table: "TempleEventSchedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "LotterySchedules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DayOfMonth",
                table: "LotterySchedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Repeat",
                table: "LotterySchedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "CollectionRaceSchedules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DayOfMonth",
                table: "CollectionRaceSchedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Repeat",
                table: "CollectionRaceSchedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            foreach (var table in new[] { "TempleEventSchedules", "LotterySchedules", "CollectionRaceSchedules" })
                migrationBuilder.Sql($"UPDATE `{table}` SET `Repeat` = 1 WHERE `Day` IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "EventStarts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Repeat = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Day = table.Column<int>(type: "int", nullable: true),
                    DayOfMonth = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Hour = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Minute = table.Column<byte>(type: "tinyint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStarts", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EventStarts_Kind",
                table: "EventStarts",
                column: "Kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventStarts");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "TempleEventSchedules");

            migrationBuilder.DropColumn(
                name: "DayOfMonth",
                table: "TempleEventSchedules");

            migrationBuilder.DropColumn(
                name: "Repeat",
                table: "TempleEventSchedules");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "LotterySchedules");

            migrationBuilder.DropColumn(
                name: "DayOfMonth",
                table: "LotterySchedules");

            migrationBuilder.DropColumn(
                name: "Repeat",
                table: "LotterySchedules");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "CollectionRaceSchedules");

            migrationBuilder.DropColumn(
                name: "DayOfMonth",
                table: "CollectionRaceSchedules");

            migrationBuilder.DropColumn(
                name: "Repeat",
                table: "CollectionRaceSchedules");
        }
    }
}
