using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AcademyProductionWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademyProductionBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandDnaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Family = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Headline = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Cta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReferenceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReferenceSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    BrandSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ProductionBriefJson = table.Column<string>(type: "text", nullable: false),
                    RecipeSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademyProductionBriefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademyProductionBriefs_WeddingPlannerBrandDna_BrandDnaId",
                        column: x => x.BrandDnaId,
                        principalTable: "WeddingPlannerBrandDna",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademyProductionBriefs_WeddingPlannerWorkspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcademyProductionCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BriefId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImageSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImageBytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademyProductionCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademyProductionCandidates_AcademyProductionBriefs_BriefId",
                        column: x => x.BriefId,
                        principalTable: "AcademyProductionBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcademyProductionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Customization = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QualityParity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Originality = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GeographicAuthenticity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CriticalDefect = table.Column<bool>(type: "boolean", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Preserve = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Repair = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademyProductionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademyProductionReviews_AcademyProductionCandidates_Candid~",
                        column: x => x.CandidateId,
                        principalTable: "AcademyProductionCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademyProductionBriefs_BrandDnaId",
                table: "AcademyProductionBriefs",
                column: "BrandDnaId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademyProductionBriefs_WorkspaceId_IdempotencyKey",
                table: "AcademyProductionBriefs",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademyProductionCandidates_BriefId_IdempotencyKey",
                table: "AcademyProductionCandidates",
                columns: new[] { "BriefId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademyProductionCandidates_BriefId_Version",
                table: "AcademyProductionCandidates",
                columns: new[] { "BriefId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademyProductionReviews_CandidateId_IdempotencyKey",
                table: "AcademyProductionReviews",
                columns: new[] { "CandidateId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademyProductionReviews_CandidateId_Version",
                table: "AcademyProductionReviews",
                columns: new[] { "CandidateId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademyProductionReviews");

            migrationBuilder.DropTable(
                name: "AcademyProductionCandidates");

            migrationBuilder.DropTable(
                name: "AcademyProductionBriefs");
        }
    }
}
