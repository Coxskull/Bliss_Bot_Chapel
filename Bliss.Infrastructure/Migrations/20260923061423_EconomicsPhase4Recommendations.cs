using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase4Recommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingRuleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DocumentJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRuleVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RateRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdInventorySlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvertiserOpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    PricingModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    PricingRuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndustryCategory = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CampaignObjective = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RangeLow = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RangeTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RangeHigh = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedImpressions = table.Column<int>(type: "integer", nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BenchmarkAsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateRecommendations_AdInventorySlots_AdInventorySlotId",
                        column: x => x.AdInventorySlotId,
                        principalTable: "AdInventorySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendations_AdvertiserOpportunities_AdvertiserOppor~",
                        column: x => x.AdvertiserOpportunityId,
                        principalTable: "AdvertiserOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendations_BlissMatches_BlissMatchId",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendations_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendations_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendations_GeographicMarkets_GeographicMarketId",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendations_PricingModels_PricingModelId",
                        column: x => x.PricingModelId,
                        principalTable: "PricingModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendations_PricingRuleVersions_PricingRuleVersionId",
                        column: x => x.PricingRuleVersionId,
                        principalTable: "PricingRuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RateRecommendationFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RateRecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NumericValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AdjustmentMultiplier = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateRecommendationFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateRecommendationFactors_RateRecommendations_RateRecommend~",
                        column: x => x.RateRecommendationId,
                        principalTable: "RateRecommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RateRecommendationSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RateRecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    InventoryRateBenchmarkId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorAudienceSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorPerformanceSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateRecommendationSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateRecommendationSources_CreatorAudienceSnapshots_CreatorA~",
                        column: x => x.CreatorAudienceSnapshotId,
                        principalTable: "CreatorAudienceSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendationSources_CreatorPerformanceSnapshots_Creat~",
                        column: x => x.CreatorPerformanceSnapshotId,
                        principalTable: "CreatorPerformanceSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendationSources_InventoryRateBenchmarks_Inventory~",
                        column: x => x.InventoryRateBenchmarkId,
                        principalTable: "InventoryRateBenchmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendationSources_RateRecommendations_RateRecommend~",
                        column: x => x.RateRecommendationId,
                        principalTable: "RateRecommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RateRecommendationSources_ResearchSources_ResearchSourceId",
                        column: x => x.ResearchSourceId,
                        principalTable: "ResearchSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRuleVersions_Version",
                table: "PricingRuleVersions",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendationFactors_RateRecommendationId_SortOrder",
                table: "RateRecommendationFactors",
                columns: new[] { "RateRecommendationId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_AdInventorySlotId",
                table: "RateRecommendations",
                column: "AdInventorySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_AdvertiserOpportunityId",
                table: "RateRecommendations",
                column: "AdvertiserOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_BlissMatchId",
                table: "RateRecommendations",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_ContentItemId",
                table: "RateRecommendations",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_CreatedAt",
                table: "RateRecommendations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_CreatorId",
                table: "RateRecommendations",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_GeographicMarketId",
                table: "RateRecommendations",
                column: "GeographicMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_PricingModelId",
                table: "RateRecommendations",
                column: "PricingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_PricingRuleVersionId",
                table: "RateRecommendations",
                column: "PricingRuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendations_SourceSystem_IdempotencyKey",
                table: "RateRecommendations",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendationSources_CreatorAudienceSnapshotId",
                table: "RateRecommendationSources",
                column: "CreatorAudienceSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendationSources_CreatorPerformanceSnapshotId",
                table: "RateRecommendationSources",
                column: "CreatorPerformanceSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendationSources_InventoryRateBenchmarkId",
                table: "RateRecommendationSources",
                column: "InventoryRateBenchmarkId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendationSources_RateRecommendationId",
                table: "RateRecommendationSources",
                column: "RateRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_RateRecommendationSources_ResearchSourceId",
                table: "RateRecommendationSources",
                column: "ResearchSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RateRecommendationFactors");

            migrationBuilder.DropTable(
                name: "RateRecommendationSources");

            migrationBuilder.DropTable(
                name: "RateRecommendations");

            migrationBuilder.DropTable(
                name: "PricingRuleVersions");
        }
    }
}
