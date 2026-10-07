using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreativeAcceptanceVoyage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreativeAcceptanceVoyages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoyageKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AdvertiserName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Market = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Niche = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InventoryProductId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReportJson = table.Column<string>(type: "jsonb", nullable: false),
                    ModelCalls = table.Column<int>(type: "integer", nullable: false),
                    CampaignReady = table.Column<bool>(type: "boolean", nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreativeAcceptanceVoyages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreativeAcceptanceVoyages_RecordedAt",
                table: "CreativeAcceptanceVoyages",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeAcceptanceVoyages_VoyageKey",
                table: "CreativeAcceptanceVoyages",
                column: "VoyageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreativeAcceptanceVoyages");
        }
    }
}
