-- Executed ONLY against an empty disposable PG17 CI database.
INSERT INTO public.alpha_public_intake_requests
(request_key,kind,full_name,email,category,description,notice_version)
SELECT gen_random_uuid(),'support','Fictional '||category,
  replace(category,'_','-')||'@example.invalid',
  category,'Synthetic fixture, no real customer information','AGM-SUPPORT-2026-11-08'
FROM (VALUES ('general'),('advertiser'),('creator'),('billing'),('refund'),
 ('failed_campaign'),('privacy'),('security_incident')) AS category_list(category);
-- Existing creator route remains functional and uses original GHL outbox.
INSERT INTO public.alpha_public_intake_requests
(request_key,kind,full_name,email,category,market,website,description,notice_version)
VALUES(gen_random_uuid(),'creator','Synthetic Creator','creator@unit.test','Podcast',
 'Manila','https://example.invalid/channel','','AGM-INTEREST-2026-11-05-GHL');

DO $$
DECLARE case_ref uuid; owner_ref uuid:=gen_random_uuid();
 contender_ref uuid:=gen_random_uuid(); v_failed boolean;
BEGIN
 IF (SELECT count(*) FROM public.alpha_support_cases)<>8 THEN
   RAISE EXCEPTION 'Expected 8 actual persisted support cases'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_case_events WHERE action='CREATED')<>8
 THEN RAISE EXCEPTION 'Missing initial history events'; END IF;
 IF (SELECT count(*) FROM public.alpha_public_intake_receipts)<>9
 THEN RAISE EXCEPTION 'Missing atomic intake receipts'; END IF;
 IF (SELECT count(*) FROM public.alpha_ghl_delivery)<>4
 THEN RAISE EXCEPTION 'CRM outbox must contain 3 general cases + original creator only'; END IF;
 IF EXISTS (
   SELECT 1 FROM public.alpha_ghl_delivery d
   JOIN public.alpha_public_intake_requests r ON r.id=d.request_id
   WHERE r.kind='support' AND r.category IN
   ('billing','refund','failed_campaign','privacy','security_incident'))
 THEN RAISE EXCEPTION 'Sensitive support case leaked into CRM outbox'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_cases WHERE severity='HIGH')<>4
 OR (SELECT count(*) FROM public.alpha_support_cases WHERE severity='CRITICAL')<>1
 OR (SELECT count(*) FROM public.alpha_support_cases WHERE severity='NORMAL')<>3
 THEN RAISE EXCEPTION 'Priority routing is wrong'; END IF;
 IF NOT EXISTS(SELECT 1 FROM public.alpha_support_cases WHERE
   category='security_incident' AND assigned_role='SECURITY_REVIEW' AND status='OPEN')
 THEN RAISE EXCEPTION 'Critical security escalation not queued'; END IF;
 -- Support cases must use support-specific notice even when written with
 -- privileged server credentials; older interest notice cannot be replayed.
 v_failed:=false;
 BEGIN
   INSERT INTO public.alpha_public_intake_requests
   (request_key,kind,full_name,email,category,description,notice_version)
   VALUES(gen_random_uuid(),'support','Synthetic Wrong Consent','wrong@unit.test',
     'general','Fictional wrong notice','AGM-INTEREST-2026-11-05-GHL');
 EXCEPTION WHEN others THEN v_failed:=true; END;
 IF NOT v_failed THEN RAISE EXCEPTION 'Old notice wrongly authorized support case'; END IF;
 v_failed:=false;
 BEGIN
   INSERT INTO public.alpha_public_intake_requests
   (request_key,kind,full_name,email,category,description,notice_version)
   VALUES(gen_random_uuid(),'privacy','Synthetic Wrong Support Notice','wrongprivacy@unit.test',
     '','Fictional old privacy path','AGM-SUPPORT-2026-11-08');
 EXCEPTION WHEN others THEN v_failed:=true; END;
 IF NOT v_failed THEN RAISE EXCEPTION 'Support notice wrongly accepted for privacy intake'; END IF;
 IF has_table_privilege('service_role','public.alpha_support_case_events','DELETE')
 THEN RAISE EXCEPTION 'Service role may not directly delete support history'; END IF;
 IF has_table_privilege('anon','public.alpha_support_cases','SELECT')
 OR has_table_privilege('authenticated','public.alpha_support_case_events','SELECT')
 OR has_function_privilege('anon',
   'public.alpha_support_transition(uuid,text,uuid,text,uuid)','EXECUTE')
 OR has_function_privilege('authenticated',
   'public.alpha_support_claim(uuid,uuid)','EXECUTE')
 THEN RAISE EXCEPTION 'Public support table or RPC exposure'; END IF;
 -- Synthetic critical incident must have ONE durable human acknowledgement.
 SELECT case_id INTO case_ref FROM public.alpha_support_cases
 WHERE category='security_incident';
 INSERT INTO auth.users(id) VALUES(owner_ref),(contender_ref);
 IF public.alpha_support_claim(case_ref,owner_ref) IS DISTINCT FROM true
 OR public.alpha_support_claim(case_ref,owner_ref) IS DISTINCT FROM true
 THEN RAISE EXCEPTION 'Self-claim and idempotent retry must succeed'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_case_events
       WHERE case_id=case_ref AND action='ASSIGNED')<>1
 OR NOT EXISTS (SELECT 1 FROM public.alpha_support_cases
       WHERE case_id=case_ref AND owner_user_id=owner_ref AND status='TRIAGED')
 THEN RAISE EXCEPTION 'Claim lost its durable owner or duplicated audit event'; END IF;
 IF public.alpha_support_claim(case_ref,contender_ref) IS DISTINCT FROM false
 THEN RAISE EXCEPTION 'Second operator stole claimed incident'; END IF;
 v_failed:=false;
 BEGIN PERFORM public.alpha_support_claim(case_ref,gen_random_uuid());
 EXCEPTION WHEN others THEN v_failed:=true; END;
 IF NOT v_failed THEN RAISE EXCEPTION 'Unknown operator claim accepted'; END IF;
 v_failed:=false;
 BEGIN PERFORM public.alpha_support_transition(
    case_ref,'IN_REVIEW',contender_ref,'',contender_ref);
 EXCEPTION WHEN others THEN v_failed:=true; END;
 IF NOT v_failed THEN RAISE EXCEPTION 'Non-owner updated someone else''s case'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_case_events
       WHERE case_id=case_ref AND action='ASSIGNED')<>1
 THEN RAISE EXCEPTION 'Competing operations changed history'; END IF;
 -- Existing billing lifecycle must remain intact on a different case.
 SELECT case_id INTO case_ref FROM public.alpha_support_cases WHERE category='billing';
 IF public.alpha_support_transition(case_ref,'TRIAGED',owner_ref,'',owner_ref) IS DISTINCT FROM true
 THEN RAISE EXCEPTION 'Human triage did not commit'; END IF;
 PERFORM public.alpha_support_transition(case_ref,'IN_REVIEW',owner_ref,'',owner_ref);
 PERFORM public.alpha_support_transition(case_ref,'RESOLVED',owner_ref,'Fictional resolution accepted',owner_ref);
 PERFORM public.alpha_support_transition(case_ref,'CLOSED',owner_ref,'Fictional resolution accepted',owner_ref);
 IF NOT EXISTS(SELECT 1 FROM public.alpha_support_cases WHERE case_id=case_ref
   AND status='CLOSED' AND resolved_at IS NOT NULL AND owner_user_id=owner_ref)
 THEN RAISE EXCEPTION 'Closure not durable'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_case_events WHERE case_id=case_ref)<>5
 THEN RAISE EXCEPTION 'Transition events not durable'; END IF;
 v_failed:=false;
 BEGIN PERFORM public.alpha_support_transition(case_ref,'RESOLVED',NULL,'',owner_ref);
 EXCEPTION WHEN others THEN v_failed:=true; END;
 IF NOT v_failed THEN RAISE EXCEPTION 'Improper ownerless resolution accepted'; END IF;
 v_failed:=false;
 BEGIN PERFORM public.alpha_support_transition(case_ref,'IN_REVIEW',owner_ref,'',gen_random_uuid());
 EXCEPTION WHEN others THEN v_failed:=true; END;
 IF NOT v_failed THEN RAISE EXCEPTION 'Unknown operator accepted'; END IF;
 v_failed:=false;
 BEGIN UPDATE public.alpha_support_case_events SET summary='tampered' WHERE case_id=case_ref;
 EXCEPTION WHEN others THEN v_failed:=true; END;
 IF NOT v_failed THEN RAISE EXCEPTION 'History immutability failed'; END IF;
 -- The existing 90-day intake cleanup must be able to purge a case and
 -- dependent history together, without exposing direct service-role DELETE.
 DELETE FROM public.alpha_public_intake_requests
 WHERE id=(SELECT request_id FROM public.alpha_support_cases WHERE category='general');
 IF (SELECT count(*) FROM public.alpha_support_cases)<>7
 OR (SELECT count(*) FROM public.alpha_support_case_events WHERE action='CREATED')<>7
 THEN RAISE EXCEPTION 'Retention cascade not viable'; END IF;
 RAISE NOTICE 'TASK21 ISOLATED PG17 PASS: 8 cases, 9 receipts, 4 CRM outbox rows, protected escalation, durable transitions and immutable history';
