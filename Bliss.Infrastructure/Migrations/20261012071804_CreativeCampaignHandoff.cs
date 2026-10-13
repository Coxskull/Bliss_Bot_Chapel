using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreativeCampaignHandoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdInventorySlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Stage = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DimensionsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    FormatVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatorSafeAreaVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ImageWidth = table.Column<int>(type: "integer", nullable: false),
                    ImageHeight = table.Column<int>(type: "integer", nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateApprovalDecisions_AcademyProductionCandidates_Cand~",
                        column: x => x.CandidateId,
                        principalTable: "AcademyProductionCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateApprovalDecisions_AcademyProductionReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "AcademyProductionReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateApprovalDecisions_AdInventorySlots_AdInventorySlot~",
                        column: x => x.AdInventorySlotId,
                        principalTable: "AdInventorySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreativeCampaignHandoffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserApprovalId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlphaQaApprovalId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryFitId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteLineItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPlacementRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreativeCampaignHandoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_AcademyProductionCandidates_Candid~",
                        column: x => x.CandidateId,
                        principalTable: "AcademyProductionCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_AcademyProductionReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "AcademyProductionReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_CampaignPlacementRuns_CampaignPlac~",
                        column: x => x.CampaignPlacementRunId,
                        principalTable: "CampaignPlacementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_CampaignPlacements_CampaignPlaceme~",
                        column: x => x.CampaignPlacementId,
                        principalTable: "CampaignPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_CandidateApprovalDecisions_Adverti~",
                        column: x => x.AdvertiserApprovalId,
                        principalTable: "CandidateApprovalDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_CandidateApprovalDecisions_AlphaQa~",
                        column: x => x.AlphaQaApprovalId,
                        principalTable: "CandidateApprovalDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_CandidateApprovalDecisions_Invento~",
                        column: x => x.InventoryFitId,
                        principalTable: "CandidateApprovalDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_QuoteLineItems_QuoteLineItemId",
                        column: x => x.QuoteLineItemId,
                        principalTable: "QuoteLineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreativeCampaignHandoffs_WeddingPlannerWorkspaces_Workspace~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApprovalDecisions_AdInventorySlotId",
                table: "CandidateApprovalDecisions",
                column: "AdInventorySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApprovalDecisions_CandidateId_IdempotencyKey",
                table: "CandidateApprovalDecisions",
                columns: new[] { "CandidateId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApprovalDecisions_CandidateId_ScopeKey_Version",
                table: "CandidateApprovalDecisions",
                columns: new[] { "CandidateId", "ScopeKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApprovalDecisions_ReviewId",
                table: "CandidateApprovalDecisions",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_AdvertiserApprovalId",
                table: "CreativeCampaignHandoffs",
                column: "AdvertiserApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_AlphaQaApprovalId",
                table: "CreativeCampaignHandoffs",
                column: "AlphaQaApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_CampaignPlacementId",
                table: "CreativeCampaignHandoffs",
                column: "CampaignPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_CampaignPlacementRunId",
                table: "CreativeCampaignHandoffs",
                column: "CampaignPlacementRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_CandidateId",
                table: "CreativeCampaignHandoffs",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_InventoryFitId",
                table: "CreativeCampaignHandoffs",
                column: "InventoryFitId");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_QuoteLineItemId",
                table: "CreativeCampaignHandoffs",
                column: "QuoteLineItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_ReviewId",
                table: "CreativeCampaignHandoffs",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeCampaignHandoffs_WorkspaceId_IdempotencyKey",
                table: "CreativeCampaignHandoffs",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreativeCampaignHandoffs");

            migrationBuilder.DropTable(
                name: "CandidateApprovalDecisions");
        }
    }
}
