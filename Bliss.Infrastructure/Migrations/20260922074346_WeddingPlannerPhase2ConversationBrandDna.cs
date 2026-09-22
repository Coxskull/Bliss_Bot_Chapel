using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerPhase2ConversationBrandDna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApprovedBrandDnaVersionId",
                table: "WeddingPlannerWorkspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeddingPlannerAgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LogicalRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkerKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PromptPackVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AdapterVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TriggerMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputBrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PromptTokens = table.Column<int>(type: "integer", nullable: true),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerAgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAgentRuns_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAgentRuns_WeddingPlannerConversationMessages_~",
                        column: x => x.OutputMessageId,
                        principalTable: "WeddingPlannerConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAgentRuns_WeddingPlannerConversationMessages~1",
                        column: x => x.TriggerMessageId,
                        principalTable: "WeddingPlannerConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAgentRuns_WeddingPlannerPlanningSessions_Sess~",
                        column: x => x.SessionId,
                        principalTable: "WeddingPlannerPlanningSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerAgentRuns_WeddingPlannerWorkspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerBrandDnaVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentJson = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProducingAgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerBrandDnaVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerBrandDnaVersions_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerBrandDnaVersions_WeddingPlannerAgentRuns_Prod~",
                        column: x => x.ProducingAgentRunId,
                        principalTable: "WeddingPlannerAgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerBrandDnaVersions_WeddingPlannerWorkspaces_Wor~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingPlannerBrandDnaDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandDnaVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerBrandDnaDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerBrandDnaDecisions_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerBrandDnaDecisions_WeddingPlannerBrandDnaVersi~",
                        column: x => x.BrandDnaVersionId,
                        principalTable: "WeddingPlannerBrandDnaVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerBrandDnaDecisions_WeddingPlannerWorkspaces_Wo~",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentApprovedBrandDnaVersionId",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentApprovedBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_AdvertiserId",
                table: "WeddingPlannerAgentRuns",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputBrandDnaVersionId",
                table: "WeddingPlannerAgentRuns",
                column: "OutputBrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_OutputMessageId",
                table: "WeddingPlannerAgentRuns",
                column: "OutputMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_SessionId",
                table: "WeddingPlannerAgentRuns",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerAgentRuns",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_StartedAt",
                table: "WeddingPlannerAgentRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_TriggerMessageId",
                table: "WeddingPlannerAgentRuns",
                column: "TriggerMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerAgentRuns_WorkspaceId",
                table: "WeddingPlannerAgentRuns",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaDecisions_AdvertiserId",
                table: "WeddingPlannerBrandDnaDecisions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaDecisions_BrandDnaVersionId",
                table: "WeddingPlannerBrandDnaDecisions",
                column: "BrandDnaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaDecisions_OccurredAt",
                table: "WeddingPlannerBrandDnaDecisions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaDecisions_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerBrandDnaDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaDecisions_WorkspaceId",
                table: "WeddingPlannerBrandDnaDecisions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaVersions_AdvertiserId",
                table: "WeddingPlannerBrandDnaVersions",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaVersions_CreatedAt",
                table: "WeddingPlannerBrandDnaVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaVersions_ProducingAgentRunId",
                table: "WeddingPlannerBrandDnaVersions",
                column: "ProducingAgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaVersions_SourceSystem_IdempotencyKey",
                table: "WeddingPlannerBrandDnaVersions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaVersions_WorkspaceId",
                table: "WeddingPlannerBrandDnaVersions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDnaVersions_WorkspaceId_VersionNumber",
                table: "WeddingPlannerBrandDnaVersions",
                columns: new[] { "WorkspaceId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerBrandDnaVersions_Cur~",
                table: "WeddingPlannerWorkspaces",
                column: "CurrentApprovedBrandDnaVersionId",
                principalTable: "WeddingPlannerBrandDnaVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerBrandDnaVersions_Outp~",
                table: "WeddingPlannerAgentRuns",
                column: "OutputBrandDnaVersionId",
                principalTable: "WeddingPlannerBrandDnaVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerWorkspaces_WeddingPlannerBrandDnaVersions_Cur~",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_WeddingPlannerAgentRuns_WeddingPlannerBrandDnaVersions_Outp~",
                table: "WeddingPlannerAgentRuns");

            migrationBuilder.DropTable(
                name: "WeddingPlannerBrandDnaDecisions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerBrandDnaVersions");

            migrationBuilder.DropTable(
                name: "WeddingPlannerAgentRuns");

            migrationBuilder.DropIndex(
                name: "IX_WeddingPlannerWorkspaces_CurrentApprovedBrandDnaVersionId",
                table: "WeddingPlannerWorkspaces");

            migrationBuilder.DropColumn(
                name: "CurrentApprovedBrandDnaVersionId",
                table: "WeddingPlannerWorkspaces");
        }
    }
}
