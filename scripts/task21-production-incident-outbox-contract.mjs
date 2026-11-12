// Task #21 production incident alert contract, offline, no customer actions.
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const sql=readFileSync(new URL("./task21/production-real-incident-alert-outbox-20261112.sql",import.meta.url),"utf8");
for(const expected of [
 "TASK21_PROD_INCIDENT_OUTBOX_PREREQUISITE_FAILED",
 "alpha_support_private.production_incident_alerts",
 "alpha_support_private.production_incident_events",
 "ENABLE ROW LEVEL SECURITY",
 "AFTER INSERT ON alpha_support_private.manual_mail_cases",
 "AFTER INSERT ON alpha_support_private.manual_privacy_form_cases",
 "AFTER DELETE ON alpha_support_private.manual_mail_cases",
 "AFTER DELETE ON alpha_support_private.manual_privacy_form_cases",
 "FOR UPDATE SKIP LOCKED",
 "AND q.state='QUEUED'",
 "state='CLAIMED'",
 "state='PROVIDER_ACCEPTED'",
 "state='HUMAN_ACKNOWLEDGED'",
 "state='UNCERTAIN'",
 "AND state='CLAIMED'",
 "AND state='PROVIDER_ACCEPTED'",
 "p_ack_message_id=p_provider_message_id",
 "human_ack_message_id<>provider_message_id",
 "ON DELETE CASCADE",
 "REVOKE ALL ON SCHEMA alpha_support_private",
 "FROM PUBLIC,anon,authenticated,service_role",
 "claim_next_production_incident()",
 "record_production_incident_provider(uuid,uuid,text,timestamptz)",
 "record_production_incident_human_ack(uuid,text,text,timestamptz)"
])assert.ok(sql.includes(expected),"Missing production alert safeguard: "+expected);
for(const forbidden of [
 /\bNEW\.email\b/,/\bNEW\.full_name\b/,/\bNEW\.description\b/,
 /\bNEW\.marketing_opt_in\b/,
 /\bGRANT\s+(?:EXECUTE|SELECT|INSERT|UPDATE|DELETE)\s+ON/i,
 /\bTRUNCATE\b/i,/\bDROP\s+TABLE\b/i,
 /\bCREATE\s+EXTENSION\b/i,/\bpg_net\b/i,
 /\bfetch\s*\(/i,/https?:\/\//i,
 /\bUPDATE\s+public\.alpha_public_intake_requests\b/i,
 /\bINSERT\s+INTO\s+public\.alpha_ghl_delivery\b/i,
 /\bDELETE\s+FROM\s+public\.alpha_public_intake_requests\b/i
])assert.doesNotMatch(sql,forbidden,"Dangerous production alert migration change");
assert.match(sql,/CREATE FUNCTION alpha_support_private\.claim_next_production_incident\([\s\S]*?SECURITY INVOKER/);
assert.match(sql,/CREATE FUNCTION alpha_support_private\.record_production_incident_human_ack\([\s\S]*?SECURITY INVOKER/);
assert.match(sql,/OLD\.state='CLAIMED'[\s\S]*?NEW\.state IN \('PROVIDER_ACCEPTED','UNCERTAIN'\)/);
assert.match(sql,/OLD\.state='PROVIDER_ACCEPTED'[\s\S]*?NEW\.state='HUMAN_ACKNOWLEDGED'/);
assert.match(sql,/NEW\.category IN \('security','privacy','billing','refund'\)/);
console.log("TASK21_PRODUCTION_INCIDENT_OUTBOX_DEFAULT_DENY_PASS source_only no_customer_data no_auto_retry");
