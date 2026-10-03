using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BatchMeasurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatchMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StoredProspects = table.Column<int>(type: "integer", nullable: false),
                    ElapsedMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    WorkingSetBytes = table.Column<long>(type: "bigint", nullable: true),
                    ResourceLine = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CostLine = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Retries = table.Column<int>(type: "integer", nullable: false),
                    PartialFailures = table.Column<int>(type: "integer", nullable: false),
                    Recovered = table.Column<bool>(type: "boolean", nullable: false),
                    Leakage = table.Column<bool>(type: "boolean", nullable: false),
                    HostedAcceptanceClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    FactoryTargetClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    CensusClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    Notice = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    Failures = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchMeasurements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchMeasurements_IdempotencyKey",
                table: "BatchMeasurements",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchMeasurements");
        }
    }
}
