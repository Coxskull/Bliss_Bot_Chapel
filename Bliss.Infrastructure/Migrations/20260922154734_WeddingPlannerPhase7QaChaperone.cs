using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerPhase7QaChaperone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentAcceptedQaReviewReportVersionId",
                table: "WeddingPlannerWorkspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputQaReviewReportVersionId",
                table: "WeddingPlannerAgentRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeddingPlannerQaEscalationCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RationaleSnapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SelectedVariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedCreativeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerQaEscalationCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaEscalationCases_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaEscalationCases_WeddingPlannerWorkspaces_Wo~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerQaEscalationResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaEscalationCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resolution = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExceptionRationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExceptionAcknowledged = table.Column<bool>(type: "boolean", nullable: true),
                    AcknowledgedBlockerCodesJson = table.Column<string>(type: "text", nullable: true),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerQaEscalationResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPQaResolutions_Case",
                        column: x => x.QaEscalationCaseId,
                        principalTable: "WeddingPlannerQaEscalationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaEscalationResolutions_Advertisers_Advertise~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaEscalationResolutions_WeddingPlannerWorkspa~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerQaReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedVariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    VisualReviewConfirmed = table.Column<bool>(type: "boolean", nullable: true),
                    CopyReviewConfirmed = table.Column<bool>(type: "boolean", nullable: true),
                    ProvenanceReviewConfirmed = table.Column<bool>(type: "boolean", nullable: true),
                    SyntheticMarkerAcknowledged = table.Column<bool>(type: "boolean", nullable: true),
                    EscalationCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerQaReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaReviewDecisions_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaReviewDecisions_WeddingPlannerWorkspaces_Wo~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerQaReviewJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewObjective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FocusAreasJson = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApprovedCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativePackageDocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreativePackageDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedVariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedCreativeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedCreativeAssetSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SelectedCreativeAssetContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SelectedCreativeAssetByteSize = table.Column<int>(type: "integer", nullable: false),
                    SelectedCreativeAssetWidth = table.Column<int>(type: "integer", nullable: false),
                    SelectedCreativeAssetHeight = table.Column<int>(type: "integer", nullable: false),
                    SelectedConceptId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    RulesFindingsJson = table.Column<string>(type: "text", nullable: true),
                    ChaperoneReviewStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    QaInspectionStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    ChaperoneReviewAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    QaInspectionAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputQaReviewReportVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerQaReviewJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPQaJobs_BrandDna",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaJobs_ChaperoneRun",
                        column: x => x.ChaperoneReviewAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaJobs_ColorProfile",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaJobs_CreativeAsset",
                        column: x => x.SelectedCreativeAssetId,
                        principalTable: "WeddingPlannerCreativeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaJobs_CreativeDecision",
                        column: x => x.CreativePackageDecisionId,
                        principalTable: "WeddingPlannerCreativePackageDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaJobs_CreativePackage",
                        column: x => x.ApprovedCreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaJobs_QaInspectionRun",
                        column: x => x.QaInspectionAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaJobs_ResearchReport",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaReviewJobs_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaReviewJobs_WeddingPlannerWorkspaces_Workspa~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerQaReviewReportVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProducingQaReviewJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativePackageDocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreativePackageDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedVariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedCreativeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedCreativeAssetSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SelectedConceptId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerQaReviewReportVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPQaReports_AgentRun",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaReports_BrandDna",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaReports_ColorProfile",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaReports_CreativeAsset",
                        column: x => x.SelectedCreativeAssetId,
                        principalTable: "WeddingPlannerCreativeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaReports_CreativeDecision",
                        column: x => x.CreativePackageDecisionId,
                        principalTable: "WeddingPlannerCreativePackageDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaReports_CreativePackage",
                        column: x => x.ApprovedCreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaReports_Job",
                        column: x => x.ProducingQaReviewJobId,
                        principalTable: "WeddingPlannerQaReviewJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaReports_ResearchReport",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaReviewReportVersions_Advertisers_Advertiser~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaReviewReportVersions_WeddingPlannerWorkspac~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerQaRoleContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContributionSource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContributionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerQaRoleContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPQaContributions_AgentRun",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPQaContributions_Job",
                        column: x => x.QaReviewJobId,
                        principalTable: "WeddingPlannerQaReviewJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaRoleContributions_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaRoleContributions_WeddingPlannerQaReviewRep~",
                        column: x => x.QaReviewReportVersionId,
                        principalTable: "WeddingPlannerQaReviewReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerQaRoleContributions_WeddingPlannerWorkspaces_~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentAcceptedQaReviewReportVersi~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentAcceptedQaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputQaReviewReportVersionId",
                table: "WeddingPlannerAgentRuns",
                column: "OutputQaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationCases_AdvertiserId",
                table: "WeddingPlannerQaEscalationCases",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationCases_QaReviewDecisionId",
                table: "WeddingPlannerQaEscalationCases",
                column: "QaReviewDecisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationCases_QaReviewReportVersionId",
                table: "WeddingPlannerQaEscalationCases",
                column: "QaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationCases_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerQaEscalationCases",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationCases_WorkspaceId",
                table: "WeddingPlannerQaEscalationCases",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationResolutions_AdvertiserId",
                table: "WeddingPlannerQaEscalationResolutions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationResolutions_QaEscalationCaseId",
                table: "WeddingPlannerQaEscalationResolutions",
                column: "QaEscalationCaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationResolutions_QaReviewReportVersion~",
                table: "WeddingPlannerQaEscalationResolutions",
                column: "QaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationResolutions_SourceSystem_Idempote~",
                table: "WeddingPlannerQaEscalationResolutions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaEscalationResolutions_WorkspaceId",
                table: "WeddingPlannerQaEscalationResolutions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewDecisions_AdvertiserId",
                table: "WeddingPlannerQaReviewDecisions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewDecisions_QaReviewReportVersionId",
                table: "WeddingPlannerQaReviewDecisions",
                column: "QaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewDecisions_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerQaReviewDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewDecisions_WorkspaceId",
                table: "WeddingPlannerQaReviewDecisions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_AdvertiserId",
                table: "WeddingPlannerQaReviewJobs",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_ApprovedBrandDnaVersionId",
                table: "WeddingPlannerQaReviewJobs",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_ApprovedColorProfileVersionId",
                table: "WeddingPlannerQaReviewJobs",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_ApprovedCreativePackageVersionId",
                table: "WeddingPlannerQaReviewJobs",
                column: "ApprovedCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_ApprovedResearchReportVersionId",
                table: "WeddingPlannerQaReviewJobs",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_ChaperoneReviewAgentRunId",
                table: "WeddingPlannerQaReviewJobs",
                column: "ChaperoneReviewAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_CreativePackageDecisionId",
                table: "WeddingPlannerQaReviewJobs",
                column: "CreativePackageDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_OutputQaReviewReportVersionId",
                table: "WeddingPlannerQaReviewJobs",
                column: "OutputQaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_QaInspectionAgentRunId",
                table: "WeddingPlannerQaReviewJobs",
                column: "QaInspectionAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_SelectedCreativeAssetId",
                table: "WeddingPlannerQaReviewJobs",
                column: "SelectedCreativeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerQaReviewJobs",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_StartedAt",
                table: "WeddingPlannerQaReviewJobs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewJobs_WorkspaceId",
                table: "WeddingPlannerQaReviewJobs",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_AdvertiserId",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_ApprovedBrandDnaVersio~",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_ApprovedColorProfileVe~",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_ApprovedCreativePackag~",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "ApprovedCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_ApprovedResearchReport~",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_CreativePackageDecisio~",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "CreativePackageDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_ProducingAgentRunId",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_ProducingQaReviewJobId",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "ProducingQaReviewJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_SelectedCreativeAssetId",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "SelectedCreativeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_SourceSystem_Idempoten~",
                table: "WeddingPlannerQaReviewReportVersions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_WorkspaceId",
                table: "WeddingPlannerQaReviewReportVersions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaReviewReportVersions_WorkspaceId_VersionNum~",
                table: "WeddingPlannerQaReviewReportVersions",
                columns: new[] { "WorkspaceId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaRoleContributions_AdvertiserId",
                table: "WeddingPlannerQaRoleContributions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaRoleContributions_ProducingAgentRunId",
                table: "WeddingPlannerQaRoleContributions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaRoleContributions_QaReviewJobId",
                table: "WeddingPlannerQaRoleContributions",
                column: "QaReviewJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaRoleContributions_QaReviewReportVersionId",
                table: "WeddingPlannerQaRoleContributions",
                column: "QaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaRoleContributions_QaReviewReportVersionId_L~",
                table: "WeddingPlannerQaRoleContributions",
                columns: new[] { "QaReviewReportVersionId", "LogicalRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerQaRoleContributions_WorkspaceId",
                table: "WeddingPlannerQaRoleContributions",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerQaReviewReportVersion~",
                table: "WeddingPlannerAgentRuns",
                column: "OutputQaReviewReportVersionId",
                principalTable: "WeddingPlannerQaReviewReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerQaReviewReportVersio~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentAcceptedQaReviewReportVersionId",
                principalTable: "WeddingPlannerQaReviewReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WPQaCases_Decision",
                table: "WeddingPlannerQaEscalationCases",
                column: "QaReviewDecisionId",
                principalTable: "WeddingPlannerQaReviewDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerQaEscalationCases_WeddingPlannerQaReviewRepor~",
                table: "WeddingPlannerQaEscalationCases",
                column: "QaReviewReportVersionId",
                principalTable: "WeddingPlannerQaReviewReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WPQaResolutions_Report",
                table: "WeddingPlannerQaEscalationResolutions",
                column: "QaReviewReportVersionId",
                principalTable: "WeddingPlannerQaReviewReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerQaReviewDecisions_WeddingPlannerQaReviewRepor~",
                table: "WeddingPlannerQaReviewDecisions",
                column: "QaReviewReportVersionId",
                principalTable: "WeddingPlannerQaReviewReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WPQaJobs_OutputReport",
                table: "WeddingPlannerQaReviewJobs",
                column: "OutputQaReviewReportVersionId",
                principalTable: "WeddingPlannerQaReviewReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerQaReviewReportVersion~",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerQaReviewReportVersio~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_WPQaJobs_OutputReport",
                table: "WeddingPlannerQaReviewJobs");

            migrationBuilder.DropTable(
                name: "WeddingPlannerQaEscalationResolutions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerQaRoleContributions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerQaEscalationCases");

            migrationBuilder.DropTable(
                name: "WeddingPlannerQaReviewDecisions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerQaReviewReportVersions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerQaReviewJobs");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentAcceptedQaReviewReportVersi~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputQaReviewReportVersionId",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropColumn(
                name: "CurrentAcceptedQaReviewReportVersionId",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropColumn(
                name: "OutputQaReviewReportVersionId",
                table: "WeddingPlannerAgentRuns");
        }
    }
}
