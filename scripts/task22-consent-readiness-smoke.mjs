// ALPHA Task22: offline, synthetic, no provider/network and no real customer PII.
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {handleConsentReadiness} from "../supabase/functions/alpha-ghl-consent-readiness/handler.mjs";

const uuid="82f2a62c-1c29-4824-ba38-e10d12268b7f";
const token="e30.e30.e30"; // intentionally fake JWT shape
const production="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-ghl-consent-readiness";
const origin="https://alphacreativecontent.com";
const fixture={
 inquiries:5,contact_consent_recorded:0,missing_current_contact_consent:5,
 marketing_opted_in:5,marketing_opted_out:0,pending_operator_review:0,
 locally_cleared:0,hold_count:5,reviewed_identity_and_authority:0,revocation_events:0
};
const makeReq=(o={})=>new Request(production+(o.query||""),{
 method:o.method||"GET",headers:{
  ...(o.origin===null?{}:{Origin:o.origin||origin}),
  ...(o.auth===false?{}:{Authorization:"Bearer "+(o.token||token)})
 }
});
const client=(o={})=>({
 auth:{
  getUser:async()=>o.reject?{error:"reject"}:{data:{user:{id:uuid}}},
  getClaims:async()=>o.reject?{error:"reject"}:{data:{claims:{
   sub:o.subject||uuid,role:"authenticated",
   aal:o.aal||"aal2",
   ...(o.noSession?{}:{session_id:"3c41599d-d4bb-466c-9d91-95290716c65c"})
  }}}
 },
 rpc:async(name)=>name==="alpha_support_manual_operator_authorized"
  ?{data:o.grant!==false}:name==="alpha_ghl_consent_readiness_summary"
   ?{data:o.badTotals||fixture}:{error:"unknown"}
});
const call=async(o={},c=client())=>{
 const r=await handleConsentReadiness(makeReq(o),{client:c});
 const body=r.status===204?null:await r.json();
 return {status:r.status,body,headers:r.headers};
};
assert.equal((await call({origin:"https://fake.example"})).status,403);
assert.equal((await call({auth:false})).status,401);
assert.equal((await call({method:"POST"})).status,405);
assert.equal((await call({method:"OPTIONS",auth:false})).status,204);
assert.equal((await call({query:"?request_id=synthetic"})).status,422);
assert.equal((await call({},client({reject:true}))).status,401);
assert.equal((await call({},client({aal:"aal1"}))).status,403);
assert.equal((await call({},client({subject:"e4cf72e3-9692-4fee-8aeb-7fcc527c12e0"}))).status,403);
assert.equal((await call({},client({noSession:true}))).status,403);
assert.equal((await call({},client({grant:false}))).status,403);
assert.equal((await call({},client({badTotals:{...fixture,hold_count:4}}))).status,503);
assert.equal((await call({},client({badTotals:{...fixture,revocation_events:-1}}))).status,503);
const good=await call();
assert.equal(good.status,200);
assert.equal(good.body.status,"READ_ONLY_LOCAL_READINESS");
assert.equal(good.body.inquiries,5);
assert.equal(good.body.hold_count,5);
assert.equal(good.body.contact_consent_recorded,0);
assert.equal(good.body.followup_permission,"HOLD — NEVER RELEASE FROM THIS REPORT");
assert.equal(good.headers.get("Cache-Control"),"no-store");
assert.equal(good.headers.get("Access-Control-Allow-Origin"),origin);
for(const forbidden of ["request_id","email","full_name","contact_id","token","phone","market","organization"]){
 assert.ok(!Object.hasOwn(good.body,forbidden),"Disallowed data returned: "+forbidden);
}
const sql=readFileSync(new URL("./task22/production-consent-readiness-optout-guard-20261112.sql",import.meta.url),"utf8");
for(const clause of [
 "alpha_task22_cleared_requires_current_explicit_optin",
 "AND marketing_opt_in IS TRUE",
 "VALIDATE CONSTRAINT",
 "task22_consent_revocation_force_hold",
 "NEW.followup_gate='HOLD'",
 "NEW.followup_approved_at=NULL",
 "NEW.crm_suppression_checked=false",
 "task22_consent_revocation_audit",
 "CONTACT_CONSENT_REVOKED",
 "MARKETING_OPTED_OUT",
 "crm_consent_revocations ENABLE ROW LEVEL SECURITY",
 "ON DELETE CASCADE",
 "REVOKE ALL ON FUNCTION public.alpha_ghl_consent_readiness_summary()",
 "GRANT EXECUTE ON FUNCTION public.alpha_ghl_consent_readiness_summary()",
 "TO service_role"
]) assert.ok(sql.includes(clause),"Consent safety clause missing: "+clause);
for(const forbidden of [
 /\\bINSERT INTO public\\.alpha_public_intake_requests\\b/i,
 /\\bUPDATE public\\.alpha_public_intake_requests SET\\b/i,
 /\\bDELETE FROM public\\.alpha_public_intake_requests\\b/i,
 /\\bUPDATE public\\.alpha_ghl_delivery\\b/i,
 /\\bGRANT\\s+EXECUTE\\s+ON FUNCTION public\\.alpha_ghl_consent_readiness_summary\\(\\) TO anon/i,
 /\\bemail\\s+text\\b/i
]) assert.doesNotMatch(sql,forbidden,"Unsafe migration mutation or PII");
const html=readFileSync(new URL("../frontend/public/support-operator.html",import.meta.url),"utf8");
const js=readFileSync(new URL("../frontend/public/support-operator.js",import.meta.url),"utf8");
const index=readFileSync(new URL("../supabase/functions/alpha-ghl-consent-readiness/index.ts",import.meta.url),"utf8");
assert.ok(html.includes('id="crm-consent-readiness"'));
assert.ok(html.includes('id="crm-consent-result"'));
assert.ok(js.includes('CRM_CONSENT_API=PROJECT+"/functions/v1/alpha-ghl-consent-readiness"'));
assert.ok(js.includes('Authorization:"Bearer "+token'));
assert.ok(js.includes("No outreach has been authorized."));
assert.ok(index.includes('Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")'));
assert.ok(!js.includes("SUPABASE_SERVICE_ROLE_KEY"));
assert.ok(!js.includes("localStorage.setItem"));
assert.ok(!js.includes("sessionStorage.setItem"));
assert.ok(!js.includes("innerHTML"));
console.log("TASK22_CONSENT_REVOCATION_AND_READINESS_OFFLINE_PASS 19+ validations; synthetic only, no contact writes");
