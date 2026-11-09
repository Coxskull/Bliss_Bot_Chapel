-- TASK21: scoped, expiring, disabled-by-default SYNTHETIC browser trial.
-- Not a global mutation enablement; no production grants or customer contact.
-- Reviewed staging-only prerequisite: existing private operator grants.
DO $task21_trial_guard$
BEGIN
 IF to_regclass('alpha_support_private.staging_operator_grants') IS NULL
 OR to_regclass('alpha_support_private.staging_case_mutation_trials') IS NOT NULL
 OR to_regclass('public.alpha_support_cases') IS NULL
 THEN RAISE EXCEPTION 'Task21 staging-only browser trial prerequisite/collision'; END IF;
END $task21_trial_guard$;

CREATE TABLE alpha_support_private.staging_case_mutation_trials (
 case_id uuid PRIMARY KEY REFERENCES public.alpha_support_cases(case_id) ON DELETE CASCADE,
 actor_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
 allowed_origin text NOT NULL CHECK (
   allowed_origin ~ '^https://bliss-[a-z0-9]+-alpha-auto-s-projects[.]vercel[.]app$'),
 enabled boolean NOT NULL DEFAULT false,
 created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 expires_at timestamptz NOT NULL,
 CONSTRAINT task21_trial_max_duration CHECK (
   expires_at>created_at AND expires_at<=created_at+interval '4 hours')
);
ALTER TABLE alpha_support_private.staging_case_mutation_trials ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON alpha_support_private.staging_case_mutation_trials
 FROM PUBLIC,anon,authenticated,service_role;
GRANT SELECT ON alpha_support_private.staging_case_mutation_trials TO service_role;

-- Hardcoded test request key deliberately belongs to ONE fictional fixture,
-- not to every contact with an example.invalid address. Read-only inspection
-- is allowed only by service_role, as with the existing case/receipt RPCs.
CREATE FUNCTION public.alpha_support_staging_trial_allowed(
 p_case_id uuid,p_actor_user_id uuid,p_origin text
) RETURNS boolean LANGUAGE sql STABLE SECURITY INVOKER
SET search_path=pg_catalog AS $task21_trial_allowed$
 SELECT EXISTS (
   SELECT 1 FROM alpha_support_private.staging_case_mutation_trials t
   JOIN alpha_support_private.staging_operator_grants g
     ON g.user_id=t.actor_user_id AND g.allowed_origin=t.allowed_origin
     AND g.role='GENERAL_SUPPORT' AND g.enabled
   JOIN public.alpha_support_cases c ON c.case_id=t.case_id
   JOIN public.alpha_public_intake_requests r ON r.id=c.request_id
   WHERE t.enabled AND t.expires_at>clock_timestamp()
     AND t.case_id=p_case_id AND t.actor_user_id=p_actor_user_id
     AND t.allowed_origin=p_origin
     AND c.assigned_role='GENERAL_SUPPORT'
     AND c.category='general' AND c.severity='NORMAL'
     AND c.status IN ('OPEN','TRIAGED','IN_REVIEW','RESOLVED')
     AND r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid
     AND r.email='task21-browser-fixture@example.invalid'
     AND r.marketing_opt_in=false
     AND r.full_name='TASK21 BROWSER MUTATION TEST — FICTIONAL'
 );
$task21_trial_allowed$;
REVOKE ALL ON FUNCTION public.alpha_support_staging_trial_allowed(uuid,uuid,text)
 FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_support_staging_trial_allowed(uuid,uuid,text)
 TO service_role;

-- Rechecks the same private per-case + per-actor + per-origin allowlist inside
-- this single PostgreSQL operation, then invokes the existing audited RPC.
-- The runtime supplies a VERIFIED JWT actor; no account ID from browser JSON.
CREATE FUNCTION public.alpha_support_staging_trial_action(
 p_case_id uuid,p_actor_user_id uuid,p_origin text,
 p_action text,p_status text,p_resolution text
) RETURNS boolean LANGUAGE plpgsql SECURITY INVOKER
SET search_path=pg_catalog AS $task21_trial_action$
DECLARE v_owner uuid;
BEGIN
 IF NOT public.alpha_support_staging_trial_allowed(p_case_id,p_actor_user_id,p_origin)
 THEN RETURN false; END IF;
 IF p_action='CLAIM' THEN
   IF p_status IS NOT NULL OR coalesce(p_resolution,'')<>''
   THEN RETURN false; END IF;
   RETURN public.alpha_support_claim(p_case_id,p_actor_user_id);
 ELSIF p_action='TRANSITION' THEN
   IF p_status NOT IN ('IN_REVIEW','RESOLVED','CLOSED')
     OR p_resolution IS NULL OR length(p_resolution)>1600
     OR (p_status IN ('RESOLVED','CLOSED') AND btrim(p_resolution)='')
   THEN RETURN false; END IF;
   SELECT owner_user_id INTO v_owner FROM public.alpha_support_cases
   WHERE case_id=p_case_id;
   IF v_owner IS DISTINCT FROM p_actor_user_id THEN RETURN false; END IF;
   RETURN public.alpha_support_transition(p_case_id,p_status,
     p_actor_user_id,p_resolution,p_actor_user_id);
 END IF;
 RETURN false;
END;
$task21_trial_action$;
REVOKE ALL ON FUNCTION public.alpha_support_staging_trial_action(uuid,uuid,text,text,text,text)
 FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_support_staging_trial_action(uuid,uuid,text,text,text,text)
 TO service_role;
