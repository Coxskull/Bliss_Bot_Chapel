import assert from "node:assert/strict";
import test from "node:test";
import {
  validateAskAlpha, calculateLunaUsd, readBoundedProviderJson,
  handleAskAlpha, askAlphaConfiguration
} from "../supabase/functions/alpha-public-intake/ask-alpha.mjs";

// No production HTTP, no real API key, no customer data and no database writes.
// The real handler is exercised with synthetic request, RPC and provider mocks.
const validId = n => "00000000-0000-4000-8000-" + String(n).padStart(12, "0");
const consented = () => ({
  action: "ask_alpha", audience: "general",
  notice_version: "AGM-ASK-ALPHA-AI-2026-11-08",
  ai_processing_consent: true, marketing_opt_in: false,
  session_id: validId(1), request_key: validId(2),
  message: "What planning services does ALPHA offer?"
});
const liveDisabled = () => ({
  ALPHA_ASK_ALPHA_ENABLED: "false",
  OPENAI_API_KEY: "synthetic-local-key",
  ALPHA_ASK_ALPHA_HASH_SECRET: "synthetic-only-salt-".repeat(4)
});
const enabled = () => ({ ...liveDisabled(), ALPHA_ASK_ALPHA_ENABLED: "true" });
const environment = vars => key => vars[key] ?? null;
const request = (ip = "203.0.113.7") => new Request(
  "https://example.invalid/functions/v1/alpha-public-intake",
  { headers: ip === null ? {} : {"cf-connecting-ip":ip} }
);
const out = (_origin, body, status=200) => ({status,body});
const receipt = validId(15);
const makeClient = (mode="ok") => {
  const calls=[];
  const rpc=async (name, params) => {
    calls.push({name,params});
    if(name==="alpha_ask_alpha_reserve") {
      if(mode==="quota") return {error:{code:"P0001"}};
      if(mode==="replay") return {data:[{receipt_id:receipt,state:"RESERVED",is_replay:true}]};
      return {data:[{receipt_id:receipt,state:"RESERVED",is_replay:false}]};
    }
    if(name==="alpha_ask_alpha_settle") {
      if(mode==="settleFailure") return {error:{code:"PGRST500"}};
      return {data:true};
    }
    throw Error("unexpected RPC: "+name);
  };
  return {rpc,calls};
};
const response = (overrides={}) => {
  const data={
    id:"resp_synthetic_success", status:"completed", model:"gpt-6-luna",
    service_tier:"default",
    output:[{type:"message",content:[{type:"output_text",text:
      "ALPHA accepts planning inquiries; no purchase or booking is created."}]}],
    usage:{input_tokens:100,output_tokens:120,
      input_tokens_details:{cached_tokens:0,cache_write_tokens:0}},
    ...overrides
  };
  return new Response(JSON.stringify(data),{
    status:200,headers:{"content-type":"application/json"}
  });
};
const context=(client,providerFetch,vars=enabled(),ip="203.0.113.7",payload=consented())=>({
  client,providerFetch,env:environment(vars),
  origin:"https://alphacreativecontent.com",req:request(ip),out,payload
});

test("Ask Alpha model, notice and budget match the permitted synthetic contract",()=>{
  assert.equal(askAlphaConfiguration.model,"gpt-6-luna");
  assert.equal(askAlphaConfiguration.notice,"AGM-ASK-ALPHA-AI-2026-11-08");
  assert.equal(askAlphaConfiguration.reservedUsd,0.01);
  assert.equal(askAlphaConfiguration.dailyUsd,1);
  assert.equal(askAlphaConfiguration.monthlyUsd,10);
  assert.equal(validateAskAlpha(consented()),null);
  assert.equal(calculateLunaUsd({input_tokens:100,output_tokens:120,
    input_tokens_details:{cached_tokens:0,cache_write_tokens:0}}),0.00007);
  assert.equal(calculateLunaUsd({input_tokens:100,output_tokens:0,
    input_tokens_details:{cached_tokens:20,cache_write_tokens:10}}),0.0000097);
  assert.throws(()=>calculateLunaUsd({input_tokens:100,output_tokens:401}),
    /PROVIDER_USAGE_UNVERIFIED/);
});

test("consent and personal-information denials happen before metering and providers",async()=>{
  const invalid=[
    [{ai_processing_consent:false},"CONSENT_REQUIRED",403],
    [{notice_version:"LEGACY"},"CONSENT_REQUIRED",403],
    [{request_key:"not-a-uuid"},"INVALID_SESSION",422],
    [{audience:"staff_admin"},"INVALID_AUDIENCE",422],
    [{message:"My password: 123abc"},"PERSONAL_DATA_NOT_ACCEPTED",422],
    [{message:"Contact me at person@example.com"},"PERSONAL_DATA_NOT_ACCEPTED",422],
    [{history:[{role:"user",content:"My OTP: 223344"}]},"PERSONAL_DATA_NOT_ACCEPTED",422],
    [{marketing_opt_in:"yes"},"INVALID_MARKETING_CONSENT",422],
    [{message:"x".repeat(401)},"INVALID_MESSAGE",422]
  ];
  for(const [change,error,status] of invalid) {
    const client=makeClient();
    let providerCalls=0;
    const providerFetch=async()=>{providerCalls++;throw Error("unexpected provider call")};
    const actual=await handleAskAlpha(context(client,providerFetch,enabled(),
      "203.0.113.7",{...consented(),...change}));
    assert.equal(actual.status,status,error);
    assert.equal(actual.body.error,error);
    assert.equal(providerCalls,0,error);
    assert.equal(client.calls.length,0,error);
  }
});

