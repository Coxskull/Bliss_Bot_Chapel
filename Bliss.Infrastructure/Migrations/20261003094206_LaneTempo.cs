using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LaneTempo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LaneTempoAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Lane = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tempo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CeilingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CeilingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaneTempoAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaneTempoStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Lane = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tempo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CeilingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CeilingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaneTempoStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaneTempoAudits_RecordedAt",
                table: "LaneTempoAudits",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LaneTempoStates_Lane",
                table: "LaneTempoStates",
                column: "Lane",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaneTempoAudits");

            migrationBuilder.DropTable(
                name: "LaneTempoStates");
        }
    }
}
