using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhaseAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EconomicsPhaseAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhaseKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    HistoryLine = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RepricingAuthorized = table.Column<bool>(type: "boolean", nullable: false),
                    SettlementAuthorized = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendationRewritten = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicsPhaseAcceptances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomicsPhaseAcceptances_PhaseKey",
                table: "EconomicsPhaseAcceptances",
                column: "PhaseKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EconomicsPhaseAcceptances");
        }
    }
}
