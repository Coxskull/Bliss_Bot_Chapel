using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClosedModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClosedModelReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadingKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConfiguredModels = table.Column<int>(type: "integer", nullable: false),
                    ModelCalls = table.Column<int>(type: "integer", nullable: false),
                    NoteCount = table.Column<int>(type: "integer", nullable: false),
                    LaboratoryPassed = table.Column<bool>(type: "boolean", nullable: false),
                    PassedCount = table.Column<int>(type: "integer", nullable: false),
                    ScenarioCount = table.Column<int>(type: "integer", nullable: false),
                    ModelsConfigured = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorizedTraffic = table.Column<bool>(type: "boolean", nullable: false),
                    ProductionChanged = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosedModelReadings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosedModelReadings_ReadingKey",
                table: "ClosedModelReadings",
                column: "ReadingKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosedModelReadings");
        }
    }
}
