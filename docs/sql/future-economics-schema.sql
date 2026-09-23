-- Bliss Economics & Rate Intelligence — RESERVED future schema.
-- PostgreSQL / Supabase-compatible. Do NOT apply until a future
-- Economics engineering contract is accepted. Not an EF migration.
--
-- Country and city are data. Do not create per-city tables.
-- Do not seed production rates or a 20/80 compensation default.

BEGIN;

CREATE TABLE IF NOT EXISTS "GeographicMarkets" (
    "Id" uuid PRIMARY KEY,
    "CountryCode" character varying(8) NOT NULL,
    "CityName" character varying(128),
    "MetroName" character varying(128),
    "MarketCode" character varying(64) NOT NULL,
    "CurrencyCode" character varying(8) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "AK_GeographicMarkets_MarketCode" UNIQUE ("MarketCode")
);

CREATE INDEX IF NOT EXISTS "IX_GeographicMarkets_CountryCode"
    ON "GeographicMarkets" ("CountryCode");

CREATE TABLE IF NOT EXISTS "MarketEconomicProfiles" (
    "Id" uuid PRIMARY KEY,
    "GeographicMarketId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "PurchasingPowerIndex" numeric(18, 6),
    "CompetitionLevel" character varying(32),
    "AudienceScarcityLevel" character varying(32),
    "Notes" character varying(4000),
    "EffectiveAt" timestamp with time zone NOT NULL,
    "SupersededAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_MarketEconomicProfiles_GeographicMarkets"
        FOREIGN KEY ("GeographicMarketId") REFERENCES "GeographicMarkets" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "AK_MarketEconomicProfiles_Market_Version"
        UNIQUE ("GeographicMarketId", "Version")
);

CREATE TABLE IF NOT EXISTS "IndustryEconomicProfiles" (
    "Id" uuid PRIMARY KEY,
    "Category" character varying(128) NOT NULL,
    "Version" integer NOT NULL,
    "AcquisitionEconomicsNotes" character varying(4000),
    "EffectiveAt" timestamp with time zone NOT NULL,
    "SupersededAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "AK_IndustryEconomicProfiles_Category_Version"
        UNIQUE ("Category", "Version")
);

