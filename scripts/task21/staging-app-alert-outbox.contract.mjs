import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

// Task21 single-operator app-origin incident queue acceptance, offline only.
// No network, Gmail send, paid provider or customer database write.
const read=name=>readFileSync(new URL("./"+name,import.meta.url),"utf8");
const schema=read("staging-app-origin-alert-outbox-20261112.sql");
const fixture=read("staging-single-app-outbox-fixture-20261112.sql");
const restriction=read("staging-app-alert-synthetic-claim-only-20261112.sql");
for(const required of [
  "TASK21_ISOLATED_STAGING_GATE_FAILED",
  "alpha_support_private.staging_app_alert_outbox",
  "alpha_support_private.staging_app_alert_events",
  "task21_staging_application_alert_queued",
  "AFTER INSERT ON public.alpha_support_alert_intents",
  "on_staging_application_alert()",
  "ENABLE ROW LEVEL SECURITY",
  "FOR UPDATE OF q SKIP LOCKED",
  "CLAIMED",
  "PROVIDER_ACCEPTED",
  "UNCERTAIN",
  "claim_token=gen_random_uuid()",
  "independent human ACK",
  "REVOKE EXECUTE ON FUNCTION",
  "service_role"
]) assert.ok(schema.toLowerCase().includes(required.toLowerCase()),"Missing stage-only outbox invariant "+required);
assert.match(schema,/state='QUEUED'[\s\S]*claim_token IS NULL/);
assert.match(schema,/state='PROVIDER_ACCEPTED'[\s\S]*provider_message_id IS NOT NULL/);
assert.match(schema,/state='UNCERTAIN'[\s\S]*claim_token IS NOT NULL/);
assert.ok(schema.includes("FOR UPDATE OF q SKIP LOCKED"));
assert.ok(!/https?:\/\//i.test(schema),"Private SQL schema cannot emit outbound requests");
for(const needed of [
  "TASK21_STAGING_SYNTHETIC_CLAIM_GUARD_FAILED",
  "r.kind='support'",
  "r.notice_version='AGM-SUPPORT-2026-11-08'",
  "lower(r.email) LIKE '%@example.invalid'",
  "r.full_name LIKE 'TASK21 %'",
  "r.marketing_opt_in IS FALSE",
  "staging_alert_dispatch_receipts",
  "claim_next_staging_app_alert()"
]) assert.ok(restriction.includes(needed),"Staging synthetic claim restriction missing: "+needed);
for(const needed of [
  "TASK21_ONE_SYNTHETIC_STAGING_FIXTURE_GUARD_FAILED",
  "example.invalid",
  "'support'",
  "'privacy'",
  "'AGM-SUPPORT-2026-11-08'",
  "marketing_opt_in,processing_acknowledged,adult_confirmed",
  "crm_entries",
  "durable_app_events"
]) assert.ok(fixture.includes(needed),"Hosted fixture source contract missing "+needed);
for(const secretMarker of ["OPENAI_API_KEY","SUPABASE_SERVICE_ROLE_KEY","GMAIL_ACCESS_TOKEN","password="])
 assert.ok(![schema,fixture,restriction].some(s=>s.includes(secretMarker)),"Unexpected secret in authored source");
console.log("TASK21_STAGING_APP_ORIGIN_OUTBOX_SOURCE_PASS: default-deny, synthetic-only claims, independent provider/ACK stages");
