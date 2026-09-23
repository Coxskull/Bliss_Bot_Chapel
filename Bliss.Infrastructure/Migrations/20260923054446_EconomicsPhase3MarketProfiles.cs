using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase3MarketProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExchangeRateObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    BaseCurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    QuoteCurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetrievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRateObservations_ResearchSources_ResearchSourceId",
                        column: x => x.ResearchSourceId,
                        principalTable: "ResearchSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndustryEconomicProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResearchSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    AcquisitionCostLow = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AcquisitionCostHigh = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndustryEconomicProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndustryEconomicProfiles_GeographicMarkets_GeographicMarket~",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IndustryEconomicProfiles_ResearchSources_ResearchSourceId",
                        column: x => x.ResearchSourceId,
                        principalTable: "ResearchSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryRateBenchmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: true),
                    PricingModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    InventorySlotType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContentFormat = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DurationSecondsLow = table.Column<int>(type: "integer", nullable: true),
                    DurationSecondsHigh = table.Column<int>(type: "integer", nullable: true),
                    RangeLow = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeHigh = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryRateBenchmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryRateBenchmarks_GeographicMarkets_GeographicMarketId",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryRateBenchmarks_PricingModels_PricingModelId",
                        column: x => x.PricingModelId,
                        principalTable: "PricingModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryRateBenchmarks_ResearchSources_ResearchSourceId",
                        column: x => x.ResearchSourceId,
                        principalTable: "ResearchSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketEconomicProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PurchasingPowerIndex = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CompetitionLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AudienceScarcityLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEconomicProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketEconomicProfiles_GeographicMarkets_GeographicMarketId",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketEconomicProfiles_ResearchSources_ResearchSourceId",
                        column: x => x.ResearchSourceId,
                        principalTable: "ResearchSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateObservations_BaseCurrencyCode_QuoteCurrencyCode~",
                table: "ExchangeRateObservations",
                columns: new[] { "BaseCurrencyCode", "QuoteCurrencyCode", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateObservations_ResearchSourceId",
                table: "ExchangeRateObservations",
                column: "ResearchSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_IndustryEconomicProfiles_Category_GeographicMarketId_Version",
                table: "IndustryEconomicProfiles",
                columns: new[] { "Category", "GeographicMarketId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndustryEconomicProfiles_GeographicMarketId",
                table: "IndustryEconomicProfiles",
                column: "GeographicMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_IndustryEconomicProfiles_ResearchSourceId",
                table: "IndustryEconomicProfiles",
                column: "ResearchSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRateBenchmarks_GeographicMarketId_InventorySlotTyp~",
                table: "InventoryRateBenchmarks",
                columns: new[] { "GeographicMarketId", "InventorySlotType", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRateBenchmarks_PricingModelId",
                table: "InventoryRateBenchmarks",
                column: "PricingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRateBenchmarks_ResearchSourceId",
                table: "InventoryRateBenchmarks",
                column: "ResearchSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketEconomicProfiles_GeographicMarketId_Version",
                table: "MarketEconomicProfiles",
                columns: new[] { "GeographicMarketId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketEconomicProfiles_ResearchSourceId",
                table: "MarketEconomicProfiles",
                column: "ResearchSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRateObservations");

            migrationBuilder.DropTable(
                name: "IndustryEconomicProfiles");

            migrationBuilder.DropTable(
                name: "InventoryRateBenchmarks");

            migrationBuilder.DropTable(
                name: "MarketEconomicProfiles");
        }
    }
}
