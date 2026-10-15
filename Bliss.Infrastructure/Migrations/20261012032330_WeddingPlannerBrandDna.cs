using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WeddingPlannerBrandDna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeddingPlannerBrandDna",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Niche = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Market = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Language = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Colors = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Font = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Goals = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Restrictions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingPlannerBrandDna", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingPlannerBrandDna_WeddingPlannerWorkspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDna_WorkspaceId_IdempotencyKey",
                table: "WeddingPlannerBrandDna",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingPlannerBrandDna_WorkspaceId_Version",
                table: "WeddingPlannerBrandDna",
                columns: new[] { "WorkspaceId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeddingPlannerBrandDna");
        }
    }
}
