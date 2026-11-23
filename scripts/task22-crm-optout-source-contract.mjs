// ALPHA Task22 / Task21: CRM opt-out fail-closed contract — offline, zero provider calls.
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const rd=path=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const sql=rd("scripts/task22/production-crm-optout-local-transport-hold-20261112.sql");
const send=rd("supabase/functions/alpha-public-intake/ghl-sync.mjs");
const payload=rd("supabase/functions/alpha-public-intake/ghl-payload.mjs");
const readback=rd("supabase/functions/alpha-ghl-readback/logic.mjs");
const endpoint=rd("supabase/functions/alpha-ghl-readback/index.ts");
for(const token of [
 "TASK22_CRM_OPT_OUT_SOURCE_SCOPE_CHANGED",
 "NEW.marketing_opt_in IS TRUE",
 "r.marketing_opt_in IS TRUE",
 "NEW.contact_consent = true",
 "r.contact_consent=true",
 "AND r.crm_suppression_checked=true",
 "REVOKE EXECUTE ON FUNCTION public.alpha_ghl_claim(uuid)",
 "REVOKE EXECUTE ON FUNCTION public.alpha_ghl_delivery_on_intake()"
])assert.ok(sql.includes(token),"Missing private SQL delivery/opt-out gate: "+token);
for(const token of [
 "r.marketing_opt_in !== true",
 "contact_consent,marketing_opt_in,contact_consent_version"
])assert.ok(send.includes(token),"Missing live transport opt-out guard: "+token);
assert.ok(payload.includes("record.marketing_opt_in !== true"));
assert.ok(readback.includes("HOLD_MARKETING_OPTOUT"));
assert.ok(readback.includes("request?.marketing_opt_in === true"));
assert.ok(endpoint.includes("contact_consent,marketing_opt_in,contact_consent_version"));
for(const source of [sql,send,payload,readback,endpoint]){
 assert.ok(!source.includes("dnd:true")&&!source.includes('"dnd":true'),"Do not falsely set CRM provider DND");
}
assert.doesNotMatch(sql,/\bUPDATE\s+public\.alpha_public_intake_requests\b/i);
assert.doesNotMatch(sql,/\bDELETE\s+FROM\s+public\.alpha_ghl_delivery\b/i);
assert.doesNotMatch(sql,/\bGRANT\s+EXECUTE\s+ON\s+FUNCTION\b/i);
console.log("TASK22_LOCAL_CRM_MARKETING_OPTOUT_PASS — no contact upsert after optout, remote DND still requires provider evidence");
