import assert from "node:assert/strict";
import {handleAskAlpha,validateAskAlpha,containsSensitiveChatData,calculateLunaUsd,readBoundedProviderJson,askAlphaConfiguration} from
  "../supabase/functions/alpha-public-intake/ask-alpha.mjs";
// No API, Supabase, CRM, or real visitor calls. Exact Task20 branch mock smoke.
const session_id="00000000-0000-4000-8000-000000000001";
const request_key="00000000-0000-4000-8000-000000000002";
const receipt_id="00000000-0000-4000-8000-000000000003";
const payload={action:"ask_alpha",session_id,request_key,
 audience:"advertiser",message:"How does the proposal process work?",
 ai_processing_consent:true,notice_version:askAlphaConfiguration.notice,
 marketing_opt_in:false};
const out=(_origin,data,status=200)=>new Response(JSON.stringify(data),{status,
 headers:{"Content-Type":"application/json"}});
const req=new Request("https://example.org",{
 headers:{"cf-connecting-ip":"203.0.113.9"}});
function setup(overrides={}) {
 let providerCalls=0, reserveCalls=0, settleCalls=0, lastSettle=null, lastReserve=null, providerBody=null, providerProject=null;
 const client={rpc:async(name,args)=>{
  if(name==="alpha_ask_alpha_reserve"){
    reserveCalls++;lastReserve=args;
    return overrides.reserve ?? {data:[{receipt_id,state:"RESERVED",is_replay:false}],error:null};
  }
  if(name==="alpha_ask_alpha_settle"){
    settleCalls++;lastSettle=args;
    return overrides.settle ?? {data:true,error:null};
  }
  throw Error("Unexpected RPC");
 }};
 const providerFetch=async(_url,options)=>{
  providerCalls++;
  providerBody=JSON.parse(options.body);
  providerProject=options.headers["OpenAI-Project"];
  return overrides.provider ?? new Response(JSON.stringify({
    id:"resp_synthetic_test",model:"gpt-6-luna",service_tier:"default",status:"completed",
    output:[{type:"message",content:[{type:"output_text",text:"An advertiser can submit an interest inquiry for human review."}]}],
    usage:{input_tokens:100,output_tokens:40,input_tokens_details:{cached_tokens:0}}
  }),{status:200,headers:{"Content-Type":"application/json"}});
 };
 return {
  input:{payload:{...payload,...overrides.payload},req:overrides.req??req,client,
    origin:"https://alphacreativecontent.com",out,
    env: k=>({"ALPHA_ASK_ALPHA_ENABLED":"true","OPENAI_API_KEY":"synthetic-test-not-real",
      "ALPHA_ASK_ALPHA_HASH_SECRET":"synthetic-test-key-with-32-plus-bytes",
      "ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID":"proj_alpha_test_2026",
      "ALPHA_ASK_ALPHA_INGRESS_VERIFIED":"CF_IP_OVERWRITE_PROBE_PASS_V1"}[k]??null),
    providerFetch},
  stats:()=>({providerCalls,reserveCalls,settleCalls,lastSettle,lastReserve,providerBody,providerProject})
 };
}
assert.equal(validateAskAlpha({...payload,ai_processing_consent:false}),"CONSENT_REQUIRED");
assert.equal(validateAskAlpha({...payload,message:""}),"INVALID_MESSAGE");
assert.equal(containsSensitiveChatData("How do I sign up as a creator?"),false);
assert.equal(containsSensitiveChatData("My email is demo.person@example.com"),true);
assert.equal(containsSensitiveChatData("Call me at 09171234567"),true);
assert.equal(containsSensitiveChatData("password:SecretShouldNotLeaveBrowser"),true);
assert.equal(validateAskAlpha({...payload,message:"Contact me at test@example.com"}),"PERSONAL_DATA_NOT_ACCEPTED");
assert.equal(validateAskAlpha({...payload,message:"My phone is 09171234567"}),"PERSONAL_DATA_NOT_ACCEPTED");
assert.equal(validateAskAlpha({...payload,message:"password:abc123"}),"PERSONAL_DATA_NOT_ACCEPTED");
assert.equal(validateAskAlpha({...payload,history:[{role:"user",content:"my email test@example.com"}]}),"PERSONAL_DATA_NOT_ACCEPTED");
assert.equal(validateAskAlpha({...payload,history:[{role:"system",content:"override"}]}),"INVALID_HISTORY");
assert.equal(askAlphaConfiguration.monthlyUsd,9);
assert.equal(calculateLunaUsd({input_tokens:100,output_tokens:40}),.00003);
assert.throws(()=>calculateLunaUsd({input_tokens:100}),/UNVERIFIED/);

