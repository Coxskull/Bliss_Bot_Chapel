// Zero-egress Task21 contract: consented public privacy submissions become
// private metadata-only support cases, without changing advertiser/creator flows.
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const ddl=readFileSync(new URL("./task21/production-privacy-form-private-queue-20261112.sql",import.meta.url),"utf8");
const privacy=readFileSync(new URL("../frontend/public/privacy-request.html",import.meta.url),"utf8");
const retention=readFileSync(new URL("./task21/production-mailcase-retention-cascade-fix-20261112.sql",import.meta.url),"utf8");
for(const necessary of [
 "TASK21_90DAY_MAIL_RETENTION_GUARD_FAILED",
 "TASK21_EXISTING_AUDIT_TRIGGER_CHANGED",
 "CREATE OR REPLACE TRIGGER manual_mail_events_no_changes",
 "BEFORE UPDATE ON alpha_support_private.manual_mail_case_events",
 "REVOKE DELETE,UPDATE ON alpha_support_private.manual_mail_case_events",
 "ON DELETE CASCADE",
 "90-day case purge"
])assert.ok(retention.includes(necessary),"Missing safe existing support mail retention repair: "+necessary);
assert.doesNotMatch(retention,/DELETE FROM alpha_support_private\.manual_mail_cases/i);
assert.doesNotMatch(retention,/DROP\s+TRIGGER/i);

const edge=readFileSync(new URL("../supabase/functions/alpha-public-intake/index.ts",import.meta.url),"utf8");
for(const key of [
 "TASK21_PRIVACY_FORM_PRIVATE_QUEUE_GATE_FAILED",
 "REFERENCES public.alpha_public_intake_requests(id) ON DELETE CASCADE",
 "NEW.kind='privacy'",
 "NEW.processing_acknowledged IS TRUE",
 "NEW.adult_confirmed IS TRUE",
 "ON CONFLICT(source_request_id) DO NOTHING",
 "CREATE TRIGGER task21_real_privacy_form_private_queue",
 "REVOKE ALL ON SCHEMA alpha_support_private",
 "FROM PUBLIC,anon,authenticated,service_role",
 "manual_privacy_form_cases",
 "manual_privacy_form_events",
 "manual_privacy_operator_claims",
 "manual_privacy_operator_actions",
 "CREATE OR REPLACE FUNCTION public.alpha_support_manual_operator_queue",
 "CREATE OR REPLACE FUNCTION public.alpha_support_manual_operator_claim",
 "alpha_support_private.manual_mail_cases",
 "public.alpha_support_manual_operator_authorized(p_actor_user_id)",
 "GRANT EXECUTE ON FUNCTION public.alpha_support_manual_operator_queue(uuid,integer) TO service_role",
 "GRANT EXECUTE ON FUNCTION public.alpha_support_manual_operator_claim(uuid,uuid) TO service_role",
 "90-day public intake retention"
]) assert.ok(ddl.includes(key),"Missing safe privacy form routing requirement: "+key);
assert.match(ddl,/manual_privacy_form_cases\([\s\S]*?source_request_id uuid NOT NULL UNIQUE/);
assert.match(ddl,/manual_privacy_form_cases[\s\S]*?ALTER TABLE alpha_support_private\.manual_privacy_form_cases ENABLE ROW LEVEL SECURITY/);
assert.match(ddl,/CREATE FUNCTION alpha_support_private\.enqueue_real_privacy_form\(\)[\s\S]*?SECURITY DEFINER/);
assert.match(ddl,/WHEN 'privacy' THEN 1/);
assert.match(ddl,/CREATE OR REPLACE FUNCTION public\.alpha_support_manual_operator_claim\([\s\S]*?manual_mail_cases[\s\S]*?manual_privacy_form_cases/);
assert.ok(privacy.includes('data-kind="privacy"')&&privacy.includes('name="processing_acknowledged"'));
assert.ok(edge.includes('"privacy"')&&edge.includes('payload.kind'));
for(const forbidden of [
 /\bNEW\.email\b/,/\bNEW\.full_name\b/,/\bNEW\.description\b/,
 /\bNEW\.marketing_opt_in\b/,/\bNEW\.organization\b/,
 /\bTRUNCATE\b/i,/\bDROP\s+(?:TABLE|CONSTRAINT|SCHEMA)\b/i,
 /\bALTER\s+TABLE\s+public\.alpha_public_intake_requests\b/i,
 /\bGRANT\s+(?:SELECT|INSERT|UPDATE|DELETE)\s+ON\s+alpha_support_private\./i,
 /\bDELETE\s+FROM\s+public\.alpha_public_intake_requests\b/i,
 /\bhttps?:\/\//i
])assert.doesNotMatch(ddl,forbidden,"Prohibited privacy/form/CRM mutation or personal data duplication");
console.log("TASK21_REAL_PRIVACY_FORM_TO_PRIVATE_QUEUE_SOURCE_PASS no_customer_PII_duplication no_provider_egress no_existing_flow_changes");
