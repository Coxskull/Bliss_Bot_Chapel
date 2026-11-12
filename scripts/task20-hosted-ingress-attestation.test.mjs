import assert from "node:assert/strict";
import {handleAskAlpha} from "../supabase/functions/alpha-public-intake/ask-alpha.mjs";

// Source-level release-interlock acceptance, never calls Supabase or OpenAI.
// The hosted spoof outcome is established separately by an actual no-consent
// GitHub Actions request plus Supabase function_edge_logs readback.
const payload={
  action:"ask_alpha",notice_version:"AGM-ASK-ALPHA-AI-2026-11-08",
  ai_processing_consent:true,session_id:"a0000000-0000-4000-8000-000000000001",
  request_key:"a0000000-0000-4000-8000-000000000002",
  audience:"general",message:"How do creators join?",
};
const config={
  ALPHA_ASK_ALPHA_ENABLED:"true",
  OPENAI_API_KEY:"synthetic-credential-never-sent",
  ALPHA_ASK_ALPHA_HASH_SECRET:"a".repeat(64),
  ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID:"proj_example",
  ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID:"org_example"
};
async function probe(value){
  const env=k=>k==="ALPHA_ASK_ALPHA_INGRESS_VERIFIED"?value:config[k];
  const client={rpc:()=>{throw Error("No reserve or provider call allowed in offline ingress probe");}};
  const result=await handleAskAlpha({
    payload,req:{headers:new Headers()},client,origin:"https://alphacreativecontent.com",
    out:(_origin,body,status)=>({body,status}),env,
    providerFetch:()=>{throw Error("No paid provider call permitted");}
  });
  assert.equal(result.status,503);
  return result.body.error;
}
for(const invalid of [undefined,"","true","CF_IP_SPOOF_DENIED_AT_EDGE_V0"]){
  assert.equal(await probe(invalid),"RATE_LIMIT_INGRESS_NOT_VERIFIED");
}
for(const approved of ["CF_IP_OVERWRITE_PROBE_PASS_V1","CF_IP_SPOOF_DENIED_AT_EDGE_V1"]){
  assert.equal(await probe(approved),"RATE_LIMIT_IDENTITY_UNAVAILABLE");
}
console.log("TASK20_EXPLICIT_INGRESS_ATTESTATION_FAILCLOSED_PASS no_egress no_paid_calls");
