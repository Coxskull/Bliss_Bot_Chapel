-- AGM-COMM-015: isolated test-only RLS rollback rehearsal.
-- The DROP statements target only tables/indexes in the disposable schema
-- selected by the test's exact search_path. NEVER run on a hosted database.
DROP POLICY IF EXISTS agm_fixture_owner_exact_scope_select
  ON "AskAlphaVisitorOwnershipStaging";
DROP POLICY IF EXISTS agm_fixture_consent_exact_scope_select
  ON "AskAlphaVisitorConsentStaging";

ALTER TABLE "AskAlphaVisitorOwnershipStaging" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "AskAlphaVisitorOwnershipStaging" DISABLE ROW LEVEL SECURITY;
ALTER TABLE "AskAlphaVisitorConsentStaging" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "AskAlphaVisitorConsentStaging" DISABLE ROW LEVEL SECURITY;

DROP INDEX IF EXISTS "agm_fixture_ownership_scope_idx";
DROP INDEX IF EXISTS "agm_fixture_consent_scope_idx";