test("disabled, unconfigured, or untrusted IP request fails without a paid call",async()=>{
  const cases=[
    [liveDisabled(),"203.0.113.7","AI_NOT_ENABLED"],
    [ {...enabled(), OPENAI_API_KEY:""},"203.0.113.7","AI_SERVICE_NOT_CONFIGURED"],
    [ {...enabled(), ALPHA_ASK_ALPHA_HASH_SECRET:"too-short"},"203.0.113.7","AI_SERVICE_NOT_CONFIGURED"],
    [enabled(),null,"RATE_LIMIT_IDENTITY_UNAVAILABLE"],
    [enabled(),"malformed-client-supplied-ip","RATE_LIMIT_IDENTITY_UNAVAILABLE"]
  ];
  for(const [vars,ip,error] of cases) {
    const client=makeClient();let paidCalls=0;
    const actual=await handleAskAlpha(context(client,async()=>{
      paidCalls++;throw Error("paid provider invoked unexpectedly");
    },vars,ip));
    assert.equal(actual.status,503,error);
    assert.equal(actual.body.error,error);
    assert.equal(client.calls.length,0,error);
    assert.equal(paidCalls,0,error);
  }
});

test("valid synthetic request reserves budget, settles exact usage, returns receipt (no marketing)",async()=>{
  const client=makeClient();let paidCalls=0;
  const actual=await handleAskAlpha(context(client,async(_url,opts)=>{
    paidCalls++;
    assert.equal(_url,"https://api.openai.com/v1/responses");
    assert.equal(opts.method,"POST");
    assert.equal(opts.headers["Idempotency-Key"],validId(2));
    const posted=JSON.parse(opts.body);
    assert.equal(posted.model,"gpt-6-luna");
    assert.equal(posted.store,false);
    assert.deepEqual(posted.tools,[]);
    assert.equal(posted.max_output_tokens,400);
    assert.equal(posted.reasoning.effort,"none");
    assert.equal(posted.input.at(-1).content,consented().message);
    return response();
  }));
  assert.equal(actual.status,200);
  assert.equal(actual.body.status,"ANSWERED");
  assert.equal(actual.body.receipt_id,receipt);
  assert.equal(actual.body.marketing_opt_in,false);
  assert.equal(actual.body.support_case_created,false);
  assert.match(actual.body.answer,/planning inquiries/);
  assert.equal(paidCalls,1);
  assert.deepEqual(client.calls.map(c=>c.name),
    ["alpha_ask_alpha_reserve","alpha_ask_alpha_settle"]);
  assert.equal(client.calls[0].params.p_marketing_opt_in,false);
  assert.equal(client.calls[1].params.p_state,"SETTLED");
  assert.equal(client.calls[1].params.p_actual_usd,0.00007);
  assert.match(client.calls[1].params.p_output_hash,/^[0-9a-f]{64}$/);
  assert.doesNotMatch(JSON.stringify(client.calls),/synthetic-local-key/);
});

test("budget rejection and duplicate reservation never call the provider",async()=>{
  for(const [mode,error,status] of [
    ["quota","AI_BUDGET_OR_RATE_LIMIT_REACHED",429],
    ["replay","REQUEST_ALREADY_RESERVED",409]
  ]) {
    const client=makeClient(mode);let count=0;
    const actual=await handleAskAlpha(context(client,async()=>{count++;return response()}));
    assert.equal(actual.status,status,mode);
    assert.equal(actual.body.error,error,mode);
    assert.equal(count,0,mode);
    assert.deepEqual(client.calls.map(c=>c.name),["alpha_ask_alpha_reserve"]);
  }
});

test("provider failure or unverified output settles UNKNOWN and never exposes answer",async()=>{
  for(const kind of ["http-failure","bad-model","invalid-usage"]) {
    const client=makeClient();let providerCalls=0;
    const providerFetch=async()=>{
      providerCalls++;
      if(kind==="http-failure") return new Response("",{status:401});
      if(kind==="bad-model") return response({model:"unapproved-model"});
      return response({usage:{input_tokens:100,output_tokens:401}});
    };
    const actual=await handleAskAlpha(context(client,providerFetch));
    assert.equal(actual.status,503,kind);
    assert.equal(actual.body.error,"AI_UNAVAILABLE_RECONCILIATION_REQUIRED",kind);
    assert.equal(actual.body.answer,undefined,kind);
    assert.equal(client.calls[1].params.p_state,"UNKNOWN",kind);
    assert.equal(client.calls[1].params.p_actual_usd,null,kind);
    assert.equal(providerCalls,1,kind);
  }
});

test("receipt settlement failure cannot be misreported as successful chat",async()=>{
  const client=makeClient("settleFailure");
  const actual=await handleAskAlpha(context(client,async()=>response()));
  assert.equal(actual.status,503);
  assert.equal(actual.body.error,"AI_RECEIPT_NOT_COMMITTED");
  assert.equal(actual.body.answer,undefined);
  assert.deepEqual(client.calls.map(x=>x.name),
    ["alpha_ask_alpha_reserve","alpha_ask_alpha_settle"]);
});

test("provider body is capped independently from Content-Length",async()=>{
  await assert.rejects(()=>readBoundedProviderJson(
    new Response("x".repeat(30001))),/PROVIDER_RESPONSE_TOO_LARGE/);
  await assert.rejects(()=>readBoundedProviderJson(
    new Response("{}",{headers:{"content-length":"30001"}})),/PROVIDER_RESPONSE_TOO_LARGE/);
});