END;$$;


-- PRODUCTION-PARITY NEGATIVE/POSITIVE ROLE TEST. Unlike the main fixture
-- assertions above, this runs as the actual restricted `service_role`
-- (BYPASSRLS=yes; SELECT on auth.users=no) rather than superuser.
-- A missing private definer guard previously made BOTH RPCs fail in hosted
-- Supabase while the CI superuser tests passed.
INSERT INTO auth.users(id) VALUES
  ('00000000-0000-4000-8000-000000004321'::uuid);
DO $privilege_preflight$
BEGIN
 IF has_table_privilege('service_role','auth.users','SELECT') THEN
   RAISE EXCEPTION 'Fixture does not reproduce Supabase auth.users denial'; END IF;
 IF has_schema_privilege('anon','alpha_support_private','USAGE')
 OR has_schema_privilege('authenticated','alpha_support_private','USAGE')
 OR has_function_privilege('authenticated',
   'alpha_support_private.auth_user_exists(uuid)','EXECUTE')
 THEN RAISE EXCEPTION 'Private Auth lookup accidentally exposed to client roles'; END IF;
 IF NOT has_function_privilege('service_role',
   'alpha_support_private.auth_user_exists(uuid)','EXECUTE')
 THEN RAISE EXCEPTION 'Service role cannot invoke locked-down identity helper'; END IF;
