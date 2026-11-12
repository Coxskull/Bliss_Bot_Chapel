// Task21 production manual support mailbox — no-PII, private-table contract.
// Offline release acceptance; it does not send email, write customer data,
// create a public case ID, or change CRM, paid AI or factory STOP.
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const sql=readFileSync(new URL('./production-manual-mailbox-metadata-20261112.sql',import.meta.url),'utf8');
for(const wanted of [
  'TASK21_MANUAL_MAIL_PRODUCTION_PREREQUISITE_FAILED',
  "to_regclass('public.alpha_ask_alpha_settings')",
  'CREATE SCHEMA IF NOT EXISTS alpha_support_private',
  'REVOKE ALL ON SCHEMA alpha_support_private FROM PUBLIC, anon, authenticated, service_role',
  'CREATE TABLE alpha_support_private.manual_mail_cases',
  'CREATE TABLE alpha_support_private.manual_mail_case_events',
  'provider_message_id text NOT NULL UNIQUE',
  'CREATE INDEX manual_mail_cases_open',
  'ALTER TABLE alpha_support_private.manual_mail_cases ENABLE ROW LEVEL SECURITY',
  'ALTER TABLE alpha_support_private.manual_mail_case_events ENABLE ROW LEVEL SECURITY',
  'REVOKE ALL ON ALL TABLES IN SCHEMA alpha_support_private',
  'REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA alpha_support_private',
  "('OPEN','IN_REVIEW','RESOLVED','CLOSED')",
  'manual_mail_capture_audit',
  'manual_mail_events_immutable',
  'No Data API grant'
])assert.ok(sql.includes(wanted),'Missing private production gate: '+wanted);
for(const forbidden of [
  /\bCREATE\s+PUBLICATION\b/i,
  /\bGRANT\s+[^;]*\bTO\s+(?:anon|authenticated|service_role)\b/i,
  /ALTER\s+TABLE\s+public\.alpha_public_intake/i,
  /\bUPDATE\s+public\.alpha_ask_alpha_settings/i,
  /https?:\/\//,
  /email_address\s+text/i,
  /email_subject\s+text/i,
  /email_body\s+text/i,
  /SEND\s+EMAIL/i
])assert.doesNotMatch(sql,forbidden,'Unexpected public surface/secret/PII in Task21 schema');
assert.match(sql,/provider_message_id\s+text\s+NOT\s+NULL\s+UNIQUE/);
assert.match(sql,/AFTER\s+INSERT[\s\S]*manual_mail_capture_audit/);
assert.match(sql,/BEFORE\s+UPDATE\s+OR\s+DELETE[\s\S]*manual_mail_events_immutable/);
console.log('TASK21_PRODUCTION_PRIVATE_MAILBOX_SCHEMA_ACCEPTED offline no_PII no_public_grants no_egress');
