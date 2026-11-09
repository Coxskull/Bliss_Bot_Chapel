-- ALPHA Task #21: staging-only server-managed operator grants.
-- No real customer access or production application is permitted.
-- Does not include any identity, password, secret, address or roster entry.
-- This is additive to the existing alpha_support_private staging schema.
DO $task21_staff_preflight$
BEGIN
 IF to_regnamespace('alpha_support_private') IS NULL
 OR to_regclass('public.alpha_support_cases') IS NULL
 OR to_regclass('alpha_support_private.staging_operator_grants') IS NOT NULL
 THEN RAISE EXCEPTION 'TASK21 staging grant schema not isolated or colliding'; END IF;
END;
$task21_staff_preflight$;

CREATE TABLE alpha_support_private.staging_operator_grants (
 user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
 role text NOT NULL CHECK (role='GENERAL_SUPPORT'),
 allowed_origin text NOT NULL CHECK (
   allowed_origin ~ '^https://bliss-[a-z0-9]+-alpha-auto-s-projects[.]vercel[.]app$'
   AND char_length(allowed_origin) BETWEEN 40 AND 180),
 enabled boolean NOT NULL DEFAULT false,
 granted_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 PRIMARY KEY (user_id,role,allowed_origin)
);
ALTER TABLE alpha_support_private.staging_operator_grants ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON alpha_support_private.staging_operator_grants
 FROM PUBLIC,anon,authenticated,service_role;
GRANT SELECT ON alpha_support_private.staging_operator_grants TO service_role;

-- Invoker RPCs are executable by the trusted Edge runtime's service_role ONLY.
-- They are not readable from browser Supabase anon/authenticated tokens.
CREATE FUNCTION public.alpha_support_staging_origin_allowed(p_origin text)
RETURNS boolean LANGUAGE sql STABLE SECURITY INVOKER
SET search_path=pg_catalog AS $task21_origin$
 SELECT p_origin IS NOT NULL AND char_length(p_origin) BETWEEN 40 AND 180
   AND EXISTS (
     SELECT 1 FROM alpha_support_private.staging_operator_grants g
     WHERE g.enabled AND g.allowed_origin=p_origin
       AND g.role='GENERAL_SUPPORT'
   );
$task21_origin$;
REVOKE ALL ON FUNCTION public.alpha_support_staging_origin_allowed(text)
 FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_support_staging_origin_allowed(text)
 TO service_role;

CREATE FUNCTION public.alpha_support_staging_roles(
 p_user_id uuid,p_origin text,p_aal text
)
RETURNS text[] LANGUAGE sql STABLE SECURITY INVOKER
SET search_path=pg_catalog AS $task21_roles$
 SELECT ARRAY(
   SELECT DISTINCT g.role::text
   FROM alpha_support_private.staging_operator_grants g
   WHERE g.enabled AND g.user_id=p_user_id
     AND g.allowed_origin=p_origin AND g.role='GENERAL_SUPPORT'
     AND p_aal IN ('aal1','aal2')
   ORDER BY 1
 );
$task21_roles$;
REVOKE ALL ON FUNCTION public.alpha_support_staging_roles(uuid,text,text)
 FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_support_staging_roles(uuid,text,text)
 TO service_role;
