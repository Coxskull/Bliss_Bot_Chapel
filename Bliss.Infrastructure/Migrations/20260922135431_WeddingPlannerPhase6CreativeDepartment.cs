using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerPhase6CreativeDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApprovedCreativePackageVersionId",
                table: "WeddingPlannerWorkspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputCreativePackageVersionId",
                table: "WeddingPlannerAgentRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeddingPlannerCreativeAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativeProductionJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Format = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    ByteSize = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AdapterVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReceiptJson = table.Column<string>(type: "text", nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerCreativeAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeAssets_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeAssets_WeddingPlannerWorkspaces_Works~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerCreativePackageDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedVariantId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerCreativePackageDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativePackageDecisions_Advertisers_Advertis~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativePackageDecisions_WeddingPlannerWorksp~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerCreativePackageVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProducingCreativeProductionJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedConceptPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedConceptId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    JobKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ParentCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerCreativePackageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPCreativePackages_BrandDna",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativePackages_ColorProfile",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativePackages_ConceptPackage",
                        column: x => x.ApprovedConceptPackageVersionId,
                        principalTable: "WeddingPlannerConceptPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativePackages_ParentPackage",
                        column: x => x.ParentCreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativePackages_ProducingRun",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativePackages_ResearchReport",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativePackageVersions_Advertisers_Advertise~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativePackageVersions_WeddingPlannerWorkspa~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerCreativeProductionJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Objective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FormatsJson = table.Column<string>(type: "text", nullable: false),
                    RequestedVariantCount = table.Column<int>(type: "integer", nullable: false),
                    RevisionParentCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevisionNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApprovedConceptPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedConceptId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreativeDirectionStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    StrategyAdaptationStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    VisualSystemStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    ImageDirectionStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    CopySystemStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    VariantProductionStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    CreativeDirectionAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    StrategyAdaptationAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    VisualSystemAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageDirectionAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CopySystemAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    VariantProductionAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AssetProviderAdapterVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AssetProviderRequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AssetProviderReceiptJson = table.Column<string>(type: "text", nullable: true),
                    AssetProviderEstimatedCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    OutputCreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_WeddingPlannerCreativeProductionJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_BrandDna",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_ColorProfile",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_ConceptPackage",
                        column: x => x.ApprovedConceptPackageVersionId,
                        principalTable: "WeddingPlannerConceptPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_CopyRun",
                        column: x => x.CopySystemAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_DirectionRun",
                        column: x => x.CreativeDirectionAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_ImageRun",
                        column: x => x.ImageDirectionAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_OutputPackage",
                        column: x => x.OutputCreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_ResearchReport",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_RevisionParent",
                        column: x => x.RevisionParentCreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_StrategyRun",
                        column: x => x.StrategyAdaptationAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_VariantRun",
                        column: x => x.VariantProductionAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WPCreativeJobs_VisualRun",
                        column: x => x.VisualSystemAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeProductionJobs_Advertisers_Advertiser~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeProductionJobs_WeddingPlannerWorkspac~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerCreativeRoleContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativeProductionJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerCreativeRoleContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeRoleContributions_Advertisers_Adverti~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeRoleContributions_WeddingPlannerAgent~",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeRoleContributions_WeddingPlannerCreat~",
                        column: x => x.CreativePackageVersionId,
                        principalTable: "WeddingPlannerCreativePackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeRoleContributions_WeddingPlannerCrea~1",
                        column: x => x.CreativeProductionJobId,
                        principalTable: "WeddingPlannerCreativeProductionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerCreativeRoleContributions_WeddingPlannerWorks~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentApprovedCreativePackageVers~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentApprovedCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputCreativePackageVersionId",
                table: "WeddingPlannerAgentRuns",
                column: "OutputCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeAssets_AdvertiserId",
                table: "WeddingPlannerCreativeAssets",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeAssets_CreatedAt",
                table: "WeddingPlannerCreativeAssets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeAssets_CreativePackageVersionId_Varia~",
                table: "WeddingPlannerCreativeAssets",
                columns: new[] { "CreativePackageVersionId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeAssets_CreativeProductionJobId",
                table: "WeddingPlannerCreativeAssets",
                column: "CreativeProductionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeAssets_WorkspaceId",
                table: "WeddingPlannerCreativeAssets",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageDecisions_AdvertiserId",
                table: "WeddingPlannerCreativePackageDecisions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageDecisions_CreativePackageVersi~",
                table: "WeddingPlannerCreativePackageDecisions",
                column: "CreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageDecisions_OccurredAt",
                table: "WeddingPlannerCreativePackageDecisions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageDecisions_SourceSystem_Idempot~",
                table: "WeddingPlannerCreativePackageDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageDecisions_WorkspaceId",
                table: "WeddingPlannerCreativePackageDecisions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_AdvertiserId",
                table: "WeddingPlannerCreativePackageVersions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_ApprovedBrandDnaVersi~",
                table: "WeddingPlannerCreativePackageVersions",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_ApprovedColorProfileV~",
                table: "WeddingPlannerCreativePackageVersions",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_ApprovedConceptPackag~",
                table: "WeddingPlannerCreativePackageVersions",
                column: "ApprovedConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_ApprovedResearchRepor~",
                table: "WeddingPlannerCreativePackageVersions",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_CreatedAt",
                table: "WeddingPlannerCreativePackageVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_ParentCreativePackage~",
                table: "WeddingPlannerCreativePackageVersions",
                column: "ParentCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_ProducingAgentRunId",
                table: "WeddingPlannerCreativePackageVersions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_ProducingCreativeProd~",
                table: "WeddingPlannerCreativePackageVersions",
                column: "ProducingCreativeProductionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_SourceSystem_Idempote~",
                table: "WeddingPlannerCreativePackageVersions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_WorkspaceId",
                table: "WeddingPlannerCreativePackageVersions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativePackageVersions_WorkspaceId_VersionNu~",
                table: "WeddingPlannerCreativePackageVersions",
                columns: new[] { "WorkspaceId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_AdvertiserId",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_ApprovedBrandDnaVersio~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_ApprovedColorProfileVe~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_ApprovedConceptPackage~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "ApprovedConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_ApprovedResearchReport~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_CopySystemAgentRunId",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "CopySystemAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_CreativeDirectionAgent~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "CreativeDirectionAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_ImageDirectionAgentRun~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "ImageDirectionAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_OutputCreativePackageV~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "OutputCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_RevisionParentCreative~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "RevisionParentCreativePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_SourceSystem_Idempoten~",
                table: "WeddingPlannerCreativeProductionJobs",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_StartedAt",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_StrategyAdaptationAgen~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "StrategyAdaptationAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_VariantProductionAgent~",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "VariantProductionAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_VisualSystemAgentRunId",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "VisualSystemAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeProductionJobs_WorkspaceId",
                table: "WeddingPlannerCreativeProductionJobs",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeRoleContributions_AdvertiserId",
                table: "WeddingPlannerCreativeRoleContributions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeRoleContributions_CreatedAt",
                table: "WeddingPlannerCreativeRoleContributions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeRoleContributions_CreativePackageVers~",
                table: "WeddingPlannerCreativeRoleContributions",
                columns: new[] { "CreativePackageVersionId", "LogicalRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeRoleContributions_CreativeProductionJ~",
                table: "WeddingPlannerCreativeRoleContributions",
                column: "CreativeProductionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeRoleContributions_ProducingAgentRunId",
                table: "WeddingPlannerCreativeRoleContributions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerCreativeRoleContributions_WorkspaceId",
                table: "WeddingPlannerCreativeRoleContributions",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerCreativePackageVersio~",
                table: "WeddingPlannerAgentRuns",
                column: "OutputCreativePackageVersionId",
                principalTable: "WeddingPlannerCreativePackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerCreativePackageVersi~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentApprovedCreativePackageVersionId",
                principalTable: "WeddingPlannerCreativePackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerCreativeAssets_WeddingPlannerCreativePackageV~",
                table: "WeddingPlannerCreativeAssets",
                column: "CreativePackageVersionId",
                principalTable: "WeddingPlannerCreativePackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerCreativeAssets_WeddingPlannerCreativeProducti~",
                table: "WeddingPlannerCreativeAssets",
                column: "CreativeProductionJobId",
                principalTable: "WeddingPlannerCreativeProductionJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerCreativePackageDecisions_WeddingPlannerCreati~",
                table: "WeddingPlannerCreativePackageDecisions",
                column: "CreativePackageVersionId",
                principalTable: "WeddingPlannerCreativePackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WPCreativePackages_ProducingJob",
                table: "WeddingPlannerCreativePackageVersions",
                column: "ProducingCreativeProductionJobId",
                principalTable: "WeddingPlannerCreativeProductionJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerCreativePackageVersio~",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerCreativePackageVersi~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_WPCreativeJobs_OutputPackage",
                table: "WeddingPlannerCreativeProductionJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_WPCreativeJobs_RevisionParent",
                table: "WeddingPlannerCreativeProductionJobs");

            migrationBuilder.DropTable(
                name: "WeddingPlannerCreativeAssets");

            migrationBuilder.DropTable(
                name: "WeddingPlannerCreativePackageDecisions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerCreativeRoleContributions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerCreativePackageVersions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerCreativeProductionJobs");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentApprovedCreativePackageVers~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputCreativePackageVersionId",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropColumn(
                name: "CurrentApprovedCreativePackageVersionId",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropColumn(
                name: "OutputCreativePackageVersionId",
                table: "WeddingPlannerAgentRuns");
        }
    }
}
