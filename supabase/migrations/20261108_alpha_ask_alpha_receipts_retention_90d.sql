-- ALPHA GLOBAL MEDIA LLC — Ask Alpha Task #20, approved controlled-staging retention.
-- Schedule cleanup ONLY on the dedicated Ask Alpha receipt table.
-- This never alters the factory STOP, intake forms, HighLevel, campaigns or payments.
-- Financial safety: only SETTLED receipts older than 90 days can be purged.
-- UNKNOWN and RESERVED states are preserved for manual cost reconciliation;
-- they must NOT disappear automatically and mask potential provider charges.
-- The job is unique. Never silently overwrite an existing schedule.
DO $retention$
DECLARE
  job_id bigint;
BEGIN
  IF to_regclass('public.alpha_ask_alpha_turns') IS NULL THEN
    RAISE EXCEPTION 'Ask Alpha receipt table is absent';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname='pg_cron') THEN
    RAISE EXCEPTION 'Existing pg_cron extension is required';
  END IF;
  IF EXISTS (SELECT 1 FROM cron.job
             WHERE jobname='alpha_ask_alpha_receipts_retention_90d') THEN
    RAISE EXCEPTION 'Ask Alpha retention job already exists: inspect before modification';
  END IF;
  SELECT cron.schedule(
    'alpha_ask_alpha_receipts_retention_90d',
    '45 3 * * *',
    $jobsql$DELETE FROM public.alpha_ask_alpha_turns
      WHERE state = 'SETTLED'
        AND created_at < now() - interval '90 days';$jobsql$
  ) INTO job_id;
  IF job_id IS NULL THEN
    RAISE EXCEPTION 'Ask Alpha retention scheduling failed';
  END IF;
END;
$retention$;
