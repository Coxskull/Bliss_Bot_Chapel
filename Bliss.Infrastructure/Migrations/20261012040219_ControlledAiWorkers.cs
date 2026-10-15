using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ControlledAiWorkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControlledAiBudgetLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Calls = table.Column<int>(type: "integer", nullable: false),
                    ReservedUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledAiBudgetLedgers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ControlledAiDailyUsage",
                columns: table => new
                {
                    Day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Calls = table.Column<int>(type: "integer", nullable: false),
                    ReservedUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledAiDailyUsage", x => x.Day);
                });

            migrationBuilder.CreateTable(
                name: "ControlledAiRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BriefId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachedCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    InputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExecutionConfigSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservedUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ActualUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CostStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OutputText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    OutputImage = table.Column<byte[]>(type: "bytea", nullable: true),
                    OutputSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadlineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledAiRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlledAiRuns_AcademyProductionBriefs_BriefId",
                        column: x => x.BriefId,
                        principalTable: "AcademyProductionBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledAiRuns_AcademyProductionCandidates_AttachedCandid~",
                        column: x => x.AttachedCandidateId,
                        principalTable: "AcademyProductionCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledAiRuns_WeddingPlannerPlanningSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "WeddingPlannerPlanningSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledAiRuns_WeddingPlannerWorkspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledAiEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledAiEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlledAiEvents_ControlledAiRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ControlledAiRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledAiEvents_RunId_Action",
                table: "ControlledAiEvents",
                columns: new[] { "RunId", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlledAiRuns_AttachedCandidateId",
                table: "ControlledAiRuns",
                column: "AttachedCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlledAiRuns_BriefId",
                table: "ControlledAiRuns",
                column: "BriefId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlledAiRuns_SessionId",
                table: "ControlledAiRuns",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlledAiRuns_Status_CreatedAt",
                table: "ControlledAiRuns",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledAiRuns_WorkspaceId_IdempotencyKey",
                table: "ControlledAiRuns",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlledAiBudgetLedgers");

            migrationBuilder.DropTable(
                name: "ControlledAiDailyUsage");

            migrationBuilder.DropTable(
                name: "ControlledAiEvents");

            migrationBuilder.DropTable(
                name: "ControlledAiRuns");
        }
    }
}
