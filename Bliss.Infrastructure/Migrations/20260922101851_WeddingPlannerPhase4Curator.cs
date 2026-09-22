using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerPhase4Curator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApprovedResearchReportVersionId",
                table: "WeddingPlannerWorkspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedRolesJson",
                table: "WeddingPlannerAgentRuns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutputResearchReportVersionId",
                table: "WeddingPlannerAgentRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkerProfileVersion",
                table: "WeddingPlannerAgentRuns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeddingPlannerResearchJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Objective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    QuestionsJson = table.Column<string>(type: "text", nullable: false),
                    Geography = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AllowedDomainsJson = table.Column<string>(type: "text", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResearchProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResearchAdapterVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResearchProviderRequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResearchWorkerKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResearchEstimatedCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SourceCatalogJson = table.Column<string>(type: "text", nullable: true),
                    ResearchStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    EvidenceStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    SynthesisRiskStageOutputJson = table.Column<string>(type: "text", nullable: true),
                    ResearchAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    SynthesisRiskAgentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_WeddingPlannerResearchJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchJobs_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchJobs_WeddingPlannerAgentRuns_Evidence~",
                        column: x => x.EvidenceAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchJobs_WeddingPlannerAgentRuns_Research~",
                        column: x => x.ResearchAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchJobs_WeddingPlannerAgentRuns_Synthesi~",
                        column: x => x.SynthesisRiskAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchJobs_WeddingPlannerBrandDnaVersions_A~",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchJobs_WeddingPlannerColorProfileVersio~",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchJobs_WeddingPlannerWorkspaces_Workspa~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerResearchReportVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProducingResearchJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedColorProfileVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerResearchReportVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportVersions_Advertisers_Advertiser~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportVersions_WeddingPlannerAgentRun~",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportVersions_WeddingPlannerBrandDna~",
                        column: x => x.ApprovedBrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportVersions_WeddingPlannerColorPro~",
                        column: x => x.ApprovedColorProfileVersionId,
                        principalTable: "WeddingPlannerColorProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportVersions_WeddingPlannerResearch~",
                        column: x => x.ProducingResearchJobId,
                        principalTable: "WeddingPlannerResearchJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportVersions_WeddingPlannerWorkspac~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerResearchReportDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerResearchReportDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportDecisions_Advertisers_Advertise~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportDecisions_WeddingPlannerResearc~",
                        column: x => x.ResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchReportDecisions_WeddingPlannerWorkspa~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerResearchRoleContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerResearchRoleContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchRoleContributions_Advertisers_Adverti~",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchRoleContributions_WeddingPlannerAgent~",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchRoleContributions_WeddingPlannerResea~",
                        column: x => x.ResearchJobId,
                        principalTable: "WeddingPlannerResearchJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchRoleContributions_WeddingPlannerRese~1",
                        column: x => x.ResearchReportVersionId,
                        principalTable: "WeddingPlannerResearchReportVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerResearchRoleContributions_WeddingPlannerWorks~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentApprovedResearchReportVersi~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentApprovedResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputResearchReportVersionId",
                table: "WeddingPlannerAgentRuns",
                column: "OutputResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_AdvertiserId",
                table: "WeddingPlannerResearchJobs",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_ApprovedBrandDnaVersionId",
                table: "WeddingPlannerResearchJobs",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_ApprovedColorProfileVersionId",
                table: "WeddingPlannerResearchJobs",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_EvidenceAgentRunId",
                table: "WeddingPlannerResearchJobs",
                column: "EvidenceAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_OutputResearchReportVersionId",
                table: "WeddingPlannerResearchJobs",
                column: "OutputResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_ResearchAgentRunId",
                table: "WeddingPlannerResearchJobs",
                column: "ResearchAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerResearchJobs",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_StartedAt",
                table: "WeddingPlannerResearchJobs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_SynthesisRiskAgentRunId",
                table: "WeddingPlannerResearchJobs",
                column: "SynthesisRiskAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchJobs_WorkspaceId",
                table: "WeddingPlannerResearchJobs",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportDecisions_AdvertiserId",
                table: "WeddingPlannerResearchReportDecisions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportDecisions_OccurredAt",
                table: "WeddingPlannerResearchReportDecisions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportDecisions_ResearchReportVersion~",
                table: "WeddingPlannerResearchReportDecisions",
                column: "ResearchReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportDecisions_SourceSystem_Idempote~",
                table: "WeddingPlannerResearchReportDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportDecisions_WorkspaceId",
                table: "WeddingPlannerResearchReportDecisions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_AdvertiserId",
                table: "WeddingPlannerResearchReportVersions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_ApprovedBrandDnaVersio~",
                table: "WeddingPlannerResearchReportVersions",
                column: "ApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_ApprovedColorProfileVe~",
                table: "WeddingPlannerResearchReportVersions",
                column: "ApprovedColorProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_CreatedAt",
                table: "WeddingPlannerResearchReportVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_ProducingAgentRunId",
                table: "WeddingPlannerResearchReportVersions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_ProducingResearchJobId",
                table: "WeddingPlannerResearchReportVersions",
                column: "ProducingResearchJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_SourceSystem_Idempoten~",
                table: "WeddingPlannerResearchReportVersions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_WorkspaceId",
                table: "WeddingPlannerResearchReportVersions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchReportVersions_WorkspaceId_VersionNum~",
                table: "WeddingPlannerResearchReportVersions",
                columns: new[] { "WorkspaceId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchRoleContributions_AdvertiserId",
                table: "WeddingPlannerResearchRoleContributions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchRoleContributions_CreatedAt",
                table: "WeddingPlannerResearchRoleContributions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchRoleContributions_ProducingAgentRunId",
                table: "WeddingPlannerResearchRoleContributions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchRoleContributions_ResearchJobId",
                table: "WeddingPlannerResearchRoleContributions",
                column: "ResearchJobId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchRoleContributions_ResearchReportVersi~",
                table: "WeddingPlannerResearchRoleContributions",
                columns: new[] { "ResearchReportVersionId", "LogicalRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerResearchRoleContributions_WorkspaceId",
                table: "WeddingPlannerResearchRoleContributions",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerResearchReportVersion~",
                table: "WeddingPlannerAgentRuns",
                column: "OutputResearchReportVersionId",
                principalTable: "WeddingPlannerResearchReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerResearchReportVersio~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentApprovedResearchReportVersionId",
                principalTable: "WeddingPlannerResearchReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerResearchJobs_WeddingPlannerResearchReportVers~",
                table: "WeddingPlannerResearchJobs",
                column: "OutputResearchReportVersionId",
                principalTable: "WeddingPlannerResearchReportVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerResearchReportVersion~",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerResearchReportVersio~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerResearchJobs_WeddingPlannerResearchReportVers~",
                table: "WeddingPlannerResearchJobs");

            migrationBuilder.DropTable(
                name: "WeddingPlannerResearchReportDecisions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerResearchRoleContributions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerResearchReportVersions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerResearchJobs");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentApprovedResearchReportVersi~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputResearchReportVersionId",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropColumn(
                name: "CurrentApprovedResearchReportVersionId",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropColumn(
                name: "AssignedRolesJson",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropColumn(
                name: "OutputResearchReportVersionId",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropColumn(
                name: "WorkerProfileVersion",
                table: "WeddingPlannerAgentRuns");
        }
    }
}
