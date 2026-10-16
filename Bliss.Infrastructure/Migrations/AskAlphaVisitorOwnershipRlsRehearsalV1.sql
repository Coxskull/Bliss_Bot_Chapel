-- AGM-COMM-015: REVIEW-ONLY synthetic row-level isolation rehearsal.
-- This file is NOT an EF migration, production policy, or OIDC identity proof.
-- Install only after AskAlphaVisitorOwnershipStagingV1.sql within a unique
-- disposable agm_j_<uuid> schema of the dedicated loopback CI database.
-- The test harness grants SELECT-only rights to its short-lived NOLOGIN role.
-- app.* / agm.fixture_* custom settings can be set by arbitrary SQL callers:
-- they are NOT an authenticated tenant-context mechanism for production.
--
-- These READ-ONLY policies deny without all three exact synthetic inputs.
-- They do not authorize consent, transcript sharing, writes, or payments.
CREATE INDEX "agm_fixture_ownership_scope_idx"
  ON "AskAlphaVisitorOwnershipStaging"
  ("WorkspaceId","Issuer","SubjectSha256");
CREATE INDEX "agm_fixture_consent_scope_idx"
  ON "AskAlphaVisitorConsentStaging"
  ("WorkspaceId","Issuer","SubjectSha256");

ALTER TABLE "AskAlphaVisitorOwnershipStaging" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AskAlphaVisitorOwnershipStaging" FORCE ROW LEVEL SECURITY;
ALTER TABLE "AskAlphaVisitorConsentStaging" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AskAlphaVisitorConsentStaging" FORCE ROW LEVEL SECURITY;

CREATE POLICY agm_fixture_owner_exact_scope_select
  ON "AskAlphaVisitorOwnershipStaging"
  AS PERMISSIVE FOR SELECT TO PUBLIC
  USING (
    "WorkspaceId"::text = current_setting('agm.fixture_workspace_id', true)
    AND "Issuer" = current_setting('agm.fixture_issuer', true)
    AND "SubjectSha256"::text =
      current_setting('agm.fixture_subject_sha256', true)
  );

CREATE POLICY agm_fixture_consent_exact_scope_select
  ON "AskAlphaVisitorConsentStaging"
  AS PERMISSIVE FOR SELECT TO PUBLIC
  USING (
    "WorkspaceId"::text = current_setting('agm.fixture_workspace_id', true)
    AND "Issuer" = current_setting('agm.fixture_issuer', true)
    AND "SubjectSha256"::text =
      current_setting('agm.fixture_subject_sha256', true)
  );
