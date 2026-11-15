-- Task #21 isolated PostgreSQL acceptance fixture.
-- Runs ONLY in a disposable CI PostgreSQL container; no Supabase, Railway,
-- HighLevel, mail, production auth, or operator identity is contacted or copied.
\set ON_ERROR_STOP on
CREATE ROLE anon NOLOGIN;
CREATE ROLE authenticated NOLOGIN;
CREATE ROLE service_role NOLOGIN;

CREATE SCHEMA auth;
CREATE TABLE auth.users(id uuid PRIMARY KEY);
CREATE SCHEMA alpha_support_private;

CREATE TABLE public.alpha_public_intake_requests(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 kind text NOT NULL,
 processing_acknowledged boolean NOT NULL DEFAULT false,
 adult_confirmed boolean NOT NULL DEFAULT false,
 created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE alpha_support_private.manual_mail_cases(
 case_id uuid PRIMARY KEY,
 category text NOT NULL,
 status text NOT NULL DEFAULT 'OPEN',
 observed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE alpha_support_private.manual_operator_grants(
 user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
 role text NOT NULL DEFAULT 'GENERAL_SUPPORT' CHECK(role='GENERAL_SUPPORT'),
 allowed_origin text NOT NULL DEFAULT 'https://alphacreativecontent.com',
 enabled boolean NOT NULL DEFAULT false
);
CREATE TABLE alpha_support_private.manual_operator_claims(
 case_id uuid PRIMARY KEY REFERENCES alpha_support_private.manual_mail_cases(case_id),
 actor_user_id uuid NOT NULL
);
CREATE TABLE alpha_support_private.manual_operator_actions(
 event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 case_id uuid NOT NULL REFERENCES alpha_support_private.manual_mail_cases(case_id),
 actor_user_id uuid NOT NULL,
 action text NOT NULL
);

-- Mirror the already-deployed Task21 allowlist authorization with
-- a real auth.users join; no operator grant is seeded, even synthetically.
CREATE FUNCTION public.alpha_support_manual_operator_authorized(p_actor_user_id uuid)
RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog AS $authorized$
 SELECT p_actor_user_id IS NOT NULL AND EXISTS(
  SELECT 1 FROM alpha_support_private.manual_operator_grants g
   JOIN auth.users u ON u.id=g.user_id
  WHERE g.user_id=p_actor_user_id AND g.role='GENERAL_SUPPORT'
    AND g.allowed_origin='https://alphacreativecontent.com' AND g.enabled=true
 );
$authorized$;

-- Replay the complete production DDL against an empty, unmapped database.
\i scripts/task21/production-privacy-form-private-queue-20261112.sql

-- A real privacy form yields metadata-only queue/event records, but a
-- missing operator and unknown actor MUST NOT gain staff access.
DO $verify_no_staff$
DECLARE
 actor uuid:='00000000-0000-4000-8000-000000000001'::uuid;
 rejected boolean:=false;
BEGIN
 IF EXISTS(SELECT 1 FROM auth.users) OR
    EXISTS(SELECT 1 FROM alpha_support_private.manual_operator_grants)
 THEN RAISE EXCEPTION 'TASK21_CI_FAKE_OPERATOR_CREATED'; END IF;
 IF public.alpha_support_manual_operator_authorized(actor)
    OR public.alpha_support_manual_operator_authorized(NULL)
 THEN RAISE EXCEPTION 'TASK21_CI_OPERATOR_BYPASS'; END IF;
 BEGIN
  PERFORM * FROM public.alpha_support_manual_operator_queue(actor,0);
 EXCEPTION WHEN insufficient_privilege THEN
  rejected:=true;
 END;
 IF NOT rejected THEN RAISE EXCEPTION 'TASK21_CI_QUEUE_AUTH_BYPASS'; END IF;
 IF public.alpha_support_manual_operator_claim(
   actor,'00000000-0000-4000-8000-000000000002'::uuid)
 THEN RAISE EXCEPTION 'TASK21_CI_CLAIM_AUTH_BYPASS'; END IF;
 IF has_schema_privilege('anon','alpha_support_private','USAGE')
    OR has_schema_privilege('authenticated','alpha_support_private','USAGE')
    OR has_schema_privilege('service_role','alpha_support_private','USAGE')
 THEN RAISE EXCEPTION 'TASK21_CI_PRIVATE_SCHEMA_GRANT'; END IF;
 IF has_function_privilege('anon',
   'public.alpha_support_manual_operator_queue(uuid,integer)','EXECUTE')
    OR has_function_privilege('authenticated',
   'public.alpha_support_manual_operator_claim(uuid,uuid)','EXECUTE')
 THEN RAISE EXCEPTION 'TASK21_CI_PUBLIC_STAFF_FUNCTION'; END IF;
END $verify_no_staff$;

INSERT INTO public.alpha_public_intake_requests(
 id,kind,processing_acknowledged,adult_confirmed)
VALUES('00000000-0000-4000-8000-000000000002','privacy',true,true);

DO $verify_metadata$
BEGIN
 IF (SELECT count(*) FROM alpha_support_private.manual_privacy_form_cases)<>1
    OR (SELECT count(*) FROM alpha_support_private.manual_privacy_form_events
        WHERE event_type='CREATED')<>1
 THEN RAISE EXCEPTION 'TASK21_CI_PRIVACY_METADATA_QUEUE_FAILED'; END IF;
 IF (SELECT count(*) FROM alpha_support_private.manual_privacy_operator_claims)<>0
    OR (SELECT count(*) FROM auth.users)<>0
 THEN RAISE EXCEPTION 'TASK21_CI_OPERATOR_AUTO_PROVISIONED'; END IF;
END $verify_metadata$;
SELECT 'TASK21_EMPTY_BRANCH_REPLAY_PASS default_deny no_operator_seed no_egress' AS acceptance;
