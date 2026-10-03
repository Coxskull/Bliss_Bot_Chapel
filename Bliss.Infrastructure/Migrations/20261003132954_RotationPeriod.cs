using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RotationPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RotationPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TheoreticalSlots = table.Column<int>(type: "integer", nullable: false),
                    PlacedAdvertisers = table.Column<int>(type: "integer", nullable: false),
                    OpenSlots = table.Column<int>(type: "integer", nullable: false),
                    SlotCount = table.Column<int>(type: "integer", nullable: false),
                    CreatorApproved = table.Column<bool>(type: "boolean", nullable: false),
                    RevenueLine = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CensusClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    SlotsChanged = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotationPeriods", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RotationPeriods_PeriodKey",
                table: "RotationPeriods",
                column: "PeriodKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RotationPeriods");
        }
    }
}
