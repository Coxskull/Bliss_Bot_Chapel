-- Read-only Postgres metadata; no customer data, DDL, role switching or secrets.
-- Exactly TWO rows, both passed=true, required for function-advisor acceptance.
-- Independent of RLS table containment: see lead-generation-containment-verification.sql.
WITH func AS (
  SELECT p.oid,p.proname,p.prosecdef,p.prorettype,p.proconfig,
         n.nspname
  FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
  WHERE n.nspname='public' AND p.proname IN ('rls_auto_enable','set_updated_at')
)
SELECT 'rls_auto_enable' AS object_name,
   EXISTS (
     SELECT 1 FROM func f
     WHERE f.proname='rls_auto_enable'
       AND f.prosecdef
       AND f.prorettype = 'pg_catalog.event_trigger'::regtype
       AND NOT has_function_privilege('anon',f.oid,'EXECUTE')
       AND NOT has_function_privilege('authenticated',f.oid,'EXECUTE')
       AND EXISTS (
         SELECT 1 FROM pg_event_trigger e
         WHERE e.evtname='ensure_rls'
           AND e.evtevent='ddl_command_end'
           AND e.evtenabled='O'
           AND e.evtfoid=f.oid
       )
   ) AS passed
UNION ALL
SELECT 'set_updated_at',
   EXISTS (
     SELECT 1 FROM func f
     WHERE f.proname='set_updated_at'
       AND NOT f.prosecdef
       AND f.prorettype = 'pg_catalog.trigger'::regtype
       AND 'search_path=pg_catalog' = ANY(f.proconfig)
   )
ORDER BY object_name;
-- A missing function, changed event trigger, or remaining public EXECUTE is FAIL.
-- This does NOT demonstrate live PostgREST behavior, app integration, or incident scope.
