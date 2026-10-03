using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContactRouteAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactRouteAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Authorization = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Adapter = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Transmission = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Notice = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactRouteAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactRouteAudits_IdempotencyKey",
                table: "ContactRouteAudits",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactRouteAudits");
        }
    }
}
