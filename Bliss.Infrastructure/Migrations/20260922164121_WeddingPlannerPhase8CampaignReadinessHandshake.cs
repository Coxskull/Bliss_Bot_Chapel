using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerPhase8CampaignReadinessHandshake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentCampaignReadinessHandshakeVersionId",
                table: "WeddingPlannerWorkspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeddingPlannerCampaignReadinessHandshakeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QaReviewReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewReportDocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QaAcceptDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativePackageDocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreativePackageDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedVariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedCreativeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedCreativeAssetSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SelectedCreativeAssetByteSize = table.Column<int>(type: "integer", nullable: false),
                    SelectedCreativeAssetWidth = table.Column<int>(type: "integer", nullable: false),
                    SelectedCreativeAssetHeight = table.Column<int>(type: "integer", nullable: false),
                    ApprovedConceptPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedConceptId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchStatusSnapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MatchOverallScoreSnapshot = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OpportunityStatusSnapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdInventorySlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPlacementRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RulesFindingsJson = table.Column<string>(type: "text", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DisclaimerAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    SyntheticMarkerAcknowledged = table.Column<bool>(type: "boolean", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerCampaignReadinessHandshakeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_BlissMatch",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_Creator",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_Opportunity",
                        column: x => x.AdvertiserOpportunityId,
                        principalTable: "AdvertiserOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_RuleVersion",
                        column: x => x.RuleVersionId,
                        principalTable: "RuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_BrandDna",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_Campaign",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_ColorProfile",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_ConceptPackage",
                        column: x => x.ApprovedConceptPackageVersionId,
                        principalTable: "WeddingPlannerConceptPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_Content",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_CreativeAsset",
                        column: x => x.SelectedCreativeAssetId,
                        principalTable: "WeddingPlannerCreativeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_CreativeDecision",
                        column: x => x.CreativePackageDecisionId,
                        principalTable: "WeddingPlannerCreativePackageDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_CreativePackage",
                        column: x => x.ApprovedCreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_Placement",
                        column: x => x.CampaignPlacementId,
                        principalTable: "CampaignPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_PlacementRun",
                        column: x => x.CampaignPlacementRunId,
                        principalTable: "CampaignPlacementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_QaDecision",
                        column: x => x.QaAcceptDecisionId,
                        principalTable: "WeddingPlannerQaReviewDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_QaReport",
                        column: x => x.QaReviewReportVersionId,
                        principalTable: "WeddingPlannerQaReviewReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_ResearchReport",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCrHandshake_Slot",
                        column: x => x.AdInventorySlotId,
                        principalTable: "AdInventorySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCampaignReadinessHandshakeVersions_Advertiser~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCampaignReadinessHandshakeVersions_WeddingPla~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerCampaignReadinessDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignReadinessHandshakeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerCampaignReadinessDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCampaignReadinessDecisions_Advertisers_Advert~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCampaignReadinessDecisions_WeddingPlannerCamp~",
                        column: x => x.CampaignReadinessHandshakeVersionId,
                        principalTable: "WeddingPlannerCampaignReadinessHandshakeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCampaignReadinessDecisions_WeddingPlannerWork~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentCampaignReadinessHandshakeV~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentCampaignReadinessHandshakeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessDecisions_AdvertiserId",
                table: "WeddingPlannerCampaignReadinessDecisions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessDecisions_CampaignReadinessH~",
                table: "WeddingPlannerCampaignReadinessDecisions",
                column: "CampaignReadinessHandshakeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessDecisions_SourceSystem_Idemp~",
                table: "WeddingPlannerCampaignReadinessDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessDecisions_WorkspaceId",
                table: "WeddingPlannerCampaignReadinessDecisions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_AdInventor~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "AdInventorySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_Advertiser~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_ApprovedBr~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_ApprovedC~1",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "ApprovedConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_ApprovedCo~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_ApprovedCr~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "ApprovedCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_ApprovedRe~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_BlissMatch~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_CampaignId",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_CampaignP~1",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "CampaignPlacementRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_CampaignPl~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "CampaignPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_ContentIte~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_CreatorId",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_AdvertiserOpportunityId",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "AdvertiserOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_RuleVersionId",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_CreativePa~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "CreativePackageDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_QaAcceptDe~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "QaAcceptDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_QaReviewRe~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "QaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_SelectedCr~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "SelectedCreativeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_SourceSyst~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_WorkspaceI~",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                columns: new[] { "WorkspaceId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_WorkspaceId",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerCampaignReadinessHan~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentCampaignReadinessHandshakeVersionId",
                principalTable: "WeddingPlannerCampaignReadinessHandshakeVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerCampaignReadinessHan~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropTable(
                name: "WeddingPlannerCampaignReadinessDecisions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerCampaignReadinessHandshakeVersions");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentCampaignReadinessHandshakeV~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropColumn(
                name: "CurrentCampaignReadinessHandshakeVersionId",
                table: "WeddingPlannerWorkspaces");
        }
    }
}
