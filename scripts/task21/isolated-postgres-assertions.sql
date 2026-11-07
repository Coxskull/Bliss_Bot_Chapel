-- Executed ONLY against an empty disposable PG17 CI database.
INSERT INTO public.alpha_public_intake_requests
(request_key,kind,full_name,email,category,description,notice_version)
SELECT gen_random_uuid(),'support','Fictional '+category,
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
DECLARE case_ref uuid; owner_ref uuid:=gen_random_uuid(); v_failed boolean;
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
 IF has_table_privilege('anon','public.alpha_support_cases','SELECT')
 OR has_table_privilege('authenticated','public.alpha_support_case_events','SELECT')
 OR has_function_privilege('anon',
   'public.alpha_support_transition(uuid,text,uuid,text,uuid)','EXECUTE')
 THEN RAISE EXCEPTION 'Public support table or RPC exposure'; END IF;
 SELECT case_id INTO case_ref FROM public.alpha_support_cases WHERE category='billing';
 INSERT INTO auth.users(id) VALUES(owner_ref);
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
 v_failed:=false;
 BEGIN DELETE FROM public.alpha_support_case_events WHERE case_id=case_ref;
 EXCEPTION WHEN others THEN v_failed:=true; END;
 IF NOT v_failed THEN RAISE EXCEPTION 'History delete guard failed'; END IF;
 RAISE NOTICE 'TASK21 ISOLATED PG17 PASS: 8 cases, 9 receipts, 4 CRM outbox rows, protected escalation, durable transitions and immutable history';
END;$$;
