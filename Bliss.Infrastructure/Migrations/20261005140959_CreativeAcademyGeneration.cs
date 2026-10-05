using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreativeAcademyGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreativeAcademyGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Family = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TeacherKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipeSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ModelCalls = table.Column<int>(type: "integer", nullable: false),
                    CampaignReady = table.Column<bool>(type: "boolean", nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreativeAcademyGenerations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreativeAcademyGenerations_RecordedAt",
                table: "CreativeAcademyGenerations",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CreativeAcademyGenerations_RequestKey",
                table: "CreativeAcademyGenerations",
                column: "RequestKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreativeAcademyGenerations");
        }
    }
}
