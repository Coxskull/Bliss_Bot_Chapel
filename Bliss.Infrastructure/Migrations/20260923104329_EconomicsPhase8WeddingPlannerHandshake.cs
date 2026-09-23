using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase8WeddingPlannerHandshake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeddingPlannerEconomicsRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdInventorySlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    PricingModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateRecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    IndustryCategory = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CampaignObjective = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerEconomicsRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerEconomicsRequests_AdInventorySlots_AdInventor~",
                        column: x => x.AdInventorySlotId,
                        principalTable: "AdInventorySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerEconomicsRequests_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerEconomicsRequests_BlissMatches_BlissMatchId",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerEconomicsRequests_GeographicMarkets_Geographi~",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerEconomicsRequests_PricingModels_PricingModelId",
                        column: x => x.PricingModelId,
                        principalTable: "PricingModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerEconomicsRequests_RateRecommendations_RateRec~",
                        column: x => x.RateRecommendationId,
                        principalTable: "RateRecommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerEconomicsRequests_WeddingPlannerPlanningSessi~",
                        column: x => x.SessionId,
                        principalTable: "WeddingPlannerPlanningSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerEconomicsRequests_WeddingPlannerWorkspaces_Wo~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_AdInventorySlotId",
                table: "WeddingPlannerEconomicsRequests",
                column: "AdInventorySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_AdvertiserId",
                table: "WeddingPlannerEconomicsRequests",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_BlissMatchId",
                table: "WeddingPlannerEconomicsRequests",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_GeographicMarketId",
                table: "WeddingPlannerEconomicsRequests",
                column: "GeographicMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_PricingModelId",
                table: "WeddingPlannerEconomicsRequests",
                column: "PricingModelId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_RateRecommendationId",
                table: "WeddingPlannerEconomicsRequests",
                column: "RateRecommendationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_SessionId_CreatedAt",
                table: "WeddingPlannerEconomicsRequests",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerEconomicsRequests",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerEconomicsRequests_WorkspaceId",
                table: "WeddingPlannerEconomicsRequests",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeddingPlannerEconomicsRequests");
        }
    }
}
