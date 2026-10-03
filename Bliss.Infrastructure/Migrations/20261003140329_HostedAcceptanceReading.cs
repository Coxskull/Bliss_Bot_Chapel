using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HostedAcceptanceReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostedAcceptanceReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadingKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EnvironmentName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProductionGatesApplied = table.Column<bool>(type: "boolean", nullable: false),
                    HostedDatabaseConfigured = table.Column<bool>(type: "boolean", nullable: false),
                    DatabaseServerCertificateVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityProviderHttps = table.Column<bool>(type: "boolean", nullable: false),
                    BackupDeclared = table.Column<bool>(type: "boolean", nullable: false),
                    SecretMaterialExternal = table.Column<bool>(type: "boolean", nullable: false),
                    RoleClaimsDistinct = table.Column<bool>(type: "boolean", nullable: false),
                    HostedAcceptanceClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityContacted = table.Column<bool>(type: "boolean", nullable: false),
                    BackupDrillRun = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostedAcceptanceReadings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostedAcceptanceReadings_ReadingKey",
                table: "HostedAcceptanceReadings",
                column: "ReadingKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostedAcceptanceReadings");
        }
    }
}
