using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase1Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeographicMarkets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CityName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MetroName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MarketCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeographicMarkets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResearchSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketBenchmarkObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: true),
                    IndustryCategory = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InventorySlotType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Metric = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NumericValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeLow = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeHigh = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    PublicationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RetrievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketBenchmarkObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketBenchmarkObservations_GeographicMarkets_GeographicMar~",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketBenchmarkObservations_ResearchSources_ResearchSourceId",
                        column: x => x.ResearchSourceId,
                        principalTable: "ResearchSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeographicMarkets_CountryCode",
                table: "GeographicMarkets",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_GeographicMarkets_MarketCode",
                table: "GeographicMarkets",
                column: "MarketCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketBenchmarkObservations_GeographicMarketId_Metric_Retri~",
                table: "MarketBenchmarkObservations",
                columns: new[] { "GeographicMarketId", "Metric", "RetrievedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketBenchmarkObservations_ResearchSourceId",
                table: "MarketBenchmarkObservations",
                column: "ResearchSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingModels_Code",
                table: "PricingModels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResearchSources_Name",
                table: "ResearchSources",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketBenchmarkObservations");

            migrationBuilder.DropTable(
                name: "PricingModels");

            migrationBuilder.DropTable(
                name: "GeographicMarkets");

            migrationBuilder.DropTable(
                name: "ResearchSources");
        }
    }
}
