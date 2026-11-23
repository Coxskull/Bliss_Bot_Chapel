import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {handleManualOperatorRequest} from "../supabase/functions/alpha-support-manual-operator/handler.mjs";

const actor="3c41599d-d4bb-466c-9d91-95290716c65c";
const caseId="ec0828e3-5261-438c-8706-4a89a3344b9a";
const token="e30.e30.e30"; // Synthetic shape ONLY; never a real credential
const origin="https://alphacreativecontent.com";
const calls=[];
const mkClient=(opts={})=>({
 auth:{
  getUser:async()=>opts.rejectAuth?{error:{message:"bad"}}:{data:{user:{id:actor}}},
  getClaims:async()=>opts.rejectAuth?{error:{message:"bad"}}:{data:{claims:{
   sub:opts.subject||actor,role:"authenticated",aal:opts.aal||"aal2"
  }}}
 },
 rpc:async(name,args)=>{
  calls.push({name,args});
  if(name==="alpha_support_manual_operator_authorized")
   return {data:opts.operatorGranted===true};
  if(name==="alpha_support_manual_operator_queue")return {data:[{
    case_id:caseId,category:"privacy",status:"OPEN",
    observed_at:"2026-11-12T04:00:00Z",created_at:"2026-11-12T04:01:00Z",
    claimed_by_me:false,customer_email:"PRIVATE-NOT-FOR-OUTPUT",
    provider_message_id:"SENSITIVE-GMAIL-REF"
  }]};
  if(name==="alpha_support_manual_operator_claim")return {data:true};
  throw Error("Unknown RPC");
 }
});
const req=(method,opts={})=>new Request(
 "https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-support-manual-operator"+(opts.query||""),
 {method,headers:{
  ...(opts.origin!==null?{Origin:opts.origin||origin}:{}),
  ...(opts.token!==null?{Authorization:"Bearer "+(opts.token||token)}:{}),
  ...(opts.body?{"Content-Type":"application/json"}:{})
 },...(opts.body?{body:JSON.stringify(opts.body)}:{})}
);
const response=async(method,opts={},api=mkClient())=>{
 const res=await handleManualOperatorRequest(req(method,opts),{client:api});
 const body=res.status===204?null:await res.json();
 return {res,body};
};
let testCount=0;
async function expect(method,opts,api,status,error){
 calls.length=0;
 const x=await response(method,opts,api);
 assert.equal(x.res.status,status);
 if(error)assert.equal(x.body.error,error);
 testCount++;
 return x;
}
await expect("GET",{origin:"https://evil.example"},mkClient(),403,"Forbidden origin");
await expect("GET",{origin:null},mkClient(),403,"Forbidden origin");
await expect("GET",{token:null},mkClient(),401,"Human login required");
await expect("GET",{token:"sb_publishable_forged"},mkClient(),401,"Human login required");
await expect("GET",{},mkClient({rejectAuth:true}),401,"Human login required");
await expect("GET",{},mkClient({subject:"47c98b44-19a8-4c70-bbb1-3eb39bf80333"}),401===403?401:403,"Verified staff MFA required");
await expect("GET",{},mkClient({aal:"aal1"}),403,"Verified staff MFA required");
const forbidden=await expect("GET",{},mkClient({operatorGranted:false}),403,"Operator permission denied");
assert.equal(calls.length,1);
const preflight=await expect("OPTIONS",{token:null},mkClient(),204);
assert.equal(preflight.res.headers.get("Access-Control-Allow-Origin"),origin);
const goodClient=mkClient({operatorGranted:true});
const q=await expect("GET",{query:"?page=0"},goodClient,200);
assert.equal(q.body.cases.length,1);
assert.equal(q.body.cases[0].category,"privacy");
assert.equal(q.body.cases[0].case_id,caseId);
assert.equal(q.body.cases[0].customer_email,undefined);
assert.ok(!JSON.stringify(q.body).includes("SENSITIVE-GMAIL-REF"));
assert.equal(q.res.headers.get("Cache-Control"),"no-store");
const onlyQueueCalls=calls.slice();
assert.equal(onlyQueueCalls.length,2);
assert.equal(onlyQueueCalls[1].name,"alpha_support_manual_operator_queue");
await expect("GET",{query:"?page=101"},goodClient,400,"Invalid queue page");
await expect("GET",{query:"?case_id="+caseId},goodClient,400,"Invalid queue page");
await expect("POST",{body:{action:"REFUND",case_id:caseId}},goodClient,422,"Invalid case operation");
await expect("POST",{body:{action:"CLOSE",case_id:caseId}},goodClient,422,"Invalid case operation");
await expect("POST",{body:{action:"CLAIM",case_id:caseId,owner:"admin"}},goodClient,422,"Invalid case operation");
const ok=await expect("POST",{body:{action:"CLAIM",case_id:caseId}},goodClient,200);
assert.deepEqual(ok.body,{case_id:caseId,status:"IN_REVIEW",action:"CLAIM",committed:true});
assert.equal(calls[1].name,"alpha_support_manual_operator_claim");
assert.equal(calls[1].args.p_actor_user_id,actor);
// Source assertions prevent merging a public token or refund path into this release.
const index=readFileSync(new URL("../supabase/functions/alpha-support-manual-operator/index.ts",import.meta.url),"utf8");
const ddl=readFileSync(new URL("./task21/production-locked-single-operator-20261112.sql",import.meta.url),"utf8");
assert.ok(index.includes("https://bkutbglzivfdnoerigyb.supabase.co"));
assert.ok(index.includes('Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")'));
assert.ok(!index.includes("verify_jwt:false"));
assert.ok(ddl.includes("CREATE TABLE alpha_support_private.manual_operator_grants"));
assert.ok(ddl.includes("REVOKE ALL ON FUNCTION public.alpha_support_manual_operator_claim(uuid,uuid)"));
assert.ok(ddl.includes("GRANT EXECUTE ON FUNCTION public.alpha_support_manual_operator_claim(uuid,uuid) TO service_role"));
assert.ok(!ddl.includes("INSERT INTO alpha_support_private.manual_operator_grants"));
console.log("TASK21_LOCKED_SINGLE_OPERATOR_OFFLINE_PASS cases_metadata_only true no_provider_egress tests="+testCount);
