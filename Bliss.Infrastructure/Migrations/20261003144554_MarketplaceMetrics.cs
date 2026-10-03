using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MarketplaceMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketplaceMetricReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AdvertiserCount = table.Column<int>(type: "integer", nullable: false),
                    CreatorCount = table.Column<int>(type: "integer", nullable: false),
                    SlotCount = table.Column<int>(type: "integer", nullable: false),
                    RevenueRowCount = table.Column<int>(type: "integer", nullable: false),
                    Pressure = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RevenueLine = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CensusClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    RevenueRecorded = table.Column<bool>(type: "boolean", nullable: false),
                    SlotsChanged = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceMetricReadings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceMetricReadings_MetricKey",
                table: "MarketplaceMetricReadings",
                column: "MetricKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceMetricReadings");
        }
    }
}