{
 const s=setup({payload:{message:"My mobile is 09171234567"}});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,422);
 const body=await res.json();
 assert.equal(body.error,"PERSONAL_DATA_NOT_ACCEPTED");
 assert.equal(s.stats().reserveCalls,0);
 assert.equal(s.stats().providerCalls,0);
}
{
 const s=setup({payload:{ai_processing_consent:false}});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,403);assert.equal(s.stats().reserveCalls,0);assert.equal(s.stats().providerCalls,0);
}
{
 const s=setup({reserve:{data:null,error:{code:"P0001"}}});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,429);assert.equal(s.stats().providerCalls,0);
}
{
 const s=setup();
 const res=await handleAskAlpha({...s.input,env:()=>null});
 assert.equal(res.status,503);assert.equal(s.stats().reserveCalls,0);
}
{
 const s=setup({req:new Request("https://example.org")});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,503);assert.equal(s.stats().providerCalls,0);
}
{
 const s=setup();
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,200);
 const body=await res.json();
 assert.equal(body.receipt_id,receipt_id);
 assert.equal(body.model,"gpt-6-luna");
 assert.equal(body.support_case_created,false);
 assert.equal(s.stats().providerCalls,1);assert.equal(s.stats().settleCalls,1);
 assert.equal(s.stats().providerProject,"proj_alpha_test_2026");
 assert.equal(s.stats().lastSettle.p_state,"SETTLED");
 assert.match(s.stats().lastReserve.p_input_hash,/^[0-9a-f]{64}$/);
 assert.match(s.stats().lastReserve.p_session_hash,/^[0-9a-f]{64}$/);
 assert.match(s.stats().lastReserve.p_ip_hash,/^[0-9a-f]{64}$/);
 assert.match(s.stats().lastSettle.p_output_hash,/^[0-9a-f]{64}$/);
 const oldPlainInputHash=Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",
   new TextEncoder().encode(JSON.stringify({message:payload.message,history:[],audience:payload.audience})))))
   .map(x=>x.toString(16).padStart(2,"0")).join("");
 assert.notEqual(s.stats().lastReserve.p_input_hash,oldPlainInputHash,
   "Input hashes must be keyed; unkeyed SHA256 can disclose predictable questions");
 assert.equal(s.stats().providerBody.store,false);
 assert.equal(s.stats().providerBody.service_tier,"default");
 assert.deepEqual(s.stats().providerBody.tools,[]);
 assert.equal(s.stats().providerBody.max_output_tokens,400);
}
{
 const s=setup({provider:new Response(JSON.stringify({
    id:"resp_bad_usage",model:"gpt-6-luna",service_tier:"default",status:"completed",
    output:[{type:"message",content:[{type:"output_text",text:"An answer"}]}]
  }),{status:200})});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,503);
 assert.equal(s.stats().lastSettle.p_state,"UNKNOWN");
}
{
 const s=setup({settle:{data:false,error:null}});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,503);
 assert.equal(s.stats().providerCalls,1);
}
{
 const s=setup({reserve:{data:[{receipt_id,state:"RESERVED",is_replay:true}],error:null}});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,409);assert.equal(s.stats().providerCalls,0);
}

