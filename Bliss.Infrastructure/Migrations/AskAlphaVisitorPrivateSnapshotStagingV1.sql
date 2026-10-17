-- AGM-COMM-015: isolated SYNTHETIC encrypted snapshot candidate only.
-- Install AFTER AskAlphaVisitorOwnershipStagingV1.sql in a fresh agm_j_<uuid>
-- search_path on loopback planner_lineage_ci or bliss_ci. NOT an EF migration.
-- No hosted registration, production DDL, live consent or transcript exports.
CREATE TABLE "AskAlphaVisitorSnapshotStaging" (
  "Id" uuid PRIMARY KEY,
  "OwnershipId" uuid NOT NULL,
  "WorkspaceId" uuid NOT NULL,
  "Issuer" varchar(200) NOT NULL,
  "SubjectSha256" char(64) NOT NULL CHECK ("SubjectSha256" ~ '^[0-9A-F]{64}$'),
  "Purpose" varchar(32) NOT NULL CHECK ("Purpose" = 'CREATIVE_PREVIEW_ONLY'),
  "NoticeVersion" varchar(64) NOT NULL CHECK ("NoticeVersion" = 'STAGING-NOT-LEGAL-V1'),
  "RetryKey" varchar(100) NOT NULL CHECK ("RetryKey" ~ '^[A-Za-z0-9_-]{8,100}$'),
  "ContentSha256" char(64) NOT NULL CHECK ("ContentSha256" ~ '^[0-9A-F]{64}$'),
  "Ciphertext" bytea NOT NULL CHECK (octet_length("Ciphertext") BETWEEN 1 AND 65536),
  "Nonce" bytea NOT NULL CHECK (octet_length("Nonce") = 12),
  "Tag" bytea NOT NULL CHECK (octet_length("Tag") = 16),
  "CreatedAt" timestamptz NOT NULL,
  FOREIGN KEY ("OwnershipId","WorkspaceId","Issuer","SubjectSha256")
    REFERENCES "AskAlphaVisitorOwnershipStaging"
      ("Id","WorkspaceId","Issuer","SubjectSha256"),
  UNIQUE ("OwnershipId","RetryKey"),
  UNIQUE ("OwnershipId","Purpose","NoticeVersion")
);

CREATE FUNCTION agm_snapshot_before_insert() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE opt_in_at timestamptz;
BEGIN
  -- Same identity lock as atomic enrollment and append-only consent.
  -- Serialize a snapshot creation versus a terminal withdrawal.
  PERFORM pg_advisory_xact_lock(hashtextextended(
    replace(NEW."WorkspaceId"::text,'-','') || '|' ||
    NEW."Issuer" || '|' || NEW."SubjectSha256"::text, 5018));
  PERFORM pg_advisory_xact_lock(hashtextextended(NEW."OwnershipId"::text, 5017));

  SELECT MIN(c."RecordedAt") INTO opt_in_at
  FROM "AskAlphaVisitorConsentStaging" c
  WHERE c."OwnershipId" = NEW."OwnershipId"
    AND c."WorkspaceId" = NEW."WorkspaceId"
    AND c."Issuer" = NEW."Issuer"
    AND c."SubjectSha256" = NEW."SubjectSha256"
    AND c."Purpose" = NEW."Purpose"
    AND c."NoticeVersion" = NEW."NoticeVersion"
    AND c."Action" = 'OPT_IN';

  IF opt_in_at IS NULL OR EXISTS (
    SELECT 1 FROM "AskAlphaVisitorConsentStaging" c
    WHERE c."OwnershipId"=NEW."OwnershipId" AND c."Action"='REVOKE'
  ) OR NOT EXISTS (
    SELECT 1 FROM "AskAlphaVisitorOwnershipStaging" o
    JOIN "WeddingPlannerPlanningSessions" s ON s."Id"=o."SessionId"
    JOIN "WeddingPlannerWorkspaces" w ON w."Id"=o."WorkspaceId"
    WHERE o."Id"=NEW."OwnershipId" AND o."WorkspaceId"=NEW."WorkspaceId"
      AND o."Issuer"=NEW."Issuer" AND o."SubjectSha256"=NEW."SubjectSha256"
      AND s."WorkspaceId"=o."WorkspaceId" AND s."AdvertiserId"=o."AdvertiserId"
      AND s."SourceSystem"='STAGING_SYNTHETIC_VISITOR_NEW_SESSION'
      AND s."Status"='OPEN' AND w."Status"='ACTIVE'
      AND w."AdvertiserId"=o."AdvertiserId"
  ) OR NEW."CreatedAt" < opt_in_at THEN
    RAISE EXCEPTION 'Synthetic snapshot scope or consent invalid'
      USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER agm_snapshot_eligible BEFORE INSERT ON "AskAlphaVisitorSnapshotStaging"
 FOR EACH ROW EXECUTE FUNCTION agm_snapshot_before_insert();
CREATE TRIGGER agm_snapshot_immutable BEFORE UPDATE OR DELETE ON "AskAlphaVisitorSnapshotStaging"
 FOR EACH ROW EXECUTE FUNCTION agm_staging_no_mutation();
CREATE TRIGGER agm_snapshot_no_truncate BEFORE TRUNCATE ON "AskAlphaVisitorSnapshotStaging"
 FOR EACH STATEMENT EXECUTE FUNCTION agm_staging_no_mutation();