END;
$privilege_preflight$;
SET ROLE service_role;
DO $real_service_role$
DECLARE c uuid;
BEGIN
 IF NOT alpha_support_private.auth_user_exists(
   '00000000-0000-4000-8000-000000004321'::uuid)
 THEN RAISE EXCEPTION 'Real service_role identity lookup failed'; END IF;
 IF alpha_support_private.auth_user_exists(
    '00000000-0000-4000-8000-000000004322'::uuid)
 THEN RAISE EXCEPTION 'Unknown operator identity accepted'; END IF;
 SELECT case_id INTO c FROM public.alpha_support_cases WHERE category='creator';
 IF c IS NULL THEN RAISE EXCEPTION 'Creator case fixture missing'; END IF;
 IF public.alpha_support_claim(c,
   '00000000-0000-4000-8000-000000004321'::uuid) IS DISTINCT FROM true
 THEN RAISE EXCEPTION 'Service-role self-claim failed'; END IF;
 IF public.alpha_support_claim(c,
   '00000000-0000-4000-8000-000000004321'::uuid) IS DISTINCT FROM true
 THEN RAISE EXCEPTION 'Service-role idempotent claim failed'; END IF;
 IF (SELECT COUNT(*) FROM public.alpha_support_case_events
   WHERE case_id=c AND action='ASSIGNED')<>1
 THEN RAISE EXCEPTION 'Service-role audit event must be exactly once'; END IF;
 IF public.alpha_support_transition(c,'IN_REVIEW',
     '00000000-0000-4000-8000-000000004321'::uuid,'',
     '00000000-0000-4000-8000-000000004321'::uuid) IS DISTINCT FROM true
 THEN RAISE EXCEPTION 'Service-role owned transition failed'; END IF;
 IF NOT EXISTS (SELECT 1 FROM public.alpha_support_cases
   WHERE case_id=c AND status='IN_REVIEW' AND
   owner_user_id='00000000-0000-4000-8000-000000004321'::uuid)
 THEN RAISE EXCEPTION 'Service-role transition did not persist'; END IF;
END;
$real_service_role$;
RESET ROLE;
SELECT 'TASK21_SERVICE_ROLE_AUTH_PRIVILEGE_PASS' AS acceptance_status;