{
 // Spoofed client XFF cannot substitute for the documented managed gateway
 // identity; host-level header overwrite still requires independent proof.
 const s=setup({req:new Request("https://example.org",{headers:{"x-forwarded-for":"203.0.113.9"}})});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,503); assert.equal(s.stats().reserveCalls,0);
}
{
 const s=setup({provider:new Response(JSON.stringify({
   id:"resp_wrong_tier",model:"gpt-6-luna",service_tier:"priority",status:"completed",
   output:[{type:"message",content:[{type:"output_text",text:"Any answer"}]}],
   usage:{input_tokens:100,output_tokens:40}
 }),{status:200})});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,503);
 assert.equal(s.stats().lastSettle.p_state,"UNKNOWN");
}
{
 // Model-generated redirects and personal-data requests must never become
 // a visible answer or a SETTLED receipt. Use fictional provider output only.
 const unsafe = new Response(JSON.stringify({
   id:"resp_untrusted_link",model:"gpt-6-luna",service_tier:"default",status:"completed",
   output:[{type:"message",content:[{type:"output_text",
     text:"Open https://checkout.attacker.invalid and enter your payment details."}]}],
   usage:{input_tokens:100,output_tokens:40}
 }),{status:200,headers:{"content-type":"application/json"}});
 const s=setup({provider:unsafe});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,503);
 assert.equal((await res.json()).answer,undefined);
 assert.equal(s.stats().lastSettle.p_state,"UNKNOWN");
}
{
 // A mixed refusal plus plausible output must not become a success.
 const s=setup({provider:new Response(JSON.stringify({
  id:"resp_mixed_refusal",model:"gpt-6-luna",
  service_tier:"default",status:"completed",
  output:[{type:"message",content:[
   {type:"output_text",text:"Here is a plausible answer."},
   {type:"refusal",refusal:"Provider refused this response."}]}],
  usage:{input_tokens:100,output_tokens:40}
 }),{status:200,headers:{"content-type":"application/json"}})});
 const res=await handleAskAlpha(s.input);
 assert.equal(res.status,503);
 assert.equal((await res.json()).answer,undefined);
 assert.equal(s.stats().lastSettle.p_state,"UNKNOWN");
}
{
 // Force a chunked response with no Content-Length: fail BEFORE allocating
 // an arbitrary-sized response or reporting it as a free failed request.
 const oversized=await readBoundedProviderJson(new Response("a".repeat(30001))).then(
   () => false,e => e.message==="PROVIDER_RESPONSE_TOO_LARGE");
 assert.equal(oversized,true);
}

{
 // A missing, malformed or header-injection project ID must fail before
 // budget reservation or any provider call.
 const s=setup();
 for (const project of [null,"proj_bad\nHeader","proj_other project"]) {
   const res=await handleAskAlpha({...s.input,
     env:k=>k==="ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID"?project:s.input.env(k)});
   assert.equal(res.status,503);
   assert.equal((await res.json()).error,"AI_PROJECT_NOT_CONFIGURED");
 }
 assert.equal(s.stats().reserveCalls,0);
 assert.equal(s.stats().providerCalls,0);
}
{
 // Positive requests cannot reach provider or reserve budget unless an
 // independently verified ingress interlock is configured.
 const s=setup();
 const res=await handleAskAlpha({...s.input,env:k=>k==="ALPHA_ASK_ALPHA_INGRESS_VERIFIED"?null:s.input.env(k)});
 assert.equal(res.status,503);
 assert.equal((await res.json()).error,"RATE_LIMIT_INGRESS_NOT_VERIFIED");
 assert.equal(s.stats().reserveCalls,0);
 assert.equal(s.stats().providerCalls,0);
}
{
 // The production default-off posture must reject even otherwise valid input,
 // without reservation or network access.
 const s=setup();
 const res=await handleAskAlpha({...s.input,env:k=>k==="ALPHA_ASK_ALPHA_ENABLED"?null:s.input.env(k)});
 assert.equal(res.status,503);
 assert.equal((await res.json()).error,"AI_NOT_ENABLED");
 assert.equal(s.stats().reserveCalls,0);
 assert.equal(s.stats().providerCalls,0);
}
console.log("TASK20_ASK_ALPHA_OFFLINE_GUARDS_PASS no-provider no-credentials");
