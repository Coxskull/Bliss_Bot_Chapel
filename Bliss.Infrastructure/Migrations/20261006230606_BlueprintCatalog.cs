using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BlueprintCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Intent = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Reply = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ProductIds = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ShowcaseIds = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ShowcaseDisplayed = table.Column<bool>(type: "boolean", nullable: false),
                    HumanEscalation = table.Column<bool>(type: "boolean", nullable: false),
                    InventedProduct = table.Column<bool>(type: "boolean", nullable: false),
                    InventedPrice = table.Column<bool>(type: "boolean", nullable: false),
                    ModelCalls = table.Column<int>(type: "integer", nullable: false),
                    CampaignReady = table.Column<bool>(type: "boolean", nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RealEstateProducts",
                columns: table => new
                {
                    ProductId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Tier = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Exclusivity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MaximumAdvertisers = table.Column<int>(type: "integer", nullable: false),
                    SlotIds = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ShowcaseId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Lifecycle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccupancyStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeviceStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PlatformStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealEstateProducts", x => x.ProductId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogConversations_RecordedAt",
                table: "CatalogConversations",
                column: "RecordedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogConversations");

            migrationBuilder.DropTable(
                name: "RealEstateProducts");
        }
    }
}
