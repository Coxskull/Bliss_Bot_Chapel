-- TASK21: idempotent, staff-only staging rollback of this ONE fictional
-- browser mutation grant. No other operator access, support cases or outreach
-- state changes. Global STOP stays engaged.
BEGIN;
SET LOCAL statement_timeout='20s';
SET LOCAL lock_timeout='3s';
DO $task21_guard$
BEGIN
 IF to_regclass('alpha_support_private.staging_case_mutation_trials') IS NULL
 OR (SELECT count(*) FROM alpha_support_private.staging_case_mutation_trials)<>1
 OR NOT EXISTS (SELECT 1 FROM alpha_support_private.staging_case_mutation_trials
    WHERE case_id='619fe5d5-fd4f-4fee-9e2d-c8baee50cc79'::uuid)
 THEN RAISE EXCEPTION 'Task21 exact case trial not isolated; no update'; END IF;
END $task21_guard$;
UPDATE alpha_support_private.staging_case_mutation_trials
SET enabled=false
WHERE case_id='619fe5d5-fd4f-4fee-9e2d-c8baee50cc79'::uuid
AND enabled=true;
COMMIT;
SELECT (SELECT count(*)::int FROM alpha_support_private.staging_case_mutation_trials
 WHERE enabled) AS still_enabled,
 'TASK21_SINGLE_FICTIONAL_TRIAL_DISABLED' AS receipt;
