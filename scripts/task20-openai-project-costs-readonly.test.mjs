import test from "node:test";
import assert from "node:assert/strict";
import {readOpenAiCurrentMonthCosts} from "./task20-openai-project-costs-readonly.mjs";

const project="proj_RDrdpdY1ufheFvoJu7mLWhDc";
const vars=()=>({
 TASK20_COSTS_READONLY_OPT_IN:"YES",
 OPENAI_ADMIN_KEY:"sk-admin-synthetic-never-real-0123456789",
 ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID:"org_synthetic_alpha_2026",
 ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID:project
});
const now=new Date("2026-11-10T02:20:00Z"),start=1793491200;
const bucket=(cost=0.04)=>({
 object:"bucket",start_time:start,end_time:start+86400,
 results:[{object:"organization.costs.result",
   amount:{currency:"usd",value:cost},project_id:project}]
});
const reply=(p={object:"page",has_more:false,next_page:null,data:[bucket()]})=>
 new Response(JSON.stringify(p),{status:200,headers:{"content-type":"application/json"}});

test("must explicitly opt in and pin reviewed project+organization and admin key",async()=>{
 const cases=[
  {...vars(),TASK20_COSTS_READONLY_OPT_IN:""},
  {...vars(),OPENAI_ADMIN_KEY:""},
  {...vars(),ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID:""},
  {...vars(),ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID:"org_invalid\nInjected"},
  {...vars(),ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID:"proj_other_12345"},
  {...vars(),OPENAI_ADMIN_KEY:"key\nInjected"}
 ];let network=0;
 for(const env of cases){
  const r=await readOpenAiCurrentMonthCosts({env,now,fetcher:async()=>{network++;throw Error("forbidden");}});
  assert.equal(r.ok,false);assert.ok(r.code.endsWith("REQUIRED")||r.code.includes("NOT_"));
 }
 assert.equal(network,0);
});

test("read-only monthly cost query is project and organization pinned, with no secret in output",async()=>{
 let count=0;
 const r=await readOpenAiCurrentMonthCosts({env:vars(),now,fetcher:async(url,options)=>{
  count++;
  const parsed=new URL(url);
  assert.equal(parsed.origin,"https://api.openai.com");
  assert.equal(parsed.pathname,"/v1/organization/costs");
  assert.equal(parsed.searchParams.get("start_time"),String(start));
  assert.equal(parsed.searchParams.get("end_time"),String(Math.floor(now.getTime()/1000)));
  assert.equal(parsed.searchParams.get("bucket_width"),"1d");
  assert.equal(parsed.searchParams.get("limit"),"50");
  assert.deepEqual(parsed.searchParams.getAll("project_ids[]"),[project]);
  assert.deepEqual(parsed.searchParams.getAll("group_by[]"),["project_id"]);
  assert.equal(options.method,"GET");
  assert.equal(options.redirect,"error");
  assert.equal(options.headers.Authorization,"Bearer "+vars().OPENAI_ADMIN_KEY);
  assert.equal(options.headers["OpenAI-Organization"],vars().ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID);
  return reply();
 }});
 assert.equal(count,1);assert.equal(r.ok,true);
 assert.equal(r.project_cost_usd,"0.040000");
 assert.equal(r.month_utc,"2026-11");
 assert.equal(r.amount_below_9,true);
 assert.equal(r.public_ai_activation_authorized_by_this_check,false);
 assert.equal(r.safe_to_refresh_database_meter_automatically,false);
 assert.equal(r.api_key_model_access_verified,false);
 assert.doesNotMatch(JSON.stringify(r),/sk-admin|proj_RDr|org_synthetic/);
});

test("401/403 and network errors are redacted and fail closed",async()=>{
 for(const status of [401,403]){
  const r=await readOpenAiCurrentMonthCosts({env:vars(),now,
   fetcher:async()=>new Response("admin credential not allowed",{status})});
  assert.deepEqual(r,{ok:false,code:"ADMIN_ACCESS_DENIED"});
 }
 const r=await readOpenAiCurrentMonthCosts({env:vars(),now,
  fetcher:async()=>{throw Error("secret-admin-key-should-never-appear");}});
 assert.deepEqual(r,{ok:false,code:"NETWORK_OR_RESPONSE_UNVERIFIED"});
});

test("rejects incomplete pages, unknown projects, nonUSD, negative and duplicate cost buckets",async()=>{
 const invalid=[
  {...{object:"page",has_more:true,next_page:"unsafe",data:[bucket()]}},
  {object:"page",has_more:false,next_page:null,data:[{...bucket(),results:[
   {...bucket().results[0],project_id:"proj_other"}]}]},
  {object:"page",has_more:false,next_page:null,data:[{...bucket(),results:[
   {...bucket().results[0],amount:{value:1,currency:"eur"}}]}]},
  {object:"page",has_more:false,next_page:null,data:[bucket(-0.01)]},
  {object:"page",has_more:false,next_page:null,data:[bucket(),bucket()]},
  {object:"page",has_more:false,next_page:null,data:[]},
 ];
 for(const data of invalid){
  const result=await readOpenAiCurrentMonthCosts({env:vars(),now,fetcher:async()=>reply(data)});
  assert.equal(result.ok,false);
 }
});

test("cost equal to or exceeding $9 never authorizes activation",async()=>{
 const r=await readOpenAiCurrentMonthCosts({env:vars(),now,fetcher:async()=>reply({
  object:"page",has_more:false,next_page:null,data:[bucket(9.0001)]})});
 assert.equal(r.ok,true);
 assert.equal(r.code,"COSTS_RETRIEVED_AT_OR_OVER_CAP");
 assert.equal(r.amount_below_9,false);
 assert.equal(r.public_ai_activation_authorized_by_this_check,false);
});
