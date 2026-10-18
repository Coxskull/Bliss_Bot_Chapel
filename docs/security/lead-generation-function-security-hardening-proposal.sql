-- REVIEW ONLY. Target: Alpha Lead Generation Supabase yyesqcyjhqjryaskrqip.
-- This is NOT an applied migration. Owner/security/integration approval required.
-- Preserve active ensure_rls event trigger, existing function bodies, and service_role.
-- PostgreSQL trigger/event-trigger firing does not require client EXECUTE grants.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

-- public.rls_auto_enable() returns event_trigger and currently acts as
-- the enabled ensure_rls ddl_command_end event trigger.
-- Eliminate unnecessary direct public invocation grants only.
REVOKE EXECUTE ON FUNCTION public.rls_auto_enable()
  FROM PUBLIC, anon, authenticated;

-- public.set_updated_at() is a normal invoker trigger function assigning
-- NEW.updated_at = now(); pg_catalog contains the only referenced built-in.
ALTER FUNCTION public.set_updated_at()
  SET search_path = pg_catalog;

COMMIT;

-- Acceptance is separate: run read-only lead-generation-function-security-verification.sql,
-- then Supabase Security Advisor; inspect approved integrations before any deployment.
