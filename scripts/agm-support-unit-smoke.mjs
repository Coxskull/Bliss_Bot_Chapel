import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { supportNotice,supportCategories,supportRoute,supportCrmEligible } from
  "../supabase/functions/alpha-public-intake/support-contract.mjs";
import { mapGhlContact, ghlNotice } from
  "../supabase/functions/alpha-public-intake/ghl-payload.mjs";

const expected = {
  general:["NORMAL","GENERAL_SUPPORT",false],
  advertiser:["NORMAL","ADVERTISER_SUPPORT",false],
  creator:["NORMAL","CREATOR_SUPPORT",false],
  billing:["HIGH","BILLING_REVIEW",true],
  refund:["HIGH","BILLING_REVIEW",true],
  failed_campaign:["HIGH","BILLING_REVIEW",true],
  privacy:["HIGH","PRIVACY_REVIEW",true],
  security_incident:["CRITICAL","SECURITY_REVIEW",true]
};
assert.equal(supportCategories.length,8);
for (const [category,[severity,role,human]] of Object.entries(expected)) {
  assert.deepEqual(supportRoute(category),{severity,assigned_role:role,human_required:human});
  assert.equal(supportCrmEligible(category),["general","advertiser","creator"].includes(category));
}
assert.throws(()=>supportRoute("other"));
const record = {
 kind:"support",category:"general",notice_version:supportNotice,
 full_name:"Synthetic Review",email:"qa@unit.test",organization:"",website:"",
 processing_acknowledged:true,adult_confirmed:true,description:"No GHL notes"
};
const crm = mapGhlContact(record,"aBcD1234EFgh5678IJkl");
assert.equal(crm.source,"ALPHA GLOBAL MEDIA · General Support Inquiry");
assert.equal(crm.email,record.email);
for (const forbidden of ["description","category","tags","customFields","dnd",
  "organization","market","marketing_opt_in"]) assert.ok(!(forbidden in crm));
for (const category of ["billing","refund","failed_campaign","privacy","security_incident"])
 assert.throws(()=>mapGhlContact({...record,category},"aBcD1234EFgh5678IJkl"));
assert.throws(()=>mapGhlContact({...record,notice_version:"AGM-INTEREST-2026-11-05"},
  "aBcD1234EFgh5678IJkl"));

const path = p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");
const sql = path("supabase/migrations/20261108_alpha_support_case_workflow.sql");
const edge = path("supabase/functions/alpha-public-intake/index.ts");
const send = path("supabase/functions/alpha-public-intake/ghl-sync.mjs");
const html = path("frontend/public/support.html");
const browser = path("frontend/public/alpha-intake.js");
const retry = path("supabase/functions/alpha-ghl-retry/index.ts");
for (const needle of [
 "CREATE TABLE public.alpha_support_cases",
 "CREATE TABLE public.alpha_support_case_events",
 "CREATE TRIGGER alpha_support_case_after_intake",
 "CREATE TRIGGER alpha_support_events_immutable",
 "CREATE FUNCTION public.alpha_support_transition",
 "REVOKE ALL ON public.alpha_support_cases FROM PUBLIC,anon,authenticated",
 "REVOKE ALL ON public.alpha_support_case_events FROM PUBLIC,anon,authenticated",
 "REVOKE EXECUTE ON FUNCTION public.alpha_support_transition",
 "NEW.category IN ('general','advertiser','creator')",
 "FROM auth.users",
 "FOR UPDATE"
]) assert.ok(sql.includes(needle),"Missing SQL control: "+needle);
for (const category of Object.keys(expected))
 assert.ok(sql.includes("'"+category+"'"),"Migration missing route "+category);
for (const forbidden of ["UPDATE public.alpha_support_case_events SET",
 "GRANT SELECT ON public.alpha_support_cases TO anon"])
 assert.ok(!sql.includes(forbidden));
assert.ok(edge.includes("caseRecord = saved.data"));
assert.ok(edge.includes("supportCrmEligible"));
assert.ok(edge.includes("payload.notice_version !== supportNotice"));
assert.ok(edge.includes("case_id: caseRecord.case_id"));
assert.ok(!edge.toLowerCase().includes("x-test-mode"));
assert.ok(send.includes('ALPHA_GHL_SUPPORT_SYNC_ENABLED'));
assert.ok(send.includes('return "SUPPORT_DISABLED"'));
assert.ok(send.includes("GHL_DELIVERY_STATE_NOT_COMMITTED"));
assert.ok(retry.includes("../alpha-public-intake/ghl-sync.mjs"));
assert.ok(html.includes('data-kind="support"'));
assert.ok(html.includes("SUPPORT · HUMAN REVIEW QUEUE"));
assert.ok(html.includes("not guarantee a response time"));
assert.ok(browser.includes('kind !== "support" ||'));
assert.ok(browser.includes("result.case_id"));
for(const secret of ["SUPABASE_SERVICE_ROLE_KEY","ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN",
 "ALPHA_GHL_SUPPORT_SYNC_ENABLED"]) assert.ok(!html.includes(secret));
// Main-branch interest consent and Task21 source must stay in lockstep.
assert.equal(ghlNotice,"AGM-INTEREST-2026-11-08-GHL-01");
assert.ok(browser.includes(ghlNotice),"Interest form must use the CRM notice");
assert.ok(edge.includes('const notice = "'+ghlNotice+'"'),"Edge must accept current interest notice");
assert.ok(browser.includes(supportNotice),"Support must retain separate notice");
const privacyNotice = path("frontend/public/alpha-privacy.html");
assert.ok(privacyNotice.includes(ghlNotice),"Privacy notice must match interest form");
assert.ok(privacyNotice.includes(supportNotice),"Privacy notice must disclose support");

console.log("Task21 isolated source contract PASS: 8 categories, 5 sensitive CRM denials, private schema and durable-case receipt guards.");
