using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase9HistoricalLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignPerformanceEconomics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupersedesCampaignPerformanceEconomicsId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActualImpressions = table.Column<long>(type: "bigint", nullable: true),
                    ActualViews = table.Column<long>(type: "bigint", nullable: true),
                    ActualListens = table.Column<long>(type: "bigint", nullable: true),
                    ActualEngagements = table.Column<long>(type: "bigint", nullable: true),
                    EngagementRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Conversions = table.Column<long>(type: "bigint", nullable: true),
                    ConversionValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ConversionValueCurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    AlphaPlacementCount = table.Column<int>(type: "integer", nullable: false),
                    AlphaContractedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AlphaContractedCurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    EffectiveCpm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    EffectiveCpv = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    MeasurementAsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPerformanceEconomics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignPerformanceEconomics_CampaignPerformanceEconomics_S~",
                        column: x => x.SupersedesCampaignPerformanceEconomicsId,
                        principalTable: "CampaignPerformanceEconomics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPerformanceEconomics_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalPlacementEconomics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteOutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteLineItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateRecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompensationIllustrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersedesHistoricalPlacementEconomicsId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuotedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ContractedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ActualImpressions = table.Column<long>(type: "bigint", nullable: true),
                    ActualViews = table.Column<long>(type: "bigint", nullable: true),
                    ActualListens = table.Column<long>(type: "bigint", nullable: true),
                    ActualEngagements = table.Column<long>(type: "bigint", nullable: true),
                    ActualConversions = table.Column<long>(type: "bigint", nullable: true),
                    RecommendationLow = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendationTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendationHigh = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExternalBenchmarkLow = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExternalBenchmarkHigh = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EffectiveCpm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    EffectiveCpv = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ContractedVsRecommendationTargetPercentage = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ContractedVsExternalMidpointPercentage = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AlphaCompensationAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatorCompensationAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherCompensationAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MeasurementAsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalPlacementEconomics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalPlacementEconomics_CampaignPlacements_CampaignPla~",
                        column: x => x.CampaignPlacementId,
                        principalTable: "CampaignPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalPlacementEconomics_CompensationIllustrations_Comp~",
                        column: x => x.CompensationIllustrationId,
                        principalTable: "CompensationIllustrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalPlacementEconomics_HistoricalPlacementEconomics_S~",
                        column: x => x.SupersedesHistoricalPlacementEconomicsId,
                        principalTable: "HistoricalPlacementEconomics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalPlacementEconomics_QuoteLineItems_QuoteLineItemId",
                        column: x => x.QuoteLineItemId,
                        principalTable: "QuoteLineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalPlacementEconomics_QuoteOutcomes_QuoteOutcomeId",
                        column: x => x.QuoteOutcomeId,
                        principalTable: "QuoteOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalPlacementEconomics_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalPlacementEconomics_RateRecommendations_RateRecomm~",
                        column: x => x.RateRecommendationId,
                        principalTable: "RateRecommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPerformanceEconomics_CampaignId_RecordedAt",
                table: "CampaignPerformanceEconomics",
                columns: new[] { "CampaignId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPerformanceEconomics_SourceSystem_IdempotencyKey",
                table: "CampaignPerformanceEconomics",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPerformanceEconomics_SupersedesCampaignPerformanceE~",
                table: "CampaignPerformanceEconomics",
                column: "SupersedesCampaignPerformanceEconomicsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlacementEconomics_CampaignPlacementId_RecordedAt",
                table: "HistoricalPlacementEconomics",
                columns: new[] { "CampaignPlacementId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlacementEconomics_CompensationIllustrationId",
                table: "HistoricalPlacementEconomics",
                column: "CompensationIllustrationId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlacementEconomics_QuoteLineItemId",
                table: "HistoricalPlacementEconomics",
                column: "QuoteLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlacementEconomics_QuoteOutcomeId",
                table: "HistoricalPlacementEconomics",
                column: "QuoteOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlacementEconomics_QuoteVersionId",
                table: "HistoricalPlacementEconomics",
                column: "QuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlacementEconomics_RateRecommendationId",
                table: "HistoricalPlacementEconomics",
                column: "RateRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlacementEconomics_SourceSystem_IdempotencyKey",
                table: "HistoricalPlacementEconomics",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlacementEconomics_SupersedesHistoricalPlacementE~",
                table: "HistoricalPlacementEconomics",
                column: "SupersedesHistoricalPlacementEconomicsId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignPerformanceEconomics");

            migrationBuilder.DropTable(
                name: "HistoricalPlacementEconomics");
        }
    }
}
