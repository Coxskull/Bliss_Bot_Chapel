using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase7ResearchOrchestration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EconomicsResearchRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IndustryCategory = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InventorySlotType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResearchQuestion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicsResearchRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EconomicsResearchRuns_GeographicMarkets_GeographicMarketId",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EconomicsResearchCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EconomicsResearchRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndustryCategory = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InventorySlotType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Metric = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NumericValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeLow = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RangeHigh = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    SourceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RetrievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExtractionModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PromotedObservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicsResearchCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EconomicsResearchCandidates_EconomicsResearchRuns_Economics~",
                        column: x => x.EconomicsResearchRunId,
                        principalTable: "EconomicsResearchRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EconomicsResearchCandidates_GeographicMarkets_GeographicMar~",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EconomicsResearchCandidates_MarketBenchmarkObservations_Pro~",
                        column: x => x.PromotedObservationId,
                        principalTable: "MarketBenchmarkObservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EconomicsResearchReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EconomicsResearchCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketBenchmarkObservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewerLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicsResearchReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EconomicsResearchReviewDecisions_EconomicsResearchCandidate~",
                        column: x => x.EconomicsResearchCandidateId,
                        principalTable: "EconomicsResearchCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EconomicsResearchReviewDecisions_MarketBenchmarkObservation~",
                        column: x => x.MarketBenchmarkObservationId,
                        principalTable: "MarketBenchmarkObservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchCandidates_EconomicsResearchRunId",
                table: "EconomicsResearchCandidates",
                column: "EconomicsResearchRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchCandidates_GeographicMarketId",
                table: "EconomicsResearchCandidates",
                column: "GeographicMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchCandidates_PromotedObservationId",
                table: "EconomicsResearchCandidates",
                column: "PromotedObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchCandidates_SourceSystem_IdempotencyKey",
                table: "EconomicsResearchCandidates",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchCandidates_Status_CreatedAt",
                table: "EconomicsResearchCandidates",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchReviewDecisions_EconomicsResearchCandidate~",
                table: "EconomicsResearchReviewDecisions",
                column: "EconomicsResearchCandidateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchReviewDecisions_MarketBenchmarkObservation~",
                table: "EconomicsResearchReviewDecisions",
                column: "MarketBenchmarkObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchReviewDecisions_SourceSystem_IdempotencyKey",
                table: "EconomicsResearchReviewDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchRuns_GeographicMarketId",
                table: "EconomicsResearchRuns",
                column: "GeographicMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchRuns_SourceSystem_IdempotencyKey",
                table: "EconomicsResearchRuns",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsResearchRuns_Status_CreatedAt",
                table: "EconomicsResearchRuns",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EconomicsResearchReviewDecisions");

            migrationBuilder.DropTable(
                name: "EconomicsResearchCandidates");

            migrationBuilder.DropTable(
                name: "EconomicsResearchRuns");
        }
    }
}
