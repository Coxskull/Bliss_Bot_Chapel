using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EconomicsPhase2CreatorSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatorAudienceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeographicMarketId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResearchSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Subscribers = table.Column<int>(type: "integer", nullable: true),
                    FemalePercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    MalePercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    PrimaryAgeRange = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PrimaryGeography = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorAudienceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorAudienceSnapshots_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorAudienceSnapshots_GeographicMarkets_GeographicMarket~",
                        column: x => x.GeographicMarketId,
                        principalTable: "GeographicMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorAudienceSnapshots_ResearchSources_ResearchSourceId",
                        column: x => x.ResearchSourceId,
                        principalTable: "ResearchSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatorPerformanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResearchSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AverageViews = table.Column<int>(type: "integer", nullable: true),
                    DailyViews = table.Column<int>(type: "integer", nullable: true),
                    WeeklyViews = table.Column<int>(type: "integer", nullable: true),
                    MonthlyViews = table.Column<int>(type: "integer", nullable: true),
                    HistoricalReach = table.Column<int>(type: "integer", nullable: true),
                    EngagementRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RetentionRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    PublishingFrequencyPerWeek = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContentFormat = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorPerformanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorPerformanceSnapshots_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorPerformanceSnapshots_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorPerformanceSnapshots_ResearchSources_ResearchSourceId",
                        column: x => x.ResearchSourceId,
                        principalTable: "ResearchSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorAudienceSnapshots_CreatorId_CapturedAt",
                table: "CreatorAudienceSnapshots",
                columns: new[] { "CreatorId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorAudienceSnapshots_GeographicMarketId",
                table: "CreatorAudienceSnapshots",
                column: "GeographicMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorAudienceSnapshots_ResearchSourceId",
                table: "CreatorAudienceSnapshots",
                column: "ResearchSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorPerformanceSnapshots_ContentItemId",
                table: "CreatorPerformanceSnapshots",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorPerformanceSnapshots_CreatorId_CapturedAt",
                table: "CreatorPerformanceSnapshots",
                columns: new[] { "CreatorId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorPerformanceSnapshots_ResearchSourceId",
                table: "CreatorPerformanceSnapshots",
                column: "ResearchSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatorAudienceSnapshots");

            migrationBuilder.DropTable(
                name: "CreatorPerformanceSnapshots");
        }
    }
}