CREATE TABLE IF NOT EXISTS "ResearchSources" (
    "Id" uuid PRIMARY KEY,
    "Name" character varying(256) NOT NULL,
    "SourceUrl" character varying(2048),
    "SourceType" character varying(64) NOT NULL,
    "IsApproved" boolean NOT NULL DEFAULT false,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "MarketBenchmarkObservations" (
    "Id" uuid PRIMARY KEY,
    "ResearchSourceId" uuid,
    "GeographicMarketId" uuid,
    "IndustryCategory" character varying(128),
    "Platform" character varying(64),
    "InventorySlotType" character varying(64),
    "Metric" character varying(64) NOT NULL,
    "NumericValue" numeric(18, 6),
    "RangeLow" numeric(18, 6),
    "RangeHigh" numeric(18, 6),
    "CurrencyCode" character varying(8),
    "PublicationDate" date,
    "RetrievedAt" timestamp with time zone NOT NULL,
    "ConfidenceLevel" character varying(32) NOT NULL DEFAULT 'UNKNOWN',
    "VerificationStatus" character varying(32) NOT NULL DEFAULT 'UNKNOWN',
    "Notes" character varying(4000),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_MarketBenchmarkObservations_ResearchSources"
        FOREIGN KEY ("ResearchSourceId") REFERENCES "ResearchSources" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MarketBenchmarkObservations_GeographicMarkets"
        FOREIGN KEY ("GeographicMarketId") REFERENCES "GeographicMarkets" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_MarketBenchmarkObservations_Market_Metric"
    ON "MarketBenchmarkObservations" ("GeographicMarketId", "Metric", "RetrievedAt");

CREATE TABLE IF NOT EXISTS "CreatorAudienceSnapshots" (
    "Id" uuid PRIMARY KEY,
    "CreatorId" uuid NOT NULL,
    "CapturedAt" timestamp with time zone NOT NULL,
    "Subscribers" integer,
    "FemalePercentage" numeric(7, 4),
    "MalePercentage" numeric(7, 4),
    "PrimaryAgeRange" character varying(64),
    "PrimaryGeography" character varying(128),
    "GeographicMarketId" uuid,
    "Language" character varying(64),
    "ConfidenceLevel" character varying(32) NOT NULL DEFAULT 'UNKNOWN',
    "SourceType" character varying(64),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_CreatorAudienceSnapshots_Creators"
        FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CreatorAudienceSnapshots_GeographicMarkets"
        FOREIGN KEY ("GeographicMarketId") REFERENCES "GeographicMarkets" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "CreatorPerformanceSnapshots" (
    "Id" uuid PRIMARY KEY,
    "CreatorId" uuid NOT NULL,
    "ContentItemId" uuid,
    "CapturedAt" timestamp with time zone NOT NULL,
    "AverageViews" integer,
    "DailyViews" integer,
    "WeeklyViews" integer,
    "MonthlyViews" integer,
    "HistoricalReach" integer,
    "EngagementRate" numeric(9, 6),
    "RetentionRate" numeric(9, 6),
    "PublishingFrequencyPerWeek" numeric(9, 4),
    "Platform" character varying(64),
    "ContentFormat" character varying(64),
    "ConfidenceLevel" character varying(32) NOT NULL DEFAULT 'UNKNOWN',
    "SourceType" character varying(64),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_CreatorPerformanceSnapshots_Creators"
        FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CreatorPerformanceSnapshots_ContentItems"
        FOREIGN KEY ("ContentItemId") REFERENCES "ContentItems" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "PricingModels" (
    "Id" uuid PRIMARY KEY,
    "Code" character varying(32) NOT NULL,
    "Name" character varying(128) NOT NULL,
    "Description" character varying(2000),
    "IsActive" boolean NOT NULL DEFAULT true,
    CONSTRAINT "AK_PricingModels_Code" UNIQUE ("Code")
);

CREATE TABLE IF NOT EXISTS "InventoryRateBenchmarks" (
    "Id" uuid PRIMARY KEY,
    "GeographicMarketId" uuid,
    "PricingModelId" uuid NOT NULL,
    "InventorySlotType" character varying(64) NOT NULL,
    "DurationSecondsLow" integer,
    "DurationSecondsHigh" integer,
    "RangeLow" numeric(18, 6),
    "RangeHigh" numeric(18, 6),
    "CurrencyCode" character varying(8) NOT NULL,
    "EffectiveAt" timestamp with time zone NOT NULL,
    "SupersededAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_InventoryRateBenchmarks_GeographicMarkets"
        FOREIGN KEY ("GeographicMarketId") REFERENCES "GeographicMarkets" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_InventoryRateBenchmarks_PricingModels"
        FOREIGN KEY ("PricingModelId") REFERENCES "PricingModels" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "PricingRuleVersions" (
    "Id" uuid PRIMARY KEY,
    "Version" character varying(64) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "DocumentJson" text,
    "IsActive" boolean NOT NULL DEFAULT false,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "AK_PricingRuleVersions_Version" UNIQUE ("Version")
);

CREATE TABLE IF NOT EXISTS "CompensationRuleVersions" (
    "Id" uuid PRIMARY KEY,
    "Version" character varying(64) NOT NULL,
    "Name" character varying(256) NOT NULL,
    "DocumentJson" text,
    "IsActive" boolean NOT NULL DEFAULT false,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "AK_CompensationRuleVersions_Version" UNIQUE ("Version")
);

CREATE TABLE IF NOT EXISTS "CompensationRuleShares" (
    "Id" uuid PRIMARY KEY,
    "CompensationRuleVersionId" uuid NOT NULL,
    "ParticipantRole" character varying(64) NOT NULL,
    "SharePercentage" numeric(7, 4),
    "ShareAmount" numeric(18, 2),
    "CurrencyCode" character varying(8),
    CONSTRAINT "FK_CompensationRuleShares_CompensationRuleVersions"
        FOREIGN KEY ("CompensationRuleVersionId") REFERENCES "CompensationRuleVersions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "RateRecommendations" (
    "Id" uuid PRIMARY KEY,
    "CreatorId" uuid NOT NULL,
    "ContentItemId" uuid,
    "AdInventorySlotId" uuid,
    "AdvertiserOpportunityId" uuid,
    "BlissMatchId" uuid,
    "GeographicMarketId" uuid,
    "IndustryCategory" character varying(128),
    "CampaignObjective" character varying(256),
    "DurationSeconds" integer,
    "PricingModelId" uuid NOT NULL,
    "PricingRuleVersionId" uuid NOT NULL,
    "CompensationRuleVersionId" uuid,
    "CurrencyCode" character varying(8) NOT NULL,
    "RangeLow" numeric(18, 2) NOT NULL,
    "RangeTarget" numeric(18, 2) NOT NULL,
    "RangeHigh" numeric(18, 2) NOT NULL,
    "EstimatedImpressions" integer,
    "ConfidenceLevel" character varying(32) NOT NULL,
    "BenchmarkAsOf" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_RateRecommendations_Creators"
        FOREIGN KEY ("CreatorId") REFERENCES "Creators" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendations_ContentItems"
        FOREIGN KEY ("ContentItemId") REFERENCES "ContentItems" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendations_AdInventorySlots"
        FOREIGN KEY ("AdInventorySlotId") REFERENCES "AdInventorySlots" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendations_AdvertiserOpportunities"
        FOREIGN KEY ("AdvertiserOpportunityId") REFERENCES "AdvertiserOpportunities" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendations_BlissMatches"
        FOREIGN KEY ("BlissMatchId") REFERENCES "BlissMatches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendations_GeographicMarkets"
        FOREIGN KEY ("GeographicMarketId") REFERENCES "GeographicMarkets" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendations_PricingModels"
        FOREIGN KEY ("PricingModelId") REFERENCES "PricingModels" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendations_PricingRuleVersions"
        FOREIGN KEY ("PricingRuleVersionId") REFERENCES "PricingRuleVersions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendations_CompensationRuleVersions"
        FOREIGN KEY ("CompensationRuleVersionId") REFERENCES "CompensationRuleVersions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "RateRecommendationFactors" (
    "Id" uuid PRIMARY KEY,
    "RateRecommendationId" uuid NOT NULL,
    "FactorCode" character varying(64) NOT NULL,
    "Label" character varying(256) NOT NULL,
    "NumericValue" numeric(18, 6),
    "Rationale" character varying(2000),
    "SortOrder" integer NOT NULL,
    CONSTRAINT "FK_RateRecommendationFactors_RateRecommendations"
        FOREIGN KEY ("RateRecommendationId") REFERENCES "RateRecommendations" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "RateRecommendationSources" (
    "Id" uuid PRIMARY KEY,
    "RateRecommendationId" uuid NOT NULL,
    "MarketBenchmarkObservationId" uuid,
    "ResearchSourceId" uuid,
    CONSTRAINT "FK_RateRecommendationSources_RateRecommendations"
        FOREIGN KEY ("RateRecommendationId") REFERENCES "RateRecommendations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendationSources_MarketBenchmarkObservations"
        FOREIGN KEY ("MarketBenchmarkObservationId") REFERENCES "MarketBenchmarkObservations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RateRecommendationSources_ResearchSources"
        FOREIGN KEY ("ResearchSourceId") REFERENCES "ResearchSources" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "Quotes" (
    "Id" uuid PRIMARY KEY,
    "AdvertiserId" uuid NOT NULL,
    "WeddingPlannerWorkspaceId" uuid,
    "CampaignId" uuid,
    "Status" character varying(32) NOT NULL DEFAULT 'DRAFT',
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_Quotes_Advertisers"
        FOREIGN KEY ("AdvertiserId") REFERENCES "Advertisers" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Quotes_WeddingPlannerWorkspaces"
        FOREIGN KEY ("WeddingPlannerWorkspaceId") REFERENCES "WeddingPlannerWorkspaces" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Quotes_Campaigns"
        FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "QuoteVersions" (
    "Id" uuid PRIMARY KEY,
    "QuoteId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "RateRecommendationId" uuid,
    "CurrencyCode" character varying(8) NOT NULL,
    "Subtotal" numeric(18, 2) NOT NULL,
    "Notes" character varying(4000),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_QuoteVersions_Quotes"
        FOREIGN KEY ("QuoteId") REFERENCES "Quotes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QuoteVersions_RateRecommendations"
        FOREIGN KEY ("RateRecommendationId") REFERENCES "RateRecommendations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "AK_QuoteVersions_Quote_Version" UNIQUE ("QuoteId", "Version")
);

CREATE TABLE IF NOT EXISTS "QuoteLineItems" (
    "Id" uuid PRIMARY KEY,
    "QuoteVersionId" uuid NOT NULL,
    "AdInventorySlotId" uuid NOT NULL,
    "RateRecommendationId" uuid,
    "PricingModelId" uuid NOT NULL,
    "DurationSeconds" integer,
    "Amount" numeric(18, 2) NOT NULL,
    "CurrencyCode" character varying(8) NOT NULL,
    "Description" character varying(512),
    CONSTRAINT "FK_QuoteLineItems_QuoteVersions"
        FOREIGN KEY ("QuoteVersionId") REFERENCES "QuoteVersions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QuoteLineItems_AdInventorySlots"
        FOREIGN KEY ("AdInventorySlotId") REFERENCES "AdInventorySlots" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QuoteLineItems_RateRecommendations"
        FOREIGN KEY ("RateRecommendationId") REFERENCES "RateRecommendations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QuoteLineItems_PricingModels"
        FOREIGN KEY ("PricingModelId") REFERENCES "PricingModels" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "QuoteOutcomes" (
    "Id" uuid PRIMARY KEY,
    "QuoteVersionId" uuid NOT NULL,
    "Response" character varying(32) NOT NULL,
    "NegotiatedAmount" numeric(18, 2),
    "CurrencyCode" character varying(8),
    "RespondedAt" timestamp with time zone NOT NULL,
    "ActorType" character varying(32) NOT NULL,
    "Notes" character varying(2000),
    CONSTRAINT "FK_QuoteOutcomes_QuoteVersions"
        FOREIGN KEY ("QuoteVersionId") REFERENCES "QuoteVersions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "HistoricalPlacementEconomics" (
    "Id" uuid PRIMARY KEY,
    "CampaignPlacementId" uuid NOT NULL,
    "QuoteVersionId" uuid,
    "RateRecommendationId" uuid,
    "QuotedAmount" numeric(18, 2),
    "ContractedAmount" numeric(18, 2),
    "ActualImpressions" integer,
    "ActualViews" integer,
    "CreatorCompensationAmount" numeric(18, 2),
    "AlphaCompensationAmount" numeric(18, 2),
    "OtherCompensationAmount" numeric(18, 2),
    "CurrencyCode" character varying(8),
    "CompensationRuleVersionId" uuid,
    "RecordedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_HistoricalPlacementEconomics_CampaignPlacements"
        FOREIGN KEY ("CampaignPlacementId") REFERENCES "CampaignPlacements" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HistoricalPlacementEconomics_QuoteVersions"
        FOREIGN KEY ("QuoteVersionId") REFERENCES "QuoteVersions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HistoricalPlacementEconomics_RateRecommendations"
        FOREIGN KEY ("RateRecommendationId") REFERENCES "RateRecommendations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HistoricalPlacementEconomics_CompensationRuleVersions"
        FOREIGN KEY ("CompensationRuleVersionId") REFERENCES "CompensationRuleVersions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "CampaignPerformanceEconomics" (
    "Id" uuid PRIMARY KEY,
    "CampaignId" uuid NOT NULL,
    "EngagementRate" numeric(9, 6),
    "Conversions" integer,
    "ConversionValue" numeric(18, 2),
    "CurrencyCode" character varying(8),
    "RecordedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_CampaignPerformanceEconomics_Campaigns"
        FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "EconomicsResearchRuns" (
    "Id" uuid PRIMARY KEY,
    "SourceSystem" character varying(64) NOT NULL,
    "IdempotencyKey" character varying(128) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "StartedAt" timestamp with time zone NOT NULL,
    "CompletedAt" timestamp with time zone,
    "Notes" character varying(2000),
    CONSTRAINT "AK_EconomicsResearchRuns_Source_Idempotency"
        UNIQUE ("SourceSystem", "IdempotencyKey")
);

COMMIT;
