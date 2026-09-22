using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerPhase9MeasurementLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentAcceptedMeasurementLearningReportVersionId",
                table: "WeddingPlannerWorkspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputMeasurementLearningReportVersionId",
                table: "WeddingPlannerAgentRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeddingPlannerMeasurementLearningDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasurementLearningReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_WeddingPlannerMeasurementLearningDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningDecisions_Advertisers_Adve~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningDecisions_WeddingPlannerWo~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerMeasurementLearningJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignReadinessHandshakeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandshakeStatusSnapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HandshakeWasCurrentAtJobStart = table.Column<bool>(type: "boolean", nullable: false),
                    CampaignPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPlacementRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdInventorySlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedVariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedCreativeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedConceptPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedConceptId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ObservationStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservationEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AttestationAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    Impressions = table.Column<long>(type: "bigint", nullable: false),
                    Clicks = table.Column<long>(type: "bigint", nullable: false),
                    Conversions = table.Column<long>(type: "bigint", nullable: false),
                    Spend = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: true),
                    RulesFindingsJson = table.Column<string>(type: "text", nullable: true),
                    PerformanceAnalysisStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    LearningSynthesisStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    PerformanceAnalysisAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    LearningSynthesisAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputMeasurementLearningReportVersionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_WeddingPlannerMeasurementLearningJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_BrandDna",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_Campaign",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_ColorProfile",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_ConceptPackage",
                        column: x => x.ApprovedConceptPackageVersionId,
                        principalTable: "WeddingPlannerConceptPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_Content",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_CreativeAsset",
                        column: x => x.SelectedCreativeAssetId,
                        principalTable: "WeddingPlannerCreativeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_CreativePackage",
                        column: x => x.ApprovedCreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_Handshake",
                        column: x => x.CampaignReadinessHandshakeVersionId,
                        principalTable: "WeddingPlannerCampaignReadinessHandshakeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_LearnRun",
                        column: x => x.LearningSynthesisAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_Match",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_PerfRun",
                        column: x => x.PerformanceAnalysisAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_Placement",
                        column: x => x.CampaignPlacementId,
                        principalTable: "CampaignPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_PlacementRun",
                        column: x => x.CampaignPlacementRunId,
                        principalTable: "CampaignPlacementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_QaReport",
                        column: x => x.QaReviewReportVersionId,
                        principalTable: "WeddingPlannerQaReviewReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_ResearchReport",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlJobs_Slot",
                        column: x => x.AdInventorySlotId,
                        principalTable: "AdInventorySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningJobs_Advertisers_Advertise~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningJobs_WeddingPlannerWorkspa~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerMeasurementLearningReportVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProducingMeasurementLearningJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformanceAnalysisAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningSynthesisAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignReadinessHandshakeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandshakeStatusSnapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HandshakeWasCurrentAtJobStart = table.Column<bool>(type: "boolean", nullable: false),
                    CampaignPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPlacementRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdInventorySlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaReviewReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedVariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedCreativeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedConceptPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedConceptId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ObservationStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservationEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ObservationSourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Impressions = table.Column<long>(type: "bigint", nullable: false),
                    Clicks = table.Column<long>(type: "bigint", nullable: false),
                    Conversions = table.Column<long>(type: "bigint", nullable: false),
                    Spend = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: false),
                    RulesFindingsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerMeasurementLearningReportVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPMlReports_BrandDna",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_Campaign",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_ColorProfile",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_ConceptPackage",
                        column: x => x.ApprovedConceptPackageVersionId,
                        principalTable: "WeddingPlannerConceptPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_Content",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_CreativeAsset",
                        column: x => x.SelectedCreativeAssetId,
                        principalTable: "WeddingPlannerCreativeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_CreativePackage",
                        column: x => x.ApprovedCreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_Handshake",
                        column: x => x.CampaignReadinessHandshakeVersionId,
                        principalTable: "WeddingPlannerCampaignReadinessHandshakeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_Job",
                        column: x => x.ProducingMeasurementLearningJobId,
                        principalTable: "WeddingPlannerMeasurementLearningJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_LearnRun",
                        column: x => x.LearningSynthesisAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_Match",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_PerfRun",
                        column: x => x.PerformanceAnalysisAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_Placement",
                        column: x => x.CampaignPlacementId,
                        principalTable: "CampaignPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_PlacementRun",
                        column: x => x.CampaignPlacementRunId,
                        principalTable: "CampaignPlacementRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_ProducingRun",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_QaReport",
                        column: x => x.QaReviewReportVersionId,
                        principalTable: "WeddingPlannerQaReviewReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_ResearchReport",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlReports_Slot",
                        column: x => x.AdInventorySlotId,
                        principalTable: "AdInventorySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningReportVersions_Advertisers~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningReportVersions_WeddingPlan~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerMeasurementLearningRoleContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasurementLearningReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasurementLearningJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContributionSource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerMeasurementLearningRoleContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPMlContributions_AgentRun",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPMlContributions_Job",
                        column: x => x.MeasurementLearningJobId,
                        principalTable: "WeddingPlannerMeasurementLearningJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningRoleContributions_Advertis~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningRoleContributions_WeddingP~",
                        column: x => x.MeasurementLearningReportVersionId,
                        principalTable: "WeddingPlannerMeasurementLearningReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerMeasurementLearningRoleContributions_Wedding~1",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentAcceptedMeasurementLearning~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentAcceptedMeasurementLearningReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_Advertise~1",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions",
                column: "AdvertiserOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputMeasurementLearningReportVers~",
                table: "WeddingPlannerAgentRuns",
                column: "OutputMeasurementLearningReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningDecisions_AdvertiserId",
                table: "WeddingPlannerMeasurementLearningDecisions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningDecisions_MeasurementLearn~",
                table: "WeddingPlannerMeasurementLearningDecisions",
                column: "MeasurementLearningReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningDecisions_OccurredAt",
                table: "WeddingPlannerMeasurementLearningDecisions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningDecisions_SourceSystem_Ide~",
                table: "WeddingPlannerMeasurementLearningDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningDecisions_WorkspaceId",
                table: "WeddingPlannerMeasurementLearningDecisions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_AdInventorySlotId",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "AdInventorySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_AdvertiserId",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_ApprovedBrandDnaVersi~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_ApprovedColorProfileV~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_ApprovedConceptPackag~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "ApprovedConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_ApprovedCreativePacka~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "ApprovedCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_ApprovedResearchRepor~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_BlissMatchId",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_CampaignId",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_CampaignPlacementId",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "CampaignPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_CampaignPlacementRunId",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "CampaignPlacementRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_CampaignReadinessHand~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "CampaignReadinessHandshakeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_ContentItemId",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_LearningSynthesisAgen~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "LearningSynthesisAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_OutputMeasurementLear~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "OutputMeasurementLearningReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_PerformanceAnalysisAg~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "PerformanceAnalysisAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_QaReviewReportVersion~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "QaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_SelectedCreativeAsset~",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "SelectedCreativeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_SourceSystem_Idempote~",
                table: "WeddingPlannerMeasurementLearningJobs",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_StartedAt",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningJobs_WorkspaceId",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_AdInventory~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "AdInventorySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_AdvertiserId",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_ApprovedBra~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_ApprovedCol~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_ApprovedCon~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "ApprovedConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_ApprovedCre~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "ApprovedCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_ApprovedRes~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_BlissMatchId",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_CampaignId",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_CampaignPl~1",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "CampaignPlacementRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_CampaignPla~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "CampaignPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_CampaignRea~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "CampaignReadinessHandshakeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_ContentItem~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_CreatedAt",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_LearningSyn~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "LearningSynthesisAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_Performance~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "PerformanceAnalysisAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_ProducingAg~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_ProducingMe~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "ProducingMeasurementLearningJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_QaReviewRep~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "QaReviewReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_SelectedCre~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "SelectedCreativeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_SourceSyste~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_WorkspaceId",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningReportVersions_WorkspaceId~",
                table: "WeddingPlannerMeasurementLearningReportVersions",
                columns: new[] { "WorkspaceId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningRoleContributions_Advertis~",
                table: "WeddingPlannerMeasurementLearningRoleContributions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningRoleContributions_Measure~1",
                table: "WeddingPlannerMeasurementLearningRoleContributions",
                column: "MeasurementLearningReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningRoleContributions_Measure~2",
                table: "WeddingPlannerMeasurementLearningRoleContributions",
                columns: new[] { "MeasurementLearningReportVersionId", "LogicalRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningRoleContributions_Measurem~",
                table: "WeddingPlannerMeasurementLearningRoleContributions",
                column: "MeasurementLearningJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningRoleContributions_Producin~",
                table: "WeddingPlannerMeasurementLearningRoleContributions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerMeasurementLearningRoleContributions_Workspac~",
                table: "WeddingPlannerMeasurementLearningRoleContributions",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerMeasurementLearningRe~",
                table: "WeddingPlannerAgentRuns",
                column: "OutputMeasurementLearningReportVersionId",
                principalTable: "WeddingPlannerMeasurementLearningReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerMeasurementLearningR~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentAcceptedMeasurementLearningReportVersionId",
                principalTable: "WeddingPlannerMeasurementLearningReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerMeasurementLearningDecisions_WeddingPlannerMe~",
                table: "WeddingPlannerMeasurementLearningDecisions",
                column: "MeasurementLearningReportVersionId",
                principalTable: "WeddingPlannerMeasurementLearningReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WPMlJobs_OutputReport",
                table: "WeddingPlannerMeasurementLearningJobs",
                column: "OutputMeasurementLearningReportVersionId",
                principalTable: "WeddingPlannerMeasurementLearningReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerMeasurementLearningRe~",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerMeasurementLearningR~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_WPMlJobs_OutputReport",
                table: "WeddingPlannerMeasurementLearningJobs");

            migrationBuilder.DropTable(
                name: "WeddingPlannerMeasurementLearningDecisions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerMeasurementLearningRoleContributions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerMeasurementLearningReportVersions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerMeasurementLearningJobs");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentAcceptedMeasurementLearning~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerCampaignReadinessHandshakeVersions_Advertise~1",
                table: "WeddingPlannerCampaignReadinessHandshakeVersions");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputMeasurementLearningReportVers~",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropColumn(
                name: "CurrentAcceptedMeasurementLearningReportVersionId",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropColumn(
                name: "OutputMeasurementLearningReportVersionId",
                table: "WeddingPlannerAgentRuns");
        }
    }
}
