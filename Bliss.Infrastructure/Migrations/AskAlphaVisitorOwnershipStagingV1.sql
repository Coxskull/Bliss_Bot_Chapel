-- AGM-LAUNCH-015 Stage J: DRAFT SCHEMA for disposable planner_lineage_ci ONLY.
-- THIS IS NOT AN EF MIGRATION. Do not apply to hosted Supabase/Railway/production.
-- Requires EF CreateTables() for existing WeddingPlanner workspace/session tables.
-- Runs within a unique per-test PostgreSQL search_path schema; no RLS or OIDC
-- identity-provider registration is provided or claimed.
CREATE TABLE "AskAlphaVisitorOwnershipStaging" (
  "Id" uuid PRIMARY KEY,
  "WorkspaceId" uuid NOT NULL REFERENCES "WeddingPlannerWorkspaces"("Id"),
  "SessionId" uuid NOT NULL UNIQUE REFERENCES "WeddingPlannerPlanningSessions"("Id"),
  "AdvertiserId" uuid NOT NULL REFERENCES "Advertisers"("Id"),
  "Issuer" varchar(200) NOT NULL CHECK ("Issuer" LIKE 'https://%'),
  "SubjectSha256" char(64) NOT NULL CHECK ("SubjectSha256" ~ '^[0-9A-F]{64}$'),
  "RequestKey" varchar(100) NOT NULL CHECK ("RequestKey" ~ '^[A-Za-z0-9_-]{8,100}$'),
  "CreatedAt" timestamptz NOT NULL,
  UNIQUE ("WorkspaceId","Issuer","SubjectSha256","RequestKey"),
  UNIQUE ("Id","WorkspaceId","Issuer","SubjectSha256")
);
CREATE TABLE "AskAlphaVisitorConsentStaging" (
  "Id" uuid PRIMARY KEY,
  "OwnershipId" uuid NOT NULL,
  "WorkspaceId" uuid NOT NULL,
  "Issuer" varchar(200) NOT NULL,
  "SubjectSha256" char(64) NOT NULL,
  "Action" varchar(8) NOT NULL CHECK ("Action" IN ('OPT_IN','REVOKE')),
  "Purpose" varchar(32) NOT NULL CHECK ("Purpose" = 'CREATIVE_PREVIEW_ONLY'),
  "NoticeVersion" varchar(64) NOT NULL CHECK ("NoticeVersion" = 'STAGING-NOT-LEGAL-V1'),
  "RetryKey" varchar(100) NOT NULL CHECK ("RetryKey" ~ '^[A-Za-z0-9_-]{8,100}$'),
  "RecordedAt" timestamptz NOT NULL,
  FOREIGN KEY ("OwnershipId","WorkspaceId","Issuer","SubjectSha256")
    REFERENCES "AskAlphaVisitorOwnershipStaging"
      ("Id","WorkspaceId","Issuer","SubjectSha256"),
  UNIQUE ("OwnershipId","Action"),
  UNIQUE ("OwnershipId","RetryKey")
);
CREATE FUNCTION agm_staging_no_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'AGM Stage J staging evidence is append-only' USING ERRCODE='23514';
END $$;
CREATE FUNCTION agm_staging_check_ownership() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE session_info record;
BEGIN
  -- A marker on an existing staff-created session is not cryptographic proof
  -- of visitor ownership. This is a **synthetic** isolated test precondition.
  SELECT s."WorkspaceId",s."AdvertiserId",s."Status",s."SourceSystem",
         s."CreatedAt",w."Status" AS workspace_status,w."AdvertiserId" AS workspace_advertiser
    INTO session_info
    FROM "WeddingPlannerPlanningSessions" s
    JOIN "WeddingPlannerWorkspaces" w ON w."Id"=s."WorkspaceId"
    WHERE s."Id"=NEW."SessionId" FOR SHARE OF s,w;
  IF NOT FOUND OR session_info."WorkspaceId" <> NEW."WorkspaceId"
    OR session_info."AdvertiserId" <> NEW."AdvertiserId"
    OR session_info.workspace_advertiser <> NEW."AdvertiserId"
    OR session_info."Status" <> 'OPEN'
    OR session_info.workspace_status <> 'ACTIVE'
    OR session_info."SourceSystem" <> 'STAGING_SYNTHETIC_VISITOR_NEW_SESSION'
    OR NEW."CreatedAt" < session_info."CreatedAt" THEN
    RAISE EXCEPTION 'no scoped synthetic new visitor session' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $$;
CREATE FUNCTION agm_staging_check_consent() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE opened timestamptz;
DECLARE optin timestamptz;
DECLARE revoked boolean;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended(NEW."OwnershipId"::text, 5017));
  SELECT "CreatedAt" INTO opened FROM "AskAlphaVisitorOwnershipStaging"
    WHERE "Id"=NEW."OwnershipId" FOR SHARE;
  SELECT "RecordedAt" INTO optin FROM "AskAlphaVisitorConsentStaging"
    WHERE "OwnershipId"=NEW."OwnershipId" AND "Action"='OPT_IN';
  SELECT EXISTS(SELECT 1 FROM "AskAlphaVisitorConsentStaging"
    WHERE "OwnershipId"=NEW."OwnershipId" AND "Action"='REVOKE') INTO revoked;
  IF opened IS NULL OR revoked OR
     (NEW."Action"='OPT_IN' AND
        (optin IS NOT NULL OR NEW."RecordedAt" < opened)) OR
     (NEW."Action"='REVOKE' AND
        (optin IS NULL OR NEW."RecordedAt" < optin)) THEN
    RAISE EXCEPTION 'staging consent transition denied' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER agm_ownership_scope BEFORE INSERT ON "AskAlphaVisitorOwnershipStaging"
 FOR EACH ROW EXECUTE FUNCTION agm_staging_check_ownership();
CREATE TRIGGER agm_consent_transition BEFORE INSERT ON "AskAlphaVisitorConsentStaging"
 FOR EACH ROW EXECUTE FUNCTION agm_staging_check_consent();
CREATE TRIGGER agm_ownership_immutable BEFORE UPDATE OR DELETE ON "AskAlphaVisitorOwnershipStaging"
 FOR EACH ROW EXECUTE FUNCTION agm_staging_no_mutation();
CREATE TRIGGER agm_consent_immutable BEFORE UPDATE OR DELETE ON "AskAlphaVisitorConsentStaging"
 FOR EACH ROW EXECUTE FUNCTION agm_staging_no_mutation();
CREATE TRIGGER agm_ownership_no_truncate BEFORE TRUNCATE ON "AskAlphaVisitorOwnershipStaging"
 FOR EACH STATEMENT EXECUTE FUNCTION agm_staging_no_mutation();
CREATE TRIGGER agm_consent_no_truncate BEFORE TRUNCATE ON "AskAlphaVisitorConsentStaging"
 FOR EACH STATEMENT EXECUTE FUNCTION agm_staging_no_mutation();
