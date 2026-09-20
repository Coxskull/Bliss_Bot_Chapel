using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase7CampaignPlacementBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdvertiserOpportunityId",
                table: "Campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BlissMatchId",
                table: "CampaignPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CampaignPlacementRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignPlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdInventorySlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OperatorLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InputSnapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPlacementRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignPlacementRuns_AdInventorySlots_AdInventorySlotId",
                        column: x => x.AdInventorySlotId,
                        principalTable: "AdInventorySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlacementRuns_AdvertiserOpportunities_AdvertiserOpp~",
                        column: x => x.AdvertiserOpportunityId,
                        principalTable: "AdvertiserOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlacementRuns_BlissMatches_BlissMatchId",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlacementRuns_CampaignPlacements_CampaignPlacementId",
                        column: x => x.CampaignPlacementId,
                        principalTable: "CampaignPlacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlacementRuns_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlacementRuns_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlacementRuns_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_AdvertiserOpportunityId",
                table: "Campaigns",
                column: "AdvertiserOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacements_BlissMatchId",
                table: "CampaignPlacements",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_AdInventorySlotId",
                table: "CampaignPlacementRuns",
                column: "AdInventorySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_AdvertiserOpportunityId",
                table: "CampaignPlacementRuns",
                column: "AdvertiserOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_BlissMatchId",
                table: "CampaignPlacementRuns",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_CampaignId",
                table: "CampaignPlacementRuns",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_CampaignPlacementId",
                table: "CampaignPlacementRuns",
                column: "CampaignPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_CompletedAt",
                table: "CampaignPlacementRuns",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_ContentItemId",
                table: "CampaignPlacementRuns",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_CreatorId",
                table: "CampaignPlacementRuns",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacementRuns_SourceSystem_IdempotencyKey",
                table: "CampaignPlacementRuns",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignPlacements_BlissMatches_BlissMatchId",
                table: "CampaignPlacements",
                column: "BlissMatchId",
                principalTable: "BlissMatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Campaigns_AdvertiserOpportunities_AdvertiserOpportunityId",
                table: "Campaigns",
                column: "AdvertiserOpportunityId",
                principalTable: "AdvertiserOpportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignPlacements_BlissMatches_BlissMatchId",
                table: "CampaignPlacements");

            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_AdvertiserOpportunities_AdvertiserOpportunityId",
                table: "Campaigns");

            migrationBuilder.DropTable(
                name: "CampaignPlacementRuns");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_AdvertiserOpportunityId",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_CampaignPlacements_BlissMatchId",
                table: "CampaignPlacements");

            migrationBuilder.DropColumn(
                name: "AdvertiserOpportunityId",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "BlissMatchId",
                table: "CampaignPlacements");
        }
    }
}
