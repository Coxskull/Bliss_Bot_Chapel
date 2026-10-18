-- Disposable GitHub Actions PostgreSQL 17 ONLY; NEVER run on a shared database.
-- Creates only fictional schema objects, trigger functions and non-login roles.
CREATE ROLE anon NOLOGIN;
CREATE ROLE authenticated NOLOGIN;
CREATE ROLE service_role NOLOGIN;
GRANT USAGE ON SCHEMA public TO anon,authenticated,service_role;

CREATE OR REPLACE FUNCTION public.rls_auto_enable()
RETURNS event_trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO pg_catalog
AS $function$
DECLARE cmd record;
BEGIN
  FOR cmd IN
    SELECT * FROM pg_event_trigger_ddl_commands()
    WHERE command_tag IN ('CREATE TABLE','CREATE TABLE AS','SELECT INTO')
      AND object_type IN ('table','partitioned table')
  LOOP
    IF cmd.schema_name IS NOT NULL
       AND cmd.schema_name IN ('public')
       AND cmd.schema_name NOT IN ('pg_catalog','information_schema')
       AND cmd.schema_name NOT LIKE 'pg_toast%'
       AND cmd.schema_name NOT LIKE 'pg_temp%' THEN
      BEGIN
        EXECUTE format('alter table if exists %s enable row level security',cmd.object_identity);
      EXCEPTION WHEN OTHERS THEN
        RAISE EXCEPTION 'synthetic ensure_rls failed on %: %',cmd.object_identity,SQLERRM;
      END;
    END IF;
  END LOOP;
END;
$function$;

CREATE EVENT TRIGGER ensure_rls
ON ddl_command_end
EXECUTE FUNCTION public.rls_auto_enable();

CREATE OR REPLACE FUNCTION public.set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$function$;

GRANT EXECUTE ON FUNCTION public.rls_auto_enable()
TO PUBLIC,anon,authenticated,service_role;

CREATE TABLE public.lead_test_before (
  id integer PRIMARY KEY,
  updated_at timestamptz NOT NULL
);
CREATE TRIGGER stamp_before
BEFORE UPDATE ON public.lead_test_before
FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();
INSERT INTO public.lead_test_before VALUES (1,'2020-01-01T00:00:00Z');

-- Persist only function-source and trigger-configuration metadata in session.
CREATE TEMP TABLE lead_source_before AS
SELECT p.proname,p.prosrc,p.prosecdef,p.prorettype
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.proname IN ('rls_auto_enable','set_updated_at');

DO $assert$
BEGIN
  IF NOT (SELECT relrowsecurity FROM pg_class WHERE oid='public.lead_test_before'::regclass)
  THEN RAISE EXCEPTION 'Pre-hardening CREATE TABLE did not receive automatic RLS'; END IF;
  IF NOT has_function_privilege('anon','public.rls_auto_enable()','EXECUTE')
  THEN RAISE EXCEPTION 'Expected public EXECUTE before hardening'; END IF;
END;
$assert$;
