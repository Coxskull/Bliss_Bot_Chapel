CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE "Advertisers" (
    "Id" uuid NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Website" character varying(1024),
    "CountryCode" character varying(8),
    "Description" character varying(4000),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Advertisers" PRIMARY KEY ("Id")
);

CREATE TABLE "AffiliateNetworks" (
    "Id" uuid NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Website" character varying(1024),
    "Status" character varying(64) NOT NULL DEFAULT 'ACTIVE',
    CONSTRAINT "PK_AffiliateNetworks" PRIMARY KEY ("Id")
);

CREATE TABLE "Campaigns" (
    "Id" uuid NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Status" character varying(64) NOT NULL DEFAULT 'DRAFT',
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Campaigns" PRIMARY KEY ("Id")
);

CREATE TABLE "Creators" (
    "Id" uuid NOT NULL,
    "Name" character varying(256) NOT NULL,
    "CountryCode" character varying(8),
    "PrimaryLanguage" character varying(128),
    "AudienceSize" integer,
    "FemalePercentage" numeric(5,2),
    "MalePercentage" numeric(5,2),
    "PrimaryAgeRange" character varying(64),
    "PrimaryGeography" character varying(256),
    "EngagementLevel" character varying(64),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Creators" PRIMARY KEY ("Id")
);

CREATE TABLE "DataProvenances" (
    "Id" uuid NOT NULL,
    "EntityType" character varying(128) NOT NULL,
    "EntityId" uuid NOT NULL,
    "FieldName" character varying(128) NOT NULL,
    "SourceType" character varying(64) NOT NULL,
    "SourceName" character varying(256),
    "SourceUrl" character varying(1024),
    "ConfidenceLevel" character varying(64) NOT NULL DEFAULT 'UNKNOWN',
    "CollectedAt" timestamp with time zone NOT NULL,
    "Notes" character varying(4000),
    CONSTRAINT "PK_DataProvenances" PRIMARY KEY ("Id")
);

CREATE TABLE "RuleVersions" (
    "Id" uuid NOT NULL,
    "Version" character varying(64) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "Description" character varying(4000),
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_RuleVersions" PRIMARY KEY ("Id")
);

