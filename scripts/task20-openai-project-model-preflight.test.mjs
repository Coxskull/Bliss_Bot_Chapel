import test from "node:test";
import assert from "node:assert/strict";
import {probeOpenAiProject} from "./task20-openai-project-model-preflight.mjs";
const vars=()=>({
 TASK20_NONBILLABLE_ACCESS_CHECK:"YES",
 OPENAI_API_KEY:"sk-synthetic-never-real-0123456789",
 ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID:"proj_synthetic_alpha_2026",
 ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID:"org_synthetic_alpha_2026"
});
const model=()=>new Response(JSON.stringify({object:"model",id:"gpt-6-luna",owned_by:"openai"}),{
 status:200,headers:{"content-type":"application/json","openai-organization":"org_synthetic_alpha_2026"}});
test("no opt in or missing or malformed values refuse before network",async()=>{
 const cases=[
 {...vars(),TASK20_NONBILLABLE_ACCESS_CHECK:""},
 {...vars(),ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID:""},
 {...vars(),ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID:"proj_bad\nInjected"},
 {...vars(),ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID:""},
 {...vars(),ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID:"org_bad space"},
 {...vars(),OPENAI_API_KEY:""},
 {...vars(),OPENAI_API_KEY:"key\nsecret"}
 ];let calls=0;
 for(const env of cases){const r=await probeOpenAiProject({env,fetcher:async()=>{calls++;throw Error("forbidden");}});
  assert.equal(r.ok,false);assert.ok(r.code.endsWith("INVALID")||r.code==="OPT_IN_REQUIRED");}
 assert.equal(calls,0);
});
test("authorized GET uses pinned project and organization and returns no secrets",async()=>{
 let count=0;
 const r=await probeOpenAiProject({env:vars(),fetcher:async(url,opts)=>{
  count++;assert.equal(url,"https://api.openai.com/v1/models/gpt-6-luna");
  assert.equal(opts.method,"GET");assert.equal(opts.redirect,"error");
  assert.equal(opts.headers["OpenAI-Project"],vars().ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID);
  assert.equal(opts.headers["OpenAI-Organization"],vars().ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID);
  assert.equal(opts.headers.Authorization,"Bearer "+vars().OPENAI_API_KEY);
  assert.ok(opts.signal instanceof AbortSignal);return model();
 }});
 assert.equal(count,1);assert.equal(r.ok,true);assert.equal(r.paid_inference_verified,false);
 assert.equal(r.hard_spending_cap_verified,false);
 assert.doesNotMatch(JSON.stringify(r),/sk-|proj_|org_/);
});
test("401 and 403 stay denied and provider error text is never exposed",async()=>{
 for(const status of [401,403]){
  const r=await probeOpenAiProject({env:vars(),fetcher:async()=>new Response("secret provider details",{status})});
  assert.deepEqual(r,{ok:false,code:"CREDENTIAL_OR_PROJECT_ACCESS_DENIED"});
 }
});
test("incorrect metadata or org proof cannot be accepted",async()=>{
 const cases=[
 new Response(JSON.stringify({object:"model",id:"wrong"}),{status:200,headers:{
  "content-type":"application/json","openai-organization":"org_synthetic_alpha_2026"}}),
 new Response(JSON.stringify({object:"model",id:"gpt-6-luna"}),{status:200,headers:{"content-type":"application/json"}}),
 new Response("not json",{status:200,headers:{"content-type":"text/plain","openai-organization":"org_synthetic_alpha_2026"}})
 ];
 for(const response of cases){const r=await probeOpenAiProject({env:vars(),fetcher:async()=>response});assert.equal(r.ok,false);}
});
test("overlong or malformed responses and network exceptions fail closed",async()=>{
 const cases=[
 new Response("x".repeat(4097),{status:200,headers:{"content-type":"application/json","openai-organization":"org_synthetic_alpha_2026"}}),
 new Response("{}",{status:200,headers:{"content-type":"application/json","content-length":"4097","openai-organization":"org_synthetic_alpha_2026"}})
 ];
 for(const response of cases){const r=await probeOpenAiProject({env:vars(),fetcher:async()=>response});assert.deepEqual(r,{ok:false,code:"MODEL_RESPONSE_TOO_LARGE"});}
 const e=await probeOpenAiProject({env:vars(),fetcher:async()=>{throw Error("sk-private-should-not-leak");}});
 assert.deepEqual(e,{ok:false,code:"NETWORK_OR_RESPONSE_UNVERIFIED"});
});
