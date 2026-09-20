using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3EvaluationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BlissMatchId",
                table: "MatchEvaluationRuns",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ConfidenceScore",
                table: "MatchEvaluationRuns",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchStatus",
                table: "MatchEvaluationRuns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputSnapshot",
                table: "MatchEvaluationRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallScore",
                table: "MatchEvaluationRuns",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvaluationRuns_BlissMatchId",
                table: "MatchEvaluationRuns",
                column: "BlissMatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchEvaluationRuns_BlissMatches_BlissMatchId",
                table: "MatchEvaluationRuns",
                column: "BlissMatchId",
                principalTable: "BlissMatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchEvaluationRuns_BlissMatches_BlissMatchId",
                table: "MatchEvaluationRuns");

            migrationBuilder.DropIndex(
                name: "IX_MatchEvaluationRuns_BlissMatchId",
                table: "MatchEvaluationRuns");

            migrationBuilder.DropColumn(
                name: "BlissMatchId",
                table: "MatchEvaluationRuns");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                table: "MatchEvaluationRuns");

            migrationBuilder.DropColumn(
                name: "MatchStatus",
                table: "MatchEvaluationRuns");

            migrationBuilder.DropColumn(
                name: "OutputSnapshot",
                table: "MatchEvaluationRuns");

            migrationBuilder.DropColumn(
                name: "OverallScore",
                table: "MatchEvaluationRuns");
        }
    }
}
