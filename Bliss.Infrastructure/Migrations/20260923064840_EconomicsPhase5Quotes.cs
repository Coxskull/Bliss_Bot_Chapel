using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase5Quotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserOpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_AdvertiserOpportunities_AdvertiserOpportunityId",
                        column: x => x.AdvertiserOpportunityId,
                        principalTable: "AdvertiserOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RevisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteVersions_QuoteVersions_ParentVersionId",
                        column: x => x.ParentVersionId,
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuoteVersions_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewerLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteApprovalDecisions_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuoteApprovalDecisions_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateRecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RecommendationLow = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendationTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendationHigh = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteLineItems_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuoteLineItems_RateRecommendations_RateRecommendationId",
                        column: x => x.RateRecommendationId,
                        principalTable: "RateRecommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewQuoteVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Response = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteOutcomes_QuoteVersions_NewQuoteVersionId",
                        column: x => x.NewQuoteVersionId,
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuoteOutcomes_QuoteVersions_QuoteVersionId",
                        column: x => x.QuoteVersionId,
                        principalTable: "QuoteVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuoteOutcomes_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteApprovalDecisions_QuoteId",
                table: "QuoteApprovalDecisions",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteApprovalDecisions_QuoteVersionId",
                table: "QuoteApprovalDecisions",
                column: "QuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteApprovalDecisions_SourceSystem_IdempotencyKey",
                table: "QuoteApprovalDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLineItems_QuoteVersionId_SortOrder",
                table: "QuoteLineItems",
                columns: new[] { "QuoteVersionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLineItems_RateRecommendationId",
                table: "QuoteLineItems",
                column: "RateRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteOutcomes_NewQuoteVersionId",
                table: "QuoteOutcomes",
                column: "NewQuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteOutcomes_QuoteId",
                table: "QuoteOutcomes",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteOutcomes_QuoteVersionId",
                table: "QuoteOutcomes",
                column: "QuoteVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteOutcomes_SourceSystem_IdempotencyKey",
                table: "QuoteOutcomes",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_AdvertiserOpportunityId",
                table: "Quotes",
                column: "AdvertiserOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CreatedAt",
                table: "Quotes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SourceSystem_IdempotencyKey",
                table: "Quotes",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteVersions_ParentVersionId",
                table: "QuoteVersions",
                column: "ParentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteVersions_QuoteId_VersionNumber",
                table: "QuoteVersions",
                columns: new[] { "QuoteId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteVersions_SourceSystem_IdempotencyKey",
                table: "QuoteVersions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteApprovalDecisions");

            migrationBuilder.DropTable(
                name: "QuoteLineItems");

            migrationBuilder.DropTable(
                name: "QuoteOutcomes");

            migrationBuilder.DropTable(
                name: "QuoteVersions");

            migrationBuilder.DropTable(
                name: "Quotes");
        }
    }
}
