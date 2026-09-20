using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6HumanReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchEvaluationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReviewerLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResultingMatchStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InputSnapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchReviewDecisions_BlissMatches_BlissMatchId",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchReviewDecisions_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchReviewDecisions_MatchEvaluationRuns_MatchEvaluationRun~",
                        column: x => x.MatchEvaluationRunId,
                        principalTable: "MatchEvaluationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchReviewDecisions_BlissMatchId",
                table: "MatchReviewDecisions",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReviewDecisions_CompletedAt",
                table: "MatchReviewDecisions",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReviewDecisions_CreatorId",
                table: "MatchReviewDecisions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReviewDecisions_MatchEvaluationRunId",
                table: "MatchReviewDecisions",
                column: "MatchEvaluationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReviewDecisions_SourceSystem_IdempotencyKey",
                table: "MatchReviewDecisions",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchReviewDecisions");
        }
    }
}
