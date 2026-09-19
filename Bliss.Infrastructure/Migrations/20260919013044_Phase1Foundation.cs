using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bliss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Advertisers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Website = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Advertisers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Website = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "ACTIVE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateNetworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "DRAFT"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Creators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    PrimaryLanguage = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AudienceSize = table.Column<int>(type: "integer", nullable: true),
                    FemalePercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    MalePercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    PrimaryAgeRange = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PrimaryGeography = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EngagementLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProvenances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "UNKNOWN"),
                    CollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProvenances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdvertiserPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExternalProgramId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "ACTIVE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvertiserPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvertiserPrograms_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NetworkAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "UNKNOWN"),
                    ExternalAccountId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkAccesses_Advertisers_AdvertiserId",
                        column: x => x.AdvertiserId,
                        principalTable: "Advertisers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NetworkAccesses_AffiliateNetworks_AffiliateNetworkId",
                        column: x => x.AffiliateNetworkId,
                        principalTable: "AffiliateNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExternalContentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentItems_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatorPlatforms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalProfileId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProfileUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Followers = table.Column<int>(type: "integer", nullable: true),
                    LastCollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorPlatforms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorPlatforms_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchEvaluationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InputSnapshot = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "CREATED")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEvaluationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchEvaluationRuns_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchEvaluationRuns_RuleVersions_RuleVersionId",
                        column: x => x.RuleVersionId,
                        principalTable: "RuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdvertiserOpportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MarketCountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CommissionPercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    FixedFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CommissionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExternalOpportunityId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "ACTIVE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvertiserOpportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvertiserOpportunities_AdvertiserPrograms_AdvertiserProgra~",
                        column: x => x.AdvertiserProgramId,
                        principalTable: "AdvertiserPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "UNKNOWN"),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAccesses_AdvertiserPrograms_AdvertiserProgramId",
                        column: x => x.AdvertiserProgramId,
                        principalTable: "AdvertiserPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdInventorySlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartSecond = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdInventorySlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdInventorySlots_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BlissMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "CREATED"),
                    OverallScore = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlissMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlissMatches_AdvertiserOpportunities_AdvertiserOpportunityId",
                        column: x => x.AdvertiserOpportunityId,
                        principalTable: "AdvertiserOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BlissMatches_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BlissMatches_RuleVersions_RuleVersionId",
                        column: x => x.RuleVersionId,
                        principalTable: "RuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdInventorySlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "CREATED"),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignPlacements_AdInventorySlots_AdInventorySlotId",
                        column: x => x.AdInventorySlotId,
                        principalTable: "AdInventorySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlacements_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignPlacements_ContentItems_ContentItemId",
                        column: x => x.ContentItemId,
                        principalTable: "ContentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EligibilityChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Result = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EligibilityChecks_BlissMatches_BlissMatchId",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchScoreComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlissMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchScoreComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchScoreComponents_BlissMatches_BlissMatchId",
                        column: x => x.BlissMatchId,
                        principalTable: "BlissMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdInventorySlots_ContentItemId",
                table: "AdInventorySlots",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AdInventorySlots_SlotType",
                table: "AdInventorySlots",
                column: "SlotType");

            migrationBuilder.CreateIndex(
                name: "IX_AdvertiserOpportunities_AdvertiserProgramId",
                table: "AdvertiserOpportunities",
                column: "AdvertiserProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvertiserOpportunities_ExternalOpportunityId",
                table: "AdvertiserOpportunities",
                column: "ExternalOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvertiserOpportunities_Status",
                table: "AdvertiserOpportunities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AdvertiserPrograms_AdvertiserId",
                table: "AdvertiserPrograms",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvertiserPrograms_ExternalProgramId",
                table: "AdvertiserPrograms",
                column: "ExternalProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvertiserPrograms_Status",
                table: "AdvertiserPrograms",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Advertisers_Name",
                table: "Advertisers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateNetworks_Name",
                table: "AffiliateNetworks",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateNetworks_Status",
                table: "AffiliateNetworks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BlissMatches_AdvertiserOpportunityId",
                table: "BlissMatches",
                column: "AdvertiserOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_BlissMatches_CreatorId",
                table: "BlissMatches",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_BlissMatches_RuleVersionId",
                table: "BlissMatches",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BlissMatches_Status",
                table: "BlissMatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacements_AdInventorySlotId",
                table: "CampaignPlacements",
                column: "AdInventorySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacements_CampaignId",
                table: "CampaignPlacements",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacements_ContentItemId",
                table: "CampaignPlacements",
                column: "ContentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignPlacements_Status",
                table: "CampaignPlacements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Status",
                table: "Campaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_CreatorId",
                table: "ContentItems",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_ExternalContentId",
                table: "ContentItems",
                column: "ExternalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorPlatforms_CreatorId",
                table: "CreatorPlatforms",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorPlatforms_ExternalProfileId",
                table: "CreatorPlatforms",
                column: "ExternalProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorPlatforms_Platform",
                table: "CreatorPlatforms",
                column: "Platform");

            migrationBuilder.CreateIndex(
                name: "IX_Creators_Name",
                table: "Creators",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DataProvenances_CollectedAt",
                table: "DataProvenances",
                column: "CollectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DataProvenances_EntityType_EntityId",
                table: "DataProvenances",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_DataProvenances_FieldName",
                table: "DataProvenances",
                column: "FieldName");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityChecks_BlissMatchId",
                table: "EligibilityChecks",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityChecks_CheckType",
                table: "EligibilityChecks",
                column: "CheckType");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvaluationRuns_CreatorId",
                table: "MatchEvaluationRuns",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvaluationRuns_RuleVersionId",
                table: "MatchEvaluationRuns",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchScoreComponents_BlissMatchId",
                table: "MatchScoreComponents",
                column: "BlissMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchScoreComponents_ComponentName",
                table: "MatchScoreComponents",
                column: "ComponentName");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkAccesses_AdvertiserId",
                table: "NetworkAccesses",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkAccesses_AffiliateNetworkId",
                table: "NetworkAccesses",
                column: "AffiliateNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkAccesses_ExternalAccountId",
                table: "NetworkAccesses",
                column: "ExternalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkAccesses_Status",
                table: "NetworkAccesses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAccesses_AdvertiserProgramId",
                table: "ProgramAccesses",
                column: "AdvertiserProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAccesses_Status",
                table: "ProgramAccesses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RuleVersions_IsActive",
                table: "RuleVersions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RuleVersions_Version",
                table: "RuleVersions",
                column: "Version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignPlacements");

            migrationBuilder.DropTable(
                name: "CreatorPlatforms");

            migrationBuilder.DropTable(
                name: "DataProvenances");

            migrationBuilder.DropTable(
                name: "EligibilityChecks");

            migrationBuilder.DropTable(
                name: "MatchEvaluationRuns");

            migrationBuilder.DropTable(
                name: "MatchScoreComponents");

            migrationBuilder.DropTable(
                name: "NetworkAccesses");

            migrationBuilder.DropTable(
                name: "ProgramAccesses");

            migrationBuilder.DropTable(
                name: "AdInventorySlots");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "BlissMatches");

            migrationBuilder.DropTable(
                name: "AffiliateNetworks");

            migrationBuilder.DropTable(
                name: "ContentItems");

            migrationBuilder.DropTable(
                name: "AdvertiserOpportunities");

            migrationBuilder.DropTable(
                name: "RuleVersions");

            migrationBuilder.DropTable(
                name: "Creators");

            migrationBuilder.DropTable(
                name: "AdvertiserPrograms");

            migrationBuilder.DropTable(
                name: "Advertisers");
        }
    }
}
