using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MissionControlEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvidencePackages",
                columns: table => new
                {
                    EvidenceId = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ManifestJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ParentEvidenceId = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ReviewStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DriveStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidencePackages", x => x.EvidenceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvidencePackages_CreatedAt",
                table: "EvidencePackages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EvidencePackages_ParentEvidenceId",
                table: "EvidencePackages",
                column: "ParentEvidenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvidencePackages");
        }
    }
}
