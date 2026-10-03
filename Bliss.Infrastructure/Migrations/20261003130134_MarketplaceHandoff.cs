using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MarketplaceHandoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketplaceHandoffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MatchStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OverallScore = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    EvaluatorInvoked = table.Column<bool>(type: "boolean", nullable: false),
                    WinClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceHandoffs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceHandoffs_TenantKey_CreatorId_SourceUrl",
                table: "MarketplaceHandoffs",
                columns: new[] { "TenantKey", "CreatorId", "SourceUrl" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceHandoffs");
        }
    }
}
