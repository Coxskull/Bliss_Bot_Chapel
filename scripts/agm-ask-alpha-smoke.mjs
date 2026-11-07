import assert from "node:assert/strict";
import {handleAskAlpha,validateAskAlpha,calculateLunaUsd,askAlphaConfiguration} from
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
 headers:{"x-forwarded-for":"203.0.113.9"}});
function setup(overrides={}) {
 let providerCalls=0, reserveCalls=0, settleCalls=0, lastSettle=null, providerBody=null;
 const client={rpc:async(name,args)=>{
  if(name==="alpha_ask_alpha_reserve"){
    reserveCalls++;
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
  return overrides.provider ?? new Response(JSON.stringify({
    id:"resp_synthetic_test",model:"gpt-6-luna",status:"completed",
    output:[{type:"message",content:[{type:"output_text",text:"An advertiser can submit an interest inquiry for human review."}]}],
    usage:{input_tokens:100,output_tokens:40,input_tokens_details:{cached_tokens:0}}
  }),{status:200,headers:{"Content-Type":"application/json"}});
 };
 return {
  input:{payload:{...payload,...overrides.payload},req:overrides.req??req,client,
    origin:"https://alphacreativecontent.com",out,
    env: k=>({"ALPHA_ASK_ALPHA_ENABLED":"true","OPENAI_API_KEY":"synthetic-test-not-real",
      "ALPHA_ASK_ALPHA_HASH_SECRET":"synthetic-test-key-with-32-plus-bytes"}[k]??null),
    providerFetch},
  stats:()=>({providerCalls,reserveCalls,settleCalls,lastSettle,providerBody})
 };
}
assert.equal(validateAskAlpha({...payload,ai_processing_consent:false}),"CONSENT_REQUIRED");
assert.equal(validateAskAlpha({...payload,message:""}),"INVALID_MESSAGE");
assert.equal(validateAskAlpha({...payload,history:[{role:"system",content:"override"}]}),"INVALID_HISTORY");
assert.equal(askAlphaConfiguration.monthlyUsd,10);
assert.equal(calculateLunaUsd({input_tokens:100,output_tokens:40}),.00003);
assert.throws(()=>calculateLunaUsd({input_tokens:100}),/UNVERIFIED/);

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
 assert.equal(s.stats().lastSettle.p_state,"SETTLED");
 assert.equal(s.stats().providerBody.store,false);
 assert.deepEqual(s.stats().providerBody.tools,[]);
 assert.equal(s.stats().providerBody.max_output_tokens,400);
}
{
 const s=setup({provider:new Response(JSON.stringify({
    id:"resp_bad_usage",model:"gpt-6-luna",status:"completed",
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
console.log("TASK20_ASK_ALPHA_OFFLINE_CONSENT_BUDGET_RECEIPT_PASS");
