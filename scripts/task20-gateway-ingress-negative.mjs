// Task20 controlled gateway header provenance check. Three deliberately
// DENIED requests. No private credentials, valid inference or customer data.
import assert from "node:assert/strict";
import {randomUUID} from "node:crypto";

if(Date.now()>Date.parse("2026-11-12T00:00:00+08:00"))
  throw Error("TASK20_INGRESS_PROBE_EXPIRED");

const endpoint="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake";
const origin="https://alphacreativecontent.com";
const body={
 action:"ask_alpha",
 notice_version:"AGM-ASK-ALPHA-AI-2026-11-08",
 ai_processing_consent:false,
 audience:"general",
 session_id:randomUUID(),
 request_key:randomUUID(),
 message:"Synthetic consent-denied request. Do not create a response."
};
let tested=0;
async function probe(label,agent,extra={}){
 const r=await fetch(endpoint,{
  method:"POST",
  headers:{"Origin":origin,"Content-Type":"application/json","User-Agent":agent,...extra},
  body:JSON.stringify(body),redirect:"error",signal:AbortSignal.timeout(12000)
 });
 assert.equal(r.status,403,label+": must deny before paid inference");
 const isJson=(r.headers.get("content-type")||"").includes("application/json");
 if(label==="CLEAN") assert.ok(isJson,"clean request must reach consent gate");
 if(isJson){
  const raw=await r.text();
  assert.ok(raw.length<5000,label+": bounded JSON response");
  const json=JSON.parse(raw);
  assert.equal(json.error,"CONSENT_REQUIRED",label+": must deny with consent gate");
  assert.equal(json.answer,undefined,label+": no AI answer");
  assert.equal(json.receipt_id,undefined,label+": no AI receipt");
 } else {
  // A real WAF denial can have a large HTML body. Do not download it:
  // only HTTP 403 matters, and logs are correlated separately.
  await r.body?.cancel();
 }
 // A WAF can return HTML 403 before invoking the function. That is still
 // a safe denial, but log correlation is required to certify provenance.
 console.log("TASK20_INGRESS_PROBE "+label+" HTTP_403 "+(isJson?"FUNCTION_CONSENT_DENIED":"GATEWAY_DENIED"));
 tested++;
}
await probe("CLEAN","ALPHA-TASK20-INGRESS-CLEAN-20261110");
await probe("CF_FORGED","ALPHA-TASK20-INGRESS-CF-SPOOF-20261110",{"cf-connecting-ip":"203.0.113.250"});
await probe("XFF_FORGED","ALPHA-TASK20-INGRESS-XFF-SPOOF-20261110",{"x-forwarded-for":"198.51.100.250"});
assert.equal(tested,3);
console.log("TASK20_INGRESS_DENIAL_TRIPLE_PASS no_inference no_secrets");
