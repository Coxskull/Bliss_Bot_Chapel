-- Task #20 disposable PostgreSQL retention acceptance; never run on live Supabase.
-- Existing financial_test fixture already loaded the AI governor migration.
\set ON_ERROR_STOP on
BEGIN;
INSERT INTO public.alpha_ask_alpha_turns (
  request_key,session_hash,ip_hash,input_hash,audience,notice_version,
  state,actual_usd,input_tokens,output_tokens,provider_request_id,output_hash,created_at,finished_at
) VALUES
 ('00000000-0000-4000-8000-000000000200',repeat('a',64),repeat('b',64),repeat('c',64),
  'creator','AGM-ASK-ALPHA-AI-2026-11-08','SETTLED',0.000030,100,30,'resp_old_synthetic',
  repeat('e',64),now()-interval '91 days',now()-interval '91 days'),
 ('00000000-0000-4000-8000-000000000201',repeat('a',64),repeat('b',64),repeat('c',64),
  'advertiser','AGM-ASK-ALPHA-AI-2026-11-08','SETTLED',0.000030,100,30,'resp_recent_synthetic',
  repeat('e',64),now()-interval '89 days',now()-interval '89 days'),
 ('00000000-0000-4000-8000-000000000202',repeat('a',64),repeat('b',64),repeat('c',64),
  'support','AGM-ASK-ALPHA-AI-2026-11-08','UNKNOWN',NULL,NULL,NULL,NULL,
  NULL,now()-interval '91 days',now()-interval '91 days'),
 ('00000000-0000-4000-8000-000000000203',repeat('a',64),repeat('b',64),repeat('c',64),
  'general','AGM-ASK-ALPHA-AI-2026-11-08','RESERVED',NULL,NULL,NULL,NULL,
  NULL,now()-interval '91 days',NULL);
DO $checks$
DECLARE
 deleted_count integer;
BEGIN
 WITH purged AS (
   DELETE FROM public.alpha_ask_alpha_turns
   WHERE state = 'SETTLED'
     AND created_at < now() - interval '90 days'
   RETURNING request_key
 )
 SELECT count(*) INTO deleted_count FROM purged;
 IF deleted_count <> 1 THEN
   RAISE EXCEPTION 'Retention deleted %, expected one old settled receipt',deleted_count;
 END IF;
 IF EXISTS (SELECT 1 FROM public.alpha_ask_alpha_turns
            WHERE request_key='00000000-0000-4000-8000-000000000200') THEN
   RAISE EXCEPTION 'Expired settled receipt survived';
 END IF;
 IF NOT EXISTS (SELECT 1 FROM public.alpha_ask_alpha_turns
            WHERE request_key='00000000-0000-4000-8000-000000000201' AND state='SETTLED')
    OR NOT EXISTS (SELECT 1 FROM public.alpha_ask_alpha_turns
            WHERE request_key='00000000-0000-4000-8000-000000000202' AND state='UNKNOWN')
    OR NOT EXISTS (SELECT 1 FROM public.alpha_ask_alpha_turns
            WHERE request_key='00000000-0000-4000-8000-000000000203' AND state='RESERVED') THEN
   RAISE EXCEPTION 'Recent receipt or unreconciled accounting record was deleted';
 END IF;
 RAISE NOTICE 'TASK20_RETENTION_90D_KEEP_UNSETTLED_PASS';
END
$checks$;
ROLLBACK;
