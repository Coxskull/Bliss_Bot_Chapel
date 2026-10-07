using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreativeGenerationProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "CreativeAcademyGenerations",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostStatus",
                table: "CreativeAcademyGenerations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "CreativeAcademyGenerations",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "CreativeAcademyGenerations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "CreativeAcademyGenerations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cost",
                table: "CreativeAcademyGenerations");

            migrationBuilder.DropColumn(
                name: "CostStatus",
                table: "CreativeAcademyGenerations");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "CreativeAcademyGenerations");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "CreativeAcademyGenerations");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "CreativeAcademyGenerations");
        }
    }
}
