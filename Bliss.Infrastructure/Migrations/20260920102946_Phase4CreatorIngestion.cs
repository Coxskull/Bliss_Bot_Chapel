using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4CreatorIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityKey",
                table: "CreatorPlatforms",
                type: "character varying(384)",
                maxLength: 384,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "CreatorPlatforms"
                SET "IdentityKey" = upper(trim("Platform")) || '::' || trim("ExternalProfileId")
                WHERE "ExternalProfileId" IS NOT NULL
                  AND trim("ExternalProfileId") <> '';
                """);

            migrationBuilder.CreateTable(
                name: "CreatorIngestionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdentityKey = table.Column<string>(type: "character varying(384)", maxLength: 384, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InputSnapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorIngestionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorIngestionRuns_CreatorPlatforms_CreatorPlatformId",
                        column: x => x.CreatorPlatformId,
                        principalTable: "CreatorPlatforms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorIngestionRuns_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorPlatforms_IdentityKey",
                table: "CreatorPlatforms",
                column: "IdentityKey",
                unique: true,
                filter: "\"IdentityKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorIngestionRuns_CompletedAt",
                table: "CreatorIngestionRuns",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorIngestionRuns_CreatorId",
                table: "CreatorIngestionRuns",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorIngestionRuns_CreatorPlatformId",
                table: "CreatorIngestionRuns",
                column: "CreatorPlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorIngestionRuns_IdentityKey",
                table: "CreatorIngestionRuns",
                column: "IdentityKey");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorIngestionRuns_SourceSystem_IdempotencyKey",
                table: "CreatorIngestionRuns",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatorIngestionRuns");

            migrationBuilder.DropIndex(
                name: "IX_CreatorPlatforms_IdentityKey",
                table: "CreatorPlatforms");

            migrationBuilder.DropColumn(
                name: "IdentityKey",
                table: "CreatorPlatforms");
        }
    }
}
