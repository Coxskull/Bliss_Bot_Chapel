using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LearningLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProspectSlug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Body = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    LaboratoryGraduated = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorizedTraffic = table.Column<bool>(type: "boolean", nullable: false),
                    ProductionChanged = table.Column<bool>(type: "boolean", nullable: false),
                    BehaviorChanged = table.Column<bool>(type: "boolean", nullable: false),
                    ModelCalls = table.Column<int>(type: "integer", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningNotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningNotes_ProspectSlug_IdempotencyKey",
                table: "LearningNotes",
                columns: new[] { "ProspectSlug", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningNotes");
        }
    }
}
