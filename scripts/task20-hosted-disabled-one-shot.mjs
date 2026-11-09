// Task20 one-shot diagnostic against DISABLED hosted AI. No OpenAI call.
// The operator confirmed SQL governor OFF and $9 cap immediately beforehand.
// The expiry below prevents reuse once public inference is enabled.
import { randomUUID } from "node:crypto";
import assert from "node:assert/strict";
if(Date.now()>Date.parse("2026-11-11T00:00:00+08:00"))
  throw Error("TASK20_DISABLED_PROBE_EXPIRED");
if(process.env.TASK20_OPERATOR_VERIFIED_DISABLED!=="YES")
  throw Error("TASK20_DISABLED_DB_PREFLIGHT_REQUIRED");
const url="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake";
const origin="https://alphacreativecontent.com";
const request={
  action:"ask_alpha",audience:"general",
  notice_version:"AGM-ASK-ALPHA-AI-2026-11-08",
  ai_processing_consent:true,marketing_opt_in:false,
  session_id:randomUUID(),request_key:randomUUID(),
  message:"Can ALPHA explain its general advertiser inquiry process?",history:[]
};
const response=await fetch(url,{
  method:"POST",headers:{"Content-Type":"application/json",Origin:origin},
  body:JSON.stringify(request),redirect:"error",signal:AbortSignal.timeout(12000)
});
const raw=await response.text();
assert.ok(raw.length<=4096,"TASK20_RESPONSE_TOO_LARGE");
let body;
try{body=JSON.parse(raw)}catch{throw Error("TASK20_RESPONSE_NOT_JSON");}
const errorCode=typeof body?.error==="string"?body.error:"NONE";
// Never emit provider response text or user content in CI logs.
console.log("TASK20_HOSTED_GATE",JSON.stringify({http:response.status,error:errorCode}));
assert.equal(response.status,503,"TASK20_NOT_DISABLED_STATUS");
assert.equal(errorCode,"AI_NOT_ENABLED","TASK20_NOT_FAIL_CLOSED");
assert.equal(body.answer,undefined,"TASK20_UNEXPECTED_PROVIDER_ANSWER");
assert.equal(response.headers.get("access-control-allow-origin"),origin);
console.log("TASK20_HOSTED_DISABLED_NO_PROVIDER_GATE_PASS");
