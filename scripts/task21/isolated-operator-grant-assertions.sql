-- Zero-customer, zero-auth-secret staging operator permission tests in PG17.
-- Apply after support + alert schemas; work only in disposable CI Postgres.
DO $task21_grant_checks$
DECLARE uid uuid:='00000000-0000-4000-8000-000000008121'::uuid;
  origin text:='https://bliss-task21ci-alpha-auto-s-projects.vercel.app';
  denied boolean:=false;
BEGIN
 INSERT INTO auth.users(id) VALUES(uid);
 INSERT INTO alpha_support_private.staging_operator_grants
   (user_id,role,allowed_origin,enabled)
 VALUES(uid,'GENERAL_SUPPORT',origin,true);
 IF (SELECT count(*) FROM alpha_support_private.staging_operator_grants)<>1
 THEN RAISE EXCEPTION 'Expected single test staff grant'; END IF;
 IF has_table_privilege('anon','alpha_support_private.staging_operator_grants','SELECT')
 OR has_table_privilege('authenticated','alpha_support_private.staging_operator_grants','SELECT')
 OR has_table_privilege('service_role','alpha_support_private.staging_operator_grants','INSERT')
 OR has_table_privilege('service_role','alpha_support_private.staging_operator_grants','UPDATE')
 OR has_table_privilege('service_role','alpha_support_private.staging_operator_grants','DELETE')
 OR NOT has_table_privilege('service_role','alpha_support_private.staging_operator_grants','SELECT')
 OR NOT (SELECT relrowsecurity FROM pg_catalog.pg_class
  WHERE oid='alpha_support_private.staging_operator_grants'::regclass)
 OR has_function_privilege('authenticated',
  'public.alpha_support_staging_origin_allowed(text)','EXECUTE')
 OR has_function_privilege('anon',
  'public.alpha_support_staging_roles(uuid,text,text)','EXECUTE')
 THEN RAISE EXCEPTION 'Grant table or invoker RPC exposed'; END IF;
 BEGIN
 INSERT INTO alpha_support_private.staging_operator_grants
 (user_id,role,allowed_origin,enabled)
 VALUES(uid,'SECURITY_REVIEW',origin,true);
 EXCEPTION WHEN check_violation THEN denied:=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Escalated role accepted'; END IF;
 denied:=false;
 BEGIN
 INSERT INTO alpha_support_private.staging_operator_grants
 (user_id,role,allowed_origin,enabled)
 VALUES(uid,'GENERAL_SUPPORT','http://evil.example.invalid',true);
 EXCEPTION WHEN check_violation THEN denied:=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Insecure operator origin accepted'; END IF;
END;
$task21_grant_checks$;

SET ROLE service_role;
DO $task21_grant_service_role$
DECLARE uid uuid:='00000000-0000-4000-8000-000000008121'::uuid;
 origin text:='https://bliss-task21ci-alpha-auto-s-projects.vercel.app';
 roles text[]; granted boolean;
BEGIN
 SELECT public.alpha_support_staging_origin_allowed(origin) INTO granted;
 IF granted IS DISTINCT FROM true THEN
 RAISE EXCEPTION 'Approved staging origin rejected'; END IF;
 IF public.alpha_support_staging_origin_allowed('https://wrong.example.invalid')
 OR public.alpha_support_staging_origin_allowed('https://bliss-other-alpha-auto-s-projects.vercel.app')
 OR public.alpha_support_staging_origin_allowed(NULL)
 THEN RAISE EXCEPTION 'Untrusted/stale origin accepted'; END IF;
 roles:=public.alpha_support_staging_roles(uid,origin,'aal1');
 IF roles IS DISTINCT FROM ARRAY['GENERAL_SUPPORT']::text[]
 THEN RAISE EXCEPTION 'Expected exact least privilege GENERAL_SUPPORT'; END IF;
 IF public.alpha_support_staging_roles(uid,origin,'aal2') IS DISTINCT FROM ARRAY['GENERAL_SUPPORT']::text[]
 OR coalesce(array_length(public.alpha_support_staging_roles(uid,origin,'aal0'),1),0)<>0
 OR coalesce(array_length(public.alpha_support_staging_roles(
   '00000000-0000-4000-8000-000000008122'::uuid,origin,'aal2'),1),0)<>0
 OR coalesce(array_length(public.alpha_support_staging_roles(
   uid,'https://bliss-other-alpha-auto-s-projects.vercel.app','aal1'),1),0)<>0
 THEN RAISE EXCEPTION 'Forged identity, origin or AAL allowed'; END IF;
END;
$task21_grant_service_role$;
RESET ROLE;

UPDATE alpha_support_private.staging_operator_grants SET enabled=false
WHERE user_id='00000000-0000-4000-8000-000000008121'::uuid;
SET ROLE service_role;
DO $task21_revoked_grant$
BEGIN
 IF public.alpha_support_staging_origin_allowed(
 'https://bliss-task21ci-alpha-auto-s-projects.vercel.app')
 OR coalesce(array_length(public.alpha_support_staging_roles(
 '00000000-0000-4000-8000-000000008121'::uuid,
 'https://bliss-task21ci-alpha-auto-s-projects.vercel.app','aal2'),1),0)<>0
 THEN RAISE EXCEPTION 'Revoked operator still authorized'; END IF;
END;$task21_revoked_grant$;
RESET ROLE;
SELECT 'TASK21_STAGING_OPERATOR_GRANT_PRIVILEGE_PASS' AS acceptance_status;
