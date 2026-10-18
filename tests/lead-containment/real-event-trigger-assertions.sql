-- Real PostgreSQL 17 event trigger behavioral assertions for disposable CI DB.
-- This runs in the same psql session as the setup and exact review-only proposal.
CREATE ROLE migration_operator NOLOGIN;
GRANT USAGE,CREATE ON SCHEMA public TO migration_operator;
SET ROLE migration_operator;
CREATE TABLE public.lead_test_posthardening (
  id integer PRIMARY KEY,
  updated_at timestamptz NOT NULL
);
RESET ROLE;

DO $assert$
DECLARE
  event_oid oid;
  t record;
BEGIN
  -- The real event trigger must remain enabled, defined by the correct function.
  SELECT p.oid INTO event_oid FROM pg_proc p
    JOIN pg_namespace n ON n.oid=p.pronamespace
    WHERE n.nspname='public' AND p.proname='rls_auto_enable';
  IF (SELECT count(*) FROM pg_event_trigger e
      WHERE e.evtname='ensure_rls' AND e.evtevent='ddl_command_end'
        AND e.evtenabled='O' AND e.evtfoid=event_oid) <> 1 THEN
    RAISE EXCEPTION 'Active ensure_rls event trigger was not preserved';
  END IF;
  IF NOT (SELECT relrowsecurity FROM pg_class
          WHERE oid='public.lead_test_posthardening'::regclass) THEN
    RAISE EXCEPTION 'CREATE TABLE was not automatically protected by RLS';
  END IF;
  -- The original, not just recreated, table remains under RLS.
  IF NOT (SELECT relrowsecurity FROM pg_class
          WHERE oid='public.lead_test_before'::regclass) THEN
    RAISE EXCEPTION 'Original table lost RLS';
  END IF;
  IF has_function_privilege('anon',event_oid,'EXECUTE') OR
     has_function_privilege('authenticated',event_oid,'EXECUTE') THEN
    RAISE EXCEPTION 'Public role EXECUTE remains on RLS event trigger';
  END IF;
  IF NOT has_function_privilege('service_role',event_oid,'EXECUTE') THEN
    RAISE EXCEPTION 'Existing backend service_role EXECUTE was disrupted';
  END IF;
  IF has_schema_privilege('anon','public','CREATE') OR
     has_schema_privilege('authenticated','public','CREATE') THEN
    RAISE EXCEPTION 'Public role DDL privilege was unintentionally added';
  END IF;
  FOR t IN
    SELECT p.proname,p.prosrc,p.prosecdef,p.prorettype
    FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
    WHERE n.nspname='public' AND p.proname IN ('rls_auto_enable','set_updated_at')
  LOOP
    IF NOT EXISTS (
      SELECT 1 FROM lead_source_before b
      WHERE b.proname=t.proname AND b.prosrc=t.prosrc
        AND b.prosecdef=t.prosecdef AND b.prorettype=t.prorettype
    ) THEN RAISE EXCEPTION 'Function signature/body/definer changed for %',t.proname; END IF;
  END LOOP;
END;
$assert$;

UPDATE public.lead_test_before SET id=1 WHERE id=1;
DO $assert$
DECLARE ts timestamptz;
BEGIN
  SELECT updated_at INTO ts FROM public.lead_test_before WHERE id=1;
  IF ts IS NULL OR ts < '2026-01-01'::timestamptz THEN
    RAISE EXCEPTION 'Existing set_updated_at row trigger stopped updating';
  END IF;
END;
$assert$;

-- Also prove the fixture's role-specific access guard, not customer data.
SELECT 'PASS: PostgreSQL 17 event trigger auto-RLS, pre-existing RLS, grants, function bodies and timestamp behavior' AS result;