CREATE TABLE "AdvertiserPrograms" (
    "Id" uuid NOT NULL,
    "AdvertiserId" uuid NOT NULL,
    "Name" character varying(256) NOT NULL,
    "ExternalProgramId" character varying(256),
    "Status" character varying(64) NOT NULL DEFAULT 'ACTIVE',
    CONSTRAINT "PK_AdvertiserPrograms" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AdvertiserPrograms_Advertisers_AdvertiserId" FOREIGN KEY ("AdvertiserId") REFERENCES "Advertisers" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "NetworkAccesses" (
    "Id" uuid NOT NULL,
    "AdvertiserId" uuid NOT NULL,
    "AffiliateNetworkId" uuid NOT NULL,
    "Status" character varying(64) NOT NULL DEFAULT 'UNKNOWN',
    "ExternalAccountId" character varying(256),
    "ApprovedAt" timestamp with time zone,
    CONSTRAINT "PK_NetworkAccesses" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_NetworkAccesses_Advertisers_AdvertiserId" FOREIGN KEY ("AdvertiserId") REFERENCES "Advertisers" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_NetworkAccesses_AffiliateNetworks_AffiliateNetworkId" FOREIGN KEY ("AffiliateNetworkId") REFERENCES "AffiliateNetworks" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ContentItems" (
    "Id" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "ContentType" character varying(64) NOT NULL,
    "Title" character varying(512) NOT NULL,
    "ExternalContentId" character varying(256),
    "Url" character varying(1024),
    "PublishedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ContentItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ContentItems_Creators_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CreatorPlatforms" (
    "Id" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "Platform" character varying(64) NOT NULL,
    "ExternalProfileId" character varying(256),
    "ProfileUrl" character varying(1024),
    "Followers" integer,
    "LastCollectedAt" timestamp with time zone,
    CONSTRAINT "PK_CreatorPlatforms" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CreatorPlatforms_Creators_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MatchEvaluationRuns" (
    "Id" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "RuleVersionId" uuid NOT NULL,
    "AlgorithmVersion" character varying(64) NOT NULL,
    "StartedAt" timestamp with time zone NOT NULL,
    "CompletedAt" timestamp with time zone,
    "InputSnapshot" text,
    "Status" character varying(64) NOT NULL DEFAULT 'CREATED',
    CONSTRAINT "PK_MatchEvaluationRuns" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MatchEvaluationRuns_Creators_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MatchEvaluationRuns_RuleVersions_RuleVersionId" FOREIGN KEY ("RuleVersionId") REFERENCES "RuleVersions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "AdvertiserOpportunities" (
    "Id" uuid NOT NULL,
    "AdvertiserProgramId" uuid NOT NULL,
    "Name" character varying(256) NOT NULL,
    "ProductName" character varying(256),
    "Category" character varying(128),
    "Description" character varying(4000),
    "MarketCountryCode" character varying(8),
    "Language" character varying(64),
    "CommissionPercentage" numeric(7,4),
    "FixedFee" numeric(18,2),
    "CommissionType" character varying(64),
    "ExternalOpportunityId" character varying(256),
    "Status" character varying(64) NOT NULL DEFAULT 'ACTIVE',
    CONSTRAINT "PK_AdvertiserOpportunities" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AdvertiserOpportunities_AdvertiserPrograms_AdvertiserProgra~" FOREIGN KEY ("AdvertiserProgramId") REFERENCES "AdvertiserPrograms" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ProgramAccesses" (
    "Id" uuid NOT NULL,
    "AdvertiserProgramId" uuid NOT NULL,
    "Status" character varying(64) NOT NULL DEFAULT 'UNKNOWN',
    "ApprovedAt" timestamp with time zone,
    CONSTRAINT "PK_ProgramAccesses" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProgramAccesses_AdvertiserPrograms_AdvertiserProgramId" FOREIGN KEY ("AdvertiserProgramId") REFERENCES "AdvertiserPrograms" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "AdInventorySlots" (
    "Id" uuid NOT NULL,
    "ContentItemId" uuid NOT NULL,
    "SlotType" character varying(64) NOT NULL,
    "StartSecond" integer,
    "DurationSeconds" integer,
    "IsAvailable" boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT "PK_AdInventorySlots" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AdInventorySlots_ContentItems_ContentItemId" FOREIGN KEY ("ContentItemId") REFERENCES "ContentItems" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "BlissMatches" (
    "Id" uuid NOT NULL,
    "CreatorId" uuid NOT NULL,
    "AdvertiserOpportunityId" uuid NOT NULL,
    "RuleVersionId" uuid NOT NULL,
    "Status" character varying(64) NOT NULL DEFAULT 'CREATED',
    "OverallScore" numeric(7,4),
    "ConfidenceScore" numeric(7,4),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BlissMatches" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_BlissMatches_AdvertiserOpportunities_AdvertiserOpportunityId" FOREIGN KEY ("AdvertiserOpportunityId") REFERENCES "AdvertiserOpportunities" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BlissMatches_Creators_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BlissMatches_RuleVersions_RuleVersionId" FOREIGN KEY ("RuleVersionId") REFERENCES "RuleVersions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CampaignPlacements" (
    "Id" uuid NOT NULL,
    "CampaignId" uuid NOT NULL,
    "ContentItemId" uuid NOT NULL,
    "AdInventorySlotId" uuid NOT NULL,
    "Status" character varying(64) NOT NULL DEFAULT 'CREATED',
    "StartAt" timestamp with time zone,
    "EndAt" timestamp with time zone,
    CONSTRAINT "PK_CampaignPlacements" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CampaignPlacements_AdInventorySlots_AdInventorySlotId" FOREIGN KEY ("AdInventorySlotId") REFERENCES "AdInventorySlots" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CampaignPlacements_Campaigns_CampaignId" FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CampaignPlacements_ContentItems_ContentItemId" FOREIGN KEY ("ContentItemId") REFERENCES "ContentItems" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EligibilityChecks" (
    "Id" uuid NOT NULL,
    "BlissMatchId" uuid NOT NULL,
    "CheckType" character varying(128) NOT NULL,
    "Result" character varying(64) NOT NULL,
    "ReasonCode" character varying(128),
    "Explanation" character varying(4000),
    CONSTRAINT "PK_EligibilityChecks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EligibilityChecks_BlissMatches_BlissMatchId" FOREIGN KEY ("BlissMatchId") REFERENCES "BlissMatches" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MatchScoreComponents" (
    "Id" uuid NOT NULL,
    "BlissMatchId" uuid NOT NULL,
    "ComponentName" character varying(128) NOT NULL,
    "Score" numeric(7,4),
    "Weight" numeric(7,4),
    "Explanation" character varying(4000),
    CONSTRAINT "PK_MatchScoreComponents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MatchScoreComponents_BlissMatches_BlissMatchId" FOREIGN KEY ("BlissMatchId") REFERENCES "BlissMatches" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_AdInventorySlots_ContentItemId" ON "AdInventorySlots" ("ContentItemId");

CREATE INDEX "IX_AdInventorySlots_SlotType" ON "AdInventorySlots" ("SlotType");

CREATE INDEX "IX_AdvertiserOpportunities_AdvertiserProgramId" ON "AdvertiserOpportunities" ("AdvertiserProgramId");

CREATE INDEX "IX_AdvertiserOpportunities_ExternalOpportunityId" ON "AdvertiserOpportunities" ("ExternalOpportunityId");

CREATE INDEX "IX_AdvertiserOpportunities_Status" ON "AdvertiserOpportunities" ("Status");

CREATE INDEX "IX_AdvertiserPrograms_AdvertiserId" ON "AdvertiserPrograms" ("AdvertiserId");

CREATE INDEX "IX_AdvertiserPrograms_ExternalProgramId" ON "AdvertiserPrograms" ("ExternalProgramId");

CREATE INDEX "IX_AdvertiserPrograms_Status" ON "AdvertiserPrograms" ("Status");

CREATE INDEX "IX_Advertisers_Name" ON "Advertisers" ("Name");

CREATE INDEX "IX_AffiliateNetworks_Name" ON "AffiliateNetworks" ("Name");

CREATE INDEX "IX_AffiliateNetworks_Status" ON "AffiliateNetworks" ("Status");

CREATE INDEX "IX_BlissMatches_AdvertiserOpportunityId" ON "BlissMatches" ("AdvertiserOpportunityId");

CREATE INDEX "IX_BlissMatches_CreatorId" ON "BlissMatches" ("CreatorId");

CREATE INDEX "IX_BlissMatches_RuleVersionId" ON "BlissMatches" ("RuleVersionId");

CREATE INDEX "IX_BlissMatches_Status" ON "BlissMatches" ("Status");

CREATE INDEX "IX_CampaignPlacements_AdInventorySlotId" ON "CampaignPlacements" ("AdInventorySlotId");

CREATE INDEX "IX_CampaignPlacements_CampaignId" ON "CampaignPlacements" ("CampaignId");

CREATE INDEX "IX_CampaignPlacements_ContentItemId" ON "CampaignPlacements" ("ContentItemId");

CREATE INDEX "IX_CampaignPlacements_Status" ON "CampaignPlacements" ("Status");

CREATE INDEX "IX_Campaigns_Status" ON "Campaigns" ("Status");

CREATE INDEX "IX_ContentItems_CreatorId" ON "ContentItems" ("CreatorId");

CREATE INDEX "IX_ContentItems_ExternalContentId" ON "ContentItems" ("ExternalContentId");

CREATE INDEX "IX_CreatorPlatforms_CreatorId" ON "CreatorPlatforms" ("CreatorId");

CREATE INDEX "IX_CreatorPlatforms_ExternalProfileId" ON "CreatorPlatforms" ("ExternalProfileId");

CREATE INDEX "IX_CreatorPlatforms_Platform" ON "CreatorPlatforms" ("Platform");

CREATE INDEX "IX_Creators_Name" ON "Creators" ("Name");

CREATE INDEX "IX_DataProvenances_CollectedAt" ON "DataProvenances" ("CollectedAt");

CREATE INDEX "IX_DataProvenances_EntityType_EntityId" ON "DataProvenances" ("EntityType", "EntityId");

CREATE INDEX "IX_DataProvenances_FieldName" ON "DataProvenances" ("FieldName");

CREATE INDEX "IX_EligibilityChecks_BlissMatchId" ON "EligibilityChecks" ("BlissMatchId");

CREATE INDEX "IX_EligibilityChecks_CheckType" ON "EligibilityChecks" ("CheckType");

CREATE INDEX "IX_MatchEvaluationRuns_CreatorId" ON "MatchEvaluationRuns" ("CreatorId");

CREATE INDEX "IX_MatchEvaluationRuns_RuleVersionId" ON "MatchEvaluationRuns" ("RuleVersionId");

CREATE INDEX "IX_MatchScoreComponents_BlissMatchId" ON "MatchScoreComponents" ("BlissMatchId");

CREATE INDEX "IX_MatchScoreComponents_ComponentName" ON "MatchScoreComponents" ("ComponentName");

CREATE INDEX "IX_NetworkAccesses_AdvertiserId" ON "NetworkAccesses" ("AdvertiserId");

CREATE INDEX "IX_NetworkAccesses_AffiliateNetworkId" ON "NetworkAccesses" ("AffiliateNetworkId");

CREATE INDEX "IX_NetworkAccesses_ExternalAccountId" ON "NetworkAccesses" ("ExternalAccountId");

CREATE INDEX "IX_NetworkAccesses_Status" ON "NetworkAccesses" ("Status");

CREATE INDEX "IX_ProgramAccesses_AdvertiserProgramId" ON "ProgramAccesses" ("AdvertiserProgramId");

CREATE INDEX "IX_ProgramAccesses_Status" ON "ProgramAccesses" ("Status");

CREATE INDEX "IX_RuleVersions_IsActive" ON "RuleVersions" ("IsActive");

CREATE INDEX "IX_RuleVersions_Version" ON "RuleVersions" ("Version");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260919013044_Phase1Foundation', '8.0.11');

COMMIT;

