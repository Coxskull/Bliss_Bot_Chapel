using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerPhase5ConceptWorkshop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApprovedConceptPackageVersionId",
                table: "WeddingPlannerWorkspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputConceptPackageVersionId",
                table: "WeddingPlannerAgentRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeddingPlannerConceptPackageDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedConceptId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerConceptPackageDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptPackageDecisions_Advertisers_Advertise~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptPackageDecisions_WeddingPlannerWorkspa~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerConceptPackageVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProducingWorkshopJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ChannelFormat = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanvasWidth = table.Column<int>(type: "integer", nullable: false),
                    CanvasHeight = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerConceptPackageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptPackageVersions_Advertisers_Advertiser~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptPackageVersions_WeddingPlannerAgentRun~",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptPackageVersions_WeddingPlannerBrandDna~",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptPackageVersions_WeddingPlannerColorPro~",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptPackageVersions_WeddingPlannerResearch~",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptPackageVersions_WeddingPlannerWorkspac~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerWorkshopJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Objective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CampaignGoal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AudienceFocus = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChannelFormat = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanvasWidth = table.Column<int>(type: "integer", nullable: false),
                    CanvasHeight = table.Column<int>(type: "integer", nullable: false),
                    DeliverablesJson = table.Column<string>(type: "text", nullable: false),
                    Cta = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConstraintsJson = table.Column<string>(type: "text", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ApprovedResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedResearchReportVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    StrategyStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    CreativeStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    ProductionStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    StrategyAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreativeAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductionAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputConceptPackageVersionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_WeddingPlannerWorkshopJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerAgentRuns_Creative~",
                        column: x => x.CreativeAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerAgentRuns_Producti~",
                        column: x => x.ProductionAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerAgentRuns_Strategy~",
                        column: x => x.StrategyAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerBrandDnaVersions_A~",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerColorProfileVersio~",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerConceptPackageVers~",
                        column: x => x.OutputConceptPackageVersionId,
                        principalTable: "WeddingPlannerConceptPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerResearchReportVers~",
                        column: x => x.ApprovedResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerWorkspaces_Workspa~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerConceptRoleContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerConceptRoleContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptRoleContributions_Advertisers_Advertis~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptRoleContributions_WeddingPlannerAgentR~",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptRoleContributions_WeddingPlannerConcep~",
                        column: x => x.ConceptPackageVersionId,
                        principalTable: "WeddingPlannerConceptPackageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptRoleContributions_WeddingPlannerWorksh~",
                        column: x => x.WorkshopJobId,
                        principalTable: "WeddingPlannerWorkshopJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerConceptRoleContributions_WeddingPlannerWorksp~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentApprovedConceptPackageVersi~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentApprovedConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputConceptPackageVersionId",
                table: "WeddingPlannerAgentRuns",
                column: "OutputConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageDecisions_AdvertiserId",
                table: "WeddingPlannerConceptPackageDecisions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageDecisions_ConceptPackageVersion~",
                table: "WeddingPlannerConceptPackageDecisions",
                column: "ConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageDecisions_OccurredAt",
                table: "WeddingPlannerConceptPackageDecisions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageDecisions_SourceSystem_Idempote~",
                table: "WeddingPlannerConceptPackageDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageDecisions_WorkspaceId",
                table: "WeddingPlannerConceptPackageDecisions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_AdvertiserId",
                table: "WeddingPlannerConceptPackageVersions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_ApprovedBrandDnaVersio~",
                table: "WeddingPlannerConceptPackageVersions",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_ApprovedColorProfileVe~",
                table: "WeddingPlannerConceptPackageVersions",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_ApprovedResearchReport~",
                table: "WeddingPlannerConceptPackageVersions",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_CreatedAt",
                table: "WeddingPlannerConceptPackageVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_ProducingAgentRunId",
                table: "WeddingPlannerConceptPackageVersions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_ProducingWorkshopJobId",
                table: "WeddingPlannerConceptPackageVersions",
                column: "ProducingWorkshopJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_SourceSystem_Idempoten~",
                table: "WeddingPlannerConceptPackageVersions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_WorkspaceId",
                table: "WeddingPlannerConceptPackageVersions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptPackageVersions_WorkspaceId_VersionNum~",
                table: "WeddingPlannerConceptPackageVersions",
                columns: new[] { "WorkspaceId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptRoleContributions_AdvertiserId",
                table: "WeddingPlannerConceptRoleContributions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptRoleContributions_ConceptPackageVersio~",
                table: "WeddingPlannerConceptRoleContributions",
                columns: new[] { "ConceptPackageVersionId", "LogicalRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptRoleContributions_CreatedAt",
                table: "WeddingPlannerConceptRoleContributions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptRoleContributions_ProducingAgentRunId",
                table: "WeddingPlannerConceptRoleContributions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptRoleContributions_WorkshopJobId",
                table: "WeddingPlannerConceptRoleContributions",
                column: "WorkshopJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerConceptRoleContributions_WorkspaceId",
                table: "WeddingPlannerConceptRoleContributions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_AdvertiserId",
                table: "WeddingPlannerWorkshopJobs",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_ApprovedBrandDnaVersionId",
                table: "WeddingPlannerWorkshopJobs",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_ApprovedColorProfileVersionId",
                table: "WeddingPlannerWorkshopJobs",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_ApprovedResearchReportVersionId",
                table: "WeddingPlannerWorkshopJobs",
                column: "ApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_CreativeAgentRunId",
                table: "WeddingPlannerWorkshopJobs",
                column: "CreativeAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_OutputConceptPackageVersionId",
                table: "WeddingPlannerWorkshopJobs",
                column: "OutputConceptPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_ProductionAgentRunId",
                table: "WeddingPlannerWorkshopJobs",
                column: "ProductionAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerWorkshopJobs",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_StartedAt",
                table: "WeddingPlannerWorkshopJobs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_StrategyAgentRunId",
                table: "WeddingPlannerWorkshopJobs",
                column: "StrategyAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkshopJobs_WorkspaceId",
                table: "WeddingPlannerWorkshopJobs",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerConceptPackageVersion~",
                table: "WeddingPlannerAgentRuns",
                column: "OutputConceptPackageVersionId",
                principalTable: "WeddingPlannerConceptPackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerConceptPackageVersio~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentApprovedConceptPackageVersionId",
                principalTable: "WeddingPlannerConceptPackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerConceptPackageDecisions_WeddingPlannerConcept~",
                table: "WeddingPlannerConceptPackageDecisions",
                column: "ConceptPackageVersionId",
                principalTable: "WeddingPlannerConceptPackageVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerConceptPackageVersions_WeddingPlannerWorkshop~",
                table: "WeddingPlannerConceptPackageVersions",
                column: "ProducingWorkshopJobId",
                principalTable: "WeddingPlannerWorkshopJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerConceptPackageVersion~",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerConceptPackageVersio~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerWorkshopJobs_WeddingPlannerConceptPackageVers~",
                table: "WeddingPlannerWorkshopJobs");

            migrationBuilder.DropTable(
                name: "WeddingPlannerConceptPackageDecisions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerConceptRoleContributions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerConceptPackageVersions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerWorkshopJobs");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentApprovedConceptPackageVersi~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputConceptPackageVersionId",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropColumn(
                name: "CurrentApprovedConceptPackageVersionId",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropColumn(
                name: "OutputConceptPackageVersionId",
                table: "WeddingPlannerAgentRuns");
        }
    }
}
