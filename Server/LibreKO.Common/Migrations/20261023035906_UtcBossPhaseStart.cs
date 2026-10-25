using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibreKO.Common.Migrations
{
    /// <inheritdoc />
    public partial class UtcBossPhaseStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `NpcPositions` SET `NpcId` = 9504 WHERE `ZoneId` = 86 AND `NpcId` = 9507;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `NpcPositions` SET `NpcId` = 9507 WHERE `ZoneId` = 86 AND `NpcId` = 9504;");
        }
    }
}
