-- Read-only, metadata-only checks. All four rows must report passed=true.
-- No customer rows, grants, role switching or permission changes.
WITH targets AS (
  SELECT c.oid,c.relname,c.relrowsecurity
  FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
  WHERE n.nspname='public' AND c.relname IN
    ('creator_applications','advertiser_inquiries','contact_inquiries')
    AND c.relkind IN ('r','p')
), checked AS (
  SELECT t.relname::text AS object_name,
    t.relrowsecurity
    AND NOT EXISTS (
      SELECT 1 FROM pg_roles r
      CROSS JOIN (VALUES ('SELECT'),('INSERT'),('UPDATE'),('DELETE'),('TRUNCATE'),('REFERENCES'),('TRIGGER'),('MAINTAIN')) p(privilege)
      WHERE r.rolname IN ('anon','authenticated')
        AND has_table_privilege(r.oid,t.oid,p.privilege))
    AND NOT EXISTS (
      SELECT 1 FROM pg_attribute a
      WHERE a.attrelid=t.oid AND a.attnum>0 AND NOT a.attisdropped
        AND a.attacl IS NOT NULL)
    AND EXISTS (SELECT 1 FROM pg_roles WHERE rolname='service_role' AND rolbypassrls)
    AND has_table_privilege('service_role',t.oid,'SELECT')
    AND has_table_privilege('service_role',t.oid,'INSERT')
    AND has_table_privilege('service_role',t.oid,'UPDATE')
    AND has_table_privilege('service_role',t.oid,'DELETE') AS passed
  FROM targets t
  UNION ALL
  SELECT 'alpha_lead_summary',
    EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='public' AND c.relname='alpha_lead_summary'
        AND c.relkind='v' AND 'security_invoker=true'=ANY(c.reloptions))
    AND NOT has_table_privilege('anon','public.alpha_lead_summary','SELECT')
    AND NOT has_table_privilege('authenticated','public.alpha_lead_summary','SELECT')
)
SELECT object_name,passed FROM checked ORDER BY object_name;
-- Missing target rows or any false/null is FAIL, not partial success.
-- This is catalog acceptance only, not proof of actual HTTP/API integration.
