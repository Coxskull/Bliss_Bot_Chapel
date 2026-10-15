using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CampaignMeasurementLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandoffId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EvidenceStatus = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WindowEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Impressions = table.Column<long>(type: "bigint", nullable: true),
                    Clicks = table.Column<long>(type: "bigint", nullable: true),
                    Conversions = table.Column<long>(type: "bigint", nullable: true),
                    LineageSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    LineageSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignObservations_CreativeCampaignHandoffs_HandoffId",
                        column: x => x.HandoffId,
                        principalTable: "CreativeCampaignHandoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignObservations_WeddingPlannerWorkspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignLearningProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hypothesis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Experiment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SuccessCriterion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignLearningProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignLearningProposals_CampaignObservations_ObservationId",
                        column: x => x.ObservationId,
                        principalTable: "CampaignObservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignLearningProposals_WeddingPlannerWorkspaces_Workspac~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignLearningReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignLearningReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignLearningReviews_CampaignLearningProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "CampaignLearningProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLearningProposals_ObservationId",
                table: "CampaignLearningProposals",
                column: "ObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLearningProposals_WorkspaceId_IdempotencyKey",
                table: "CampaignLearningProposals",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLearningReviews_ProposalId_IdempotencyKey",
                table: "CampaignLearningReviews",
                columns: new[] { "ProposalId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLearningReviews_ProposalId_Version",
                table: "CampaignLearningReviews",
                columns: new[] { "ProposalId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignObservations_HandoffId",
                table: "CampaignObservations",
                column: "HandoffId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignObservations_WorkspaceId_IdempotencyKey",
                table: "CampaignObservations",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignLearningReviews");

            migrationBuilder.DropTable(
                name: "CampaignLearningProposals");

            migrationBuilder.DropTable(
                name: "CampaignObservations");
        }
    }
}
