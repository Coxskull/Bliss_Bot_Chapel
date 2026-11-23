import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {handleCrmStaffReadback} from "../supabase/functions/alpha-ghl-operator-review/handler.mjs";
const id="82f2a62c-1c29-4824-ba38-e10d12268b7f";
const jwt="e30.e30.e30"; // Fictional shape only; not a real credential.
const origin="https://alphacreativecontent.com";
const req=(opts={})=>new Request("https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-ghl-operator-review",{
 method:opts.method||"POST",
 headers:{
  ...(opts.origin===null?{}:{Origin:opts.origin||origin}),
  ...(opts.jwt===null?{}:{Authorization:"Bearer "+(opts.jwt||jwt)}),
  "Content-Type":"application/json"
 },body:opts.method==="OPTIONS"||opts.method==="GET"?undefined:(opts.body??"{}")
});
let providerCalls=0;
const mock=(o={})=>({
 auth:{
  getUser:async()=>o.reject?{error:"invalid"}:{data:{user:{id}}},
  getClaims:async()=>o.reject?{error:"invalid"}:{data:{claims:{
   sub:o.sub||id,role:"authenticated",aal:o.aal||"aal2",
   ...(o.noSession?{}:{session_id:"3c41599d-d4bb-466c-9d91-95290716c65c"})
  }}}
 },
 rpc:async(name)=>name==="alpha_support_manual_operator_authorized"?
  {data:o.allowed===true}:{error:"invalid"},
 from:(table)=>{
  assert.equal(table,"alpha_ghl_review_audit");
  return {select:()=>({order:()=>({limit:async()=>({data:
   o.recent?[{checked_at:"2026-11-12T08:00:00Z"}]:[],error:null})})})};
 }
});
const remote=async()=>{
 providerCalls++;
 return {ok:true,data:{processed:3,results:[
  {suppressionStatus:"SUPPRESSED",classification:"HOLD_NO_CURRENT_INQUIRY_CONTACT_CONSENT",
   deliveredContactMatch:true,emailIdentityMatch:true,
   contact_email:"private-person@example.com",remote_contact_id:"SECRET-PII"},
  {suppressionStatus:"CLEAR",classification:"HOLD_NO_CURRENT_INQUIRY_CONTACT_CONSENT"},
  {suppressionStatus:"UNKNOWN",classification:"HOLD_CRM_SUPPRESSION_UNKNOWN"}
 ]}};
};
const run=async(options={},client=mock({allowed:true}),upstream=remote)=>{
 const x=await handleCrmStaffReadback(req(options),{client,fetchReconcile:upstream,
   now:()=>Date.parse("2026-11-12T08:02:00Z")});
 const body=x.status===204?null:await x.json();
 return {status:x.status,body,headers:x.headers};
};
assert.equal((await run({origin:"https://evil.example"})).status,403);
assert.equal((await run({jwt:null})).status,401);
assert.equal((await run({},mock({allowed:true,reject:true}))).status,401);
assert.equal((await run({},mock({allowed:true,aal:"aal1"}))).status,403);
assert.equal((await run({},mock({allowed:true,noSession:true}))).status,403);
assert.equal((await run({},mock({allowed:false}))).status,403);
assert.equal((await run({},mock({allowed:true,recent:true}))).status,429);
assert.equal((await run({},mock({allowed:true}),async()=>({ok:false}))).status,503);
const before=providerCalls;
const good=await run();
assert.equal(good.status,200);
assert.equal(good.body.processed,3);
assert.deepEqual(good.body.suppression,{SUPPRESSED:1,CLEAR:1,UNKNOWN:1});
assert.equal(good.body.classifications.HOLD_NO_CURRENT_INQUIRY_CONTACT_CONSENT,2);
assert.equal(good.body.followup_permission,"HOLD — NO AUTOMATED OUTREACH APPROVED");
assert.equal(providerCalls,before+1);
assert.ok(!JSON.stringify(good.body).includes("private-person@example.com"));
assert.ok(!JSON.stringify(good.body).includes("SECRET-PII"));
assert.equal(good.headers.get("Cache-Control"),"no-store");
assert.equal(good.headers.get("Access-Control-Allow-Origin"),origin);
assert.equal((await run({method:"OPTIONS",jwt:null})).status,204);
assert.equal((await run({method:"GET"})).status,405);
assert.equal((await run({body:"{\"request_ids\":[\"1\"]}"})).status,422);
const html=readFileSync(new URL("../frontend/public/support-operator.html",import.meta.url),"utf8");
const js=readFileSync(new URL("../frontend/public/support-operator.js",import.meta.url),"utf8");
const server=readFileSync(new URL("../supabase/functions/alpha-ghl-operator-review/index.ts",import.meta.url),"utf8");
assert.ok(html.includes('id="crm-readback"'));
assert.ok(js.includes('CRM_REVIEW_API=PROJECT+"/functions/v1/alpha-ghl-operator-review"'));
assert.ok(js.includes('"Authorization":"Bearer "+token'));
assert.ok(js.includes("Follow-up remains on HOLD"));
assert.ok(server.includes('Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")'));
assert.ok(!js.includes("SUPABASE_SERVICE_ROLE_KEY"));
assert.ok(!js.includes("sb_secret_"));
assert.ok(!js.includes("localStorage.setItem"));
assert.ok(!js.includes("sessionStorage.setItem"));
assert.ok(!js.includes("innerHTML"));
assert.ok(server.includes('"/functions/v1/alpha-ghl-readback"'));
assert.ok(server.includes('method:"POST"'));
console.log("TASK22_STAFF_GHL_READBACK_CONTRACT_PASS 14 denial/aggregate checks; no PII no remote write no followup permission");
