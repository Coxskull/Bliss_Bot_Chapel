using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoverageWeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverageWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    QualifiedSlices = table.Column<int>(type: "integer", nullable: false),
                    MarketCount = table.Column<int>(type: "integer", nullable: false),
                    MarketLine = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FuelStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CensusClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    SlicesChanged = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageWeeks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverageWeeks_WeekKey",
                table: "CoverageWeeks",
                column: "WeekKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverageWeeks");
        }
    }
}
