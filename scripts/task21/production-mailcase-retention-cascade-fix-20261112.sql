-- ALPHA Task21 retention correction, existing production support-email registry.
-- Current BEFORE DELETE OR UPDATE audit trigger conflicts with ON DELETE CASCADE
-- on the child event table, so the already-installed 90-day case purge would
-- fail once a real case receives an immutable audit event.
-- Keep UPDATE prohibited; ordinary roles still have no DELETE privileges.
DO $task21_retention_guard$
DECLARE v_trigger text;
BEGIN
 IF to_regclass('alpha_support_private.manual_mail_case_events') IS NULL
 OR to_regclass('alpha_support_private.manual_mail_cases') IS NULL
 OR NOT EXISTS(SELECT 1 FROM pg_constraint
   WHERE conrelid='alpha_support_private.manual_mail_case_events'::regclass
     AND pg_get_constraintdef(oid) LIKE '%ON DELETE CASCADE%')
 OR NOT EXISTS(SELECT 1 FROM cron.job
   WHERE jobname='alpha_task21_support_mail_90d_purge' AND active=true)
 THEN RAISE EXCEPTION 'TASK21_90DAY_MAIL_RETENTION_GUARD_FAILED'; END IF;
 SELECT pg_get_triggerdef(t.oid) INTO v_trigger
 FROM pg_trigger t
 WHERE t.tgrelid='alpha_support_private.manual_mail_case_events'::regclass
   AND t.tgname='manual_mail_events_no_changes' AND NOT t.tgisinternal;
 IF v_trigger IS NULL OR v_trigger NOT LIKE '%BEFORE DELETE OR UPDATE%'
 THEN RAISE EXCEPTION 'TASK21_EXISTING_AUDIT_TRIGGER_CHANGED'; END IF;
END $task21_retention_guard$;

CREATE OR REPLACE TRIGGER manual_mail_events_no_changes
 BEFORE UPDATE ON alpha_support_private.manual_mail_case_events
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.manual_mail_events_immutable();

-- No direct event deletion for app/browser roles. A trusted postgres-owned
-- retention job deletes the old parent; the FK cascade deletes child events.
REVOKE DELETE,UPDATE ON alpha_support_private.manual_mail_case_events
 FROM PUBLIC,anon,authenticated,service_role;
COMMENT ON TABLE alpha_support_private.manual_mail_case_events IS
 'Task21 support email status audit: ordinary clients cannot update/delete. Trusted 90-day parent retention must cascade child deletion for data minimization.';
