using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5MatchFormation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchFormationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvaluateOnCreate = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InputSnapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchFormationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchFormationRuns_AdvertiserOpportunities_AdvertiserOpport~",
                        column: x => x.AdvertiserOpportunityId,
                        principalTable: "AdvertiserOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchFormationRuns_BlissMatches_BlissMatchId",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchFormationRuns_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchFormationRuns_RuleVersions_RuleVersionId",
                        column: x => x.RuleVersionId,
                        principalTable: "RuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchFormationRuns_AdvertiserOpportunityId",
                table: "MatchFormationRuns",
                column: "AdvertiserOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchFormationRuns_BlissMatchId",
                table: "MatchFormationRuns",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchFormationRuns_CompletedAt",
                table: "MatchFormationRuns",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatchFormationRuns_CreatorId",
                table: "MatchFormationRuns",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchFormationRuns_RuleVersionId",
                table: "MatchFormationRuns",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchFormationRuns_SourceSystem_IdempotencyKey",
                table: "MatchFormationRuns",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchFormationRuns");
        }
    }
}
