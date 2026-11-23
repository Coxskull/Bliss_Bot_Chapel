// Task21 source contract for an existing staging-only internal alert ledger.
// Offline only: never sends email, writes DB, creates cases or contacts customers.
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const sql=readFileSync(new URL("./staging-alert-gmail-agent-receipt-ledger-20261112.sql",import.meta.url),"utf8");
for(const token of [
  "ISOLATED STAGING ONLY",
  "TASK21_STAGING_DISPATCH_GUARD_FAILED",
  "alpha_support_private.staging_alert_dispatch_receipts",
  "REFERENCES public.alpha_support_alert_intents(alert_id)",
  "source='CONNECTED_GMAIL_MANUAL_AGENT'",
  "PREPARED",
  "PROVIDER_ACCEPTED",
  "HUMAN_ACKNOWLEDGED",
  "provider_message_id text UNIQUE",
  "human_ack_message_id text UNIQUE",
  "provider_accepted_at",
  "human_ack_at",
  "ENABLE ROW LEVEL SECURITY",
  "REVOKE ALL ON TABLE",
  "FROM PUBLIC, anon, authenticated, service_role"
])assert.ok(sql.includes(token),"Missing critical staging-only receipt/permission requirement: "+token);
for(const prohibited of [
  /grant\s+all\s+on/i,
  /grant\s+(insert|update|delete)\s+on\s+.*\s+to\s+(?:anon|authenticated|service_role)/i,
  /https?:\/\//i,
  /fetch\s*\(/i,
  /gmail\.com/i,
  /\bupdate\s+public\.alpha_support_alert_intents\b/i,
  /\bsecret[_-]?key\b/i
])assert.doesNotMatch(sql,prohibited,"Staging migration must not grant callers control or send to providers");
assert.match(sql,/state='HUMAN_ACKNOWLEDGED'[\s\S]*human_ack_message_id IS NOT NULL[\s\S]*human_ack_at IS NOT NULL/);
console.log("TASK21_STAGING_INTERNAL_ALERT_RECEIPT_LEDGER_SOURCE_PASS offline no_egress no_customer_writes");
