using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreativeApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreativeApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CampaignReady = table.Column<bool>(type: "boolean", nullable: false),
                    MatchWritten = table.Column<bool>(type: "boolean", nullable: false),
                    PriceInvented = table.Column<bool>(type: "boolean", nullable: false),
                    ModelCalls = table.Column<int>(type: "integer", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreativeApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreativeApprovals_WeddingPlannerWorkspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "WeddingPlannerWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreativeApprovals_WorkspaceId_IdempotencyKey",
                table: "CreativeApprovals",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreativeApprovals");
        }
    }
}
