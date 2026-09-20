-- Bliss Bot Chapel schema only (Phases 1–4, idempotent). No seed data.

BEGIN;

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE TABLE "Advertisers" (
        "Id" uuid NOT NULL,
        "Name" character varying(256) NOT NULL,
        "Website" character varying(1024),
        "CountryCode" character varying(8),
        "Description" character varying(4000),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Advertisers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE TABLE "AffiliateNetworks" (
        "Id" uuid NOT NULL,
        "Name" character varying(256) NOT NULL,
        "Website" character varying(1024),
        "Status" character varying(64) NOT NULL DEFAULT 'ACTIVE',
        CONSTRAINT "PK_AffiliateNetworks" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE TABLE "Campaigns" (
        "Id" uuid NOT NULL,
        "Name" character varying(256) NOT NULL,
        "Status" character varying(64) NOT NULL DEFAULT 'DRAFT',
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Campaigns" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE TABLE "RuleVersions" (
        "Id" uuid NOT NULL,
        "Version" character varying(64) NOT NULL,
        "Name" character varying(256) NOT NULL,
        "Description" character varying(4000),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_RuleVersions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE TABLE "AdvertiserPrograms" (
        "Id" uuid NOT NULL,
        "AdvertiserId" uuid NOT NULL,
        "Name" character varying(256) NOT NULL,
        "ExternalProgramId" character varying(256),
        "Status" character varying(64) NOT NULL DEFAULT 'ACTIVE',
        CONSTRAINT "PK_AdvertiserPrograms" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AdvertiserPrograms_Advertisers_AdvertiserId" FOREIGN KEY ("AdvertiserId") REFERENCES "Advertisers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE TABLE "ProgramAccesses" (
        "Id" uuid NOT NULL,
        "AdvertiserProgramId" uuid NOT NULL,
        "Status" character varying(64) NOT NULL DEFAULT 'UNKNOWN',
        "ApprovedAt" timestamp with time zone,
        CONSTRAINT "PK_ProgramAccesses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProgramAccesses_AdvertiserPrograms_AdvertiserProgramId" FOREIGN KEY ("AdvertiserProgramId") REFERENCES "AdvertiserPrograms" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AdInventorySlots_ContentItemId" ON "AdInventorySlots" ("ContentItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AdInventorySlots_SlotType" ON "AdInventorySlots" ("SlotType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AdvertiserOpportunities_AdvertiserProgramId" ON "AdvertiserOpportunities" ("AdvertiserProgramId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AdvertiserOpportunities_ExternalOpportunityId" ON "AdvertiserOpportunities" ("ExternalOpportunityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AdvertiserOpportunities_Status" ON "AdvertiserOpportunities" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AdvertiserPrograms_AdvertiserId" ON "AdvertiserPrograms" ("AdvertiserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AdvertiserPrograms_ExternalProgramId" ON "AdvertiserPrograms" ("ExternalProgramId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AdvertiserPrograms_Status" ON "AdvertiserPrograms" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_Advertisers_Name" ON "Advertisers" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AffiliateNetworks_Name" ON "AffiliateNetworks" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_AffiliateNetworks_Status" ON "AffiliateNetworks" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_BlissMatches_AdvertiserOpportunityId" ON "BlissMatches" ("AdvertiserOpportunityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_BlissMatches_CreatorId" ON "BlissMatches" ("CreatorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_BlissMatches_RuleVersionId" ON "BlissMatches" ("RuleVersionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_BlissMatches_Status" ON "BlissMatches" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_CampaignPlacements_AdInventorySlotId" ON "CampaignPlacements" ("AdInventorySlotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_CampaignPlacements_CampaignId" ON "CampaignPlacements" ("CampaignId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_CampaignPlacements_ContentItemId" ON "CampaignPlacements" ("ContentItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_CampaignPlacements_Status" ON "CampaignPlacements" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_Campaigns_Status" ON "Campaigns" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_ContentItems_CreatorId" ON "ContentItems" ("CreatorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_ContentItems_ExternalContentId" ON "ContentItems" ("ExternalContentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_CreatorPlatforms_CreatorId" ON "CreatorPlatforms" ("CreatorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_CreatorPlatforms_ExternalProfileId" ON "CreatorPlatforms" ("ExternalProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_CreatorPlatforms_Platform" ON "CreatorPlatforms" ("Platform");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_Creators_Name" ON "Creators" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_DataProvenances_CollectedAt" ON "DataProvenances" ("CollectedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_DataProvenances_EntityType_EntityId" ON "DataProvenances" ("EntityType", "EntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_DataProvenances_FieldName" ON "DataProvenances" ("FieldName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_EligibilityChecks_BlissMatchId" ON "EligibilityChecks" ("BlissMatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_EligibilityChecks_CheckType" ON "EligibilityChecks" ("CheckType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_MatchEvaluationRuns_CreatorId" ON "MatchEvaluationRuns" ("CreatorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_MatchEvaluationRuns_RuleVersionId" ON "MatchEvaluationRuns" ("RuleVersionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_MatchScoreComponents_BlissMatchId" ON "MatchScoreComponents" ("BlissMatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_MatchScoreComponents_ComponentName" ON "MatchScoreComponents" ("ComponentName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_NetworkAccesses_AdvertiserId" ON "NetworkAccesses" ("AdvertiserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_NetworkAccesses_AffiliateNetworkId" ON "NetworkAccesses" ("AffiliateNetworkId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_NetworkAccesses_ExternalAccountId" ON "NetworkAccesses" ("ExternalAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_NetworkAccesses_Status" ON "NetworkAccesses" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_ProgramAccesses_AdvertiserProgramId" ON "ProgramAccesses" ("AdvertiserProgramId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_ProgramAccesses_Status" ON "ProgramAccesses" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_RuleVersions_IsActive" ON "RuleVersions" ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    CREATE INDEX "IX_RuleVersions_Version" ON "RuleVersions" ("Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919013044_Phase1Foundation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260919013044_Phase1Foundation', '8.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920063520_Phase2RuleDocument') THEN
    ALTER TABLE "RuleVersions" ADD "DocumentJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920063520_Phase2RuleDocument') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920063520_Phase2RuleDocument', '8.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920073126_Phase3EvaluationAudit') THEN
    ALTER TABLE "MatchEvaluationRuns" ADD "BlissMatchId" uuid NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920073126_Phase3EvaluationAudit') THEN
    ALTER TABLE "MatchEvaluationRuns" ADD "ConfidenceScore" numeric(7,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920073126_Phase3EvaluationAudit') THEN
    ALTER TABLE "MatchEvaluationRuns" ADD "MatchStatus" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920073126_Phase3EvaluationAudit') THEN
    ALTER TABLE "MatchEvaluationRuns" ADD "OutputSnapshot" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920073126_Phase3EvaluationAudit') THEN
    ALTER TABLE "MatchEvaluationRuns" ADD "OverallScore" numeric(7,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920073126_Phase3EvaluationAudit') THEN
    CREATE INDEX "IX_MatchEvaluationRuns_BlissMatchId" ON "MatchEvaluationRuns" ("BlissMatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920073126_Phase3EvaluationAudit') THEN
    ALTER TABLE "MatchEvaluationRuns" ADD CONSTRAINT "FK_MatchEvaluationRuns_BlissMatches_BlissMatchId" FOREIGN KEY ("BlissMatchId") REFERENCES "BlissMatches" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920073126_Phase3EvaluationAudit') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920073126_Phase3EvaluationAudit', '8.0.11');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    ALTER TABLE "CreatorPlatforms" ADD "IdentityKey" character varying(384);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    UPDATE "CreatorPlatforms"
    SET "IdentityKey" = upper(trim("Platform")) || '::' || trim("ExternalProfileId")
    WHERE "ExternalProfileId" IS NOT NULL
      AND trim("ExternalProfileId") <> '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    CREATE TABLE "CreatorIngestionRuns" (
        "Id" uuid NOT NULL,
        "CreatorId" uuid NOT NULL,
        "CreatorPlatformId" uuid NOT NULL,
        "SourceSystem" character varying(64) NOT NULL,
        "IdempotencyKey" character varying(128) NOT NULL,
        "IdentityKey" character varying(384) NOT NULL,
        "Status" character varying(64) NOT NULL,
        "Outcome" character varying(64) NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone NOT NULL,
        "InputSnapshot" text NOT NULL,
        CONSTRAINT "PK_CreatorIngestionRuns" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CreatorIngestionRuns_CreatorPlatforms_CreatorPlatformId" FOREIGN KEY ("CreatorPlatformId") REFERENCES "CreatorPlatforms" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CreatorIngestionRuns_Creators_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    CREATE UNIQUE INDEX "IX_CreatorPlatforms_IdentityKey" ON "CreatorPlatforms" ("IdentityKey") WHERE "IdentityKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    CREATE INDEX "IX_CreatorIngestionRuns_CompletedAt" ON "CreatorIngestionRuns" ("CompletedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    CREATE INDEX "IX_CreatorIngestionRuns_CreatorId" ON "CreatorIngestionRuns" ("CreatorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    CREATE INDEX "IX_CreatorIngestionRuns_CreatorPlatformId" ON "CreatorIngestionRuns" ("CreatorPlatformId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    CREATE INDEX "IX_CreatorIngestionRuns_IdentityKey" ON "CreatorIngestionRuns" ("IdentityKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    CREATE UNIQUE INDEX "IX_CreatorIngestionRuns_SourceSystem_IdempotencyKey" ON "CreatorIngestionRuns" ("SourceSystem", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920102946_Phase4CreatorIngestion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920102946_Phase4CreatorIngestion', '8.0.11');
    END IF;
END $EF$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920123653_Phase5MatchFormation') THEN
    CREATE TABLE "MatchFormationRuns" (
        "Id" uuid NOT NULL,
        "BlissMatchId" uuid NOT NULL,
        "CreatorId" uuid NOT NULL,
        "AdvertiserOpportunityId" uuid NOT NULL,
        "RuleVersionId" uuid NOT NULL,
        "SourceSystem" character varying(64) NOT NULL,
        "IdempotencyKey" character varying(128) NOT NULL,
        "Status" character varying(64) NOT NULL,
        "Outcome" character varying(64) NOT NULL,
        "EvaluateOnCreate" boolean NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone NOT NULL,
        "InputSnapshot" text NOT NULL,
        CONSTRAINT "PK_MatchFormationRuns" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MatchFormationRuns_AdvertiserOpportunities_AdvertiserOpport~" FOREIGN KEY ("AdvertiserOpportunityId") REFERENCES "AdvertiserOpportunities" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchFormationRuns_BlissMatches_BlissMatchId" FOREIGN KEY ("BlissMatchId") REFERENCES "BlissMatches" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchFormationRuns_Creators_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchFormationRuns_RuleVersions_RuleVersionId" FOREIGN KEY ("RuleVersionId") REFERENCES "RuleVersions" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920123653_Phase5MatchFormation') THEN
    CREATE INDEX "IX_MatchFormationRuns_AdvertiserOpportunityId" ON "MatchFormationRuns" ("AdvertiserOpportunityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920123653_Phase5MatchFormation') THEN
    CREATE INDEX "IX_MatchFormationRuns_BlissMatchId" ON "MatchFormationRuns" ("BlissMatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920123653_Phase5MatchFormation') THEN
    CREATE INDEX "IX_MatchFormationRuns_CompletedAt" ON "MatchFormationRuns" ("CompletedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920123653_Phase5MatchFormation') THEN
    CREATE INDEX "IX_MatchFormationRuns_CreatorId" ON "MatchFormationRuns" ("CreatorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920123653_Phase5MatchFormation') THEN
    CREATE INDEX "IX_MatchFormationRuns_RuleVersionId" ON "MatchFormationRuns" ("RuleVersionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920123653_Phase5MatchFormation') THEN
    CREATE UNIQUE INDEX "IX_MatchFormationRuns_SourceSystem_IdempotencyKey" ON "MatchFormationRuns" ("SourceSystem", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920123653_Phase5MatchFormation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920123653_Phase5MatchFormation', '8.0.11');
    END IF;
END $EF$;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920130043_Phase6HumanReview') THEN
    CREATE TABLE "MatchReviewDecisions" (
        "Id" uuid NOT NULL,
        "BlissMatchId" uuid NOT NULL,
        "CreatorId" uuid NOT NULL,
        "MatchEvaluationRunId" uuid,
        "SourceSystem" character varying(64) NOT NULL,
        "IdempotencyKey" character varying(128) NOT NULL,
        "ReviewerLabel" character varying(128) NOT NULL,
        "Decision" character varying(32) NOT NULL,
        "ResultingMatchStatus" character varying(64) NOT NULL,
        "Rationale" character varying(2000) NOT NULL,
        "Status" character varying(64) NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone NOT NULL,
        "InputSnapshot" text NOT NULL,
        CONSTRAINT "PK_MatchReviewDecisions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MatchReviewDecisions_BlissMatches_BlissMatchId" FOREIGN KEY ("BlissMatchId") REFERENCES "BlissMatches" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchReviewDecisions_Creators_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MatchReviewDecisions_MatchEvaluationRuns_MatchEvaluationRun~" FOREIGN KEY ("MatchEvaluationRunId") REFERENCES "MatchEvaluationRuns" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920130043_Phase6HumanReview') THEN
    CREATE INDEX "IX_MatchReviewDecisions_BlissMatchId" ON "MatchReviewDecisions" ("BlissMatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920130043_Phase6HumanReview') THEN
    CREATE INDEX "IX_MatchReviewDecisions_CompletedAt" ON "MatchReviewDecisions" ("CompletedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920130043_Phase6HumanReview') THEN
    CREATE INDEX "IX_MatchReviewDecisions_CreatorId" ON "MatchReviewDecisions" ("CreatorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920130043_Phase6HumanReview') THEN
    CREATE INDEX "IX_MatchReviewDecisions_MatchEvaluationRunId" ON "MatchReviewDecisions" ("MatchEvaluationRunId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920130043_Phase6HumanReview') THEN
    CREATE UNIQUE INDEX "IX_MatchReviewDecisions_SourceSystem_IdempotencyKey" ON "MatchReviewDecisions" ("SourceSystem", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920130043_Phase6HumanReview') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920130043_Phase6HumanReview', '8.0.11');
    END IF;
END $EF$;

COMMIT;
