using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreativeAcademy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreativeAcademyDna",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DnaKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Family = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Hero = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Palette = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cta = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Personality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TeacherKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TeacherOnFile = table.Column<bool>(type: "boolean", nullable: false),
                    Distinct = table.Column<bool>(type: "boolean", nullable: false),
                    ModelCalls = table.Column<int>(type: "integer", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreativeAcademyDna", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreativeAcademyLessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Family = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProperNouns = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Critical = table.Column<bool>(type: "boolean", nullable: false),
                    VisualRecorded = table.Column<bool>(type: "boolean", nullable: false),
                    Defects = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PreserveList = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RepairList = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ModelCalls = table.Column<int>(type: "integer", nullable: false),
                    CampaignReady = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreativeAcademyLessons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreativeAcademyDna_DnaKey",
                table: "CreativeAcademyDna",
                column: "DnaKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreativeAcademyLessons_LessonKey",
                table: "CreativeAcademyLessons",
                column: "LessonKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreativeAcademyDna");

            migrationBuilder.DropTable(
                name: "CreativeAcademyLessons");
        }
    }
}
