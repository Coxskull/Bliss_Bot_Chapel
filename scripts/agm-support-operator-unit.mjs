import {readFileSync} from "node:fs";
import assert from "node:assert/strict";
import { handleOperatorRequest } from "../supabase/functions/alpha-support-operator/operator-handler.mjs";
import { parseOperatorRoster, rolesForUser, mayAssign, parseTransition } from
  "../supabase/functions/alpha-support-operator/operator-contract.mjs";

const general="00000000-0000-4000-8000-000000000001";
const security="00000000-0000-4000-8000-000000000002";
const outsider="00000000-0000-4000-8000-000000000003";
const id="00000000-0000-4000-8000-000000000004";
const requestId="00000000-0000-4000-8000-000000000005";
const roster = JSON.stringify({GENERAL_SUPPORT:[general],SECURITY_REVIEW:[security]});
const parsed = parseOperatorRoster(roster);
assert.deepEqual(rolesForUser(parsed,general,"aal1"),["GENERAL_SUPPORT"]);
assert.deepEqual(rolesForUser(parsed,security,"aal1"),[]);
assert.deepEqual(rolesForUser(parsed,security,"aal2"),["SECURITY_REVIEW"]);
assert.equal(mayAssign(parsed,"GENERAL_SUPPORT",security),false);
assert.equal(mayAssign(parsed,"GENERAL_SUPPORT",general),true);
for (const bad of ["", "{}", "{", '{"ADMIN":["'+general+'"]}',
  '{"GENERAL_SUPPORT":["wrong"]}'])
 assert.throws(()=>parseOperatorRoster(bad));
assert.throws(()=>parseTransition({case_id:id,status:"CLOSED",owner_user_id:general,resolution:""}));

const ticket = {case_id:id,request_id:requestId,category:"general",
  severity:"NORMAL",assigned_role:"GENERAL_SUPPORT",owner_user_id:null,
  status:"OPEN",created_at:"2026-11-08T00:00:00Z"};
const contact={full_name:"Synthetic User",email:"unit@example.invalid",description:"Test only"};
const events=[{event_id:1,action:"CREATED",status:"OPEN",summary:"Synthetic"}];
let user=general,aal="aal1",role="authenticated",authFails=false;
let rpcCount=0,claimCount=0,readCount=0, selectedTicket=ticket, finish={data:true,error:null};
let envState={ALPHA_SUPPORT_OPERATOR_ROSTER:roster,ALPHA_SUPPORT_MUTATIONS_ENABLED:"false",
  ALPHA_SUPPORT_OPERATOR_ORIGIN:"https://ops.example.invalid"};
const env={get:k=>envState[k]};
const db={
  auth:{
    getUser:async()=> authFails ? {data:null,error:{message:"Denied"}}
      : {data:{user:{id:user}},error:null},
    getClaims:async()=>({data:{claims:{sub:user,aal,role}},error:null})
  },
  from(table){
    readCount++;
    return {
      select(){return this;},
      eq(){return this;},
      in(){return this;},
      order(){return this;},
      range:async()=>({data:table==="alpha_support_cases" ? [selectedTicket] : events,error:null}),
      limit:async()=>({data:table==="alpha_support_cases" ? [selectedTicket] : events,error:null}),
      maybeSingle:async()=>({data:table==="alpha_support_cases" ? selectedTicket : contact,error:null})
    };
  },
  rpc:async(name,args)=>{
    assert.equal(args.p_actor_user_id,user);
    if(name==="alpha_support_claim"){claimCount++;return finish;}
    assert.equal(name,"alpha_support_transition");
    rpcCount++;
    return finish;
  }
};
const url="https://example.invalid/functions/v1/alpha-support-operator";
const token="Bearer aaa.bbb.ccc";
const request=(method="GET",path="",opts={})=>new Request(url+path,{
  method,headers:{Authorization:token,...opts.headers},
  ...(method==="POST" ? {body:JSON.stringify(opts.body??{
    case_id:id,status:"TRIAGED",owner_user_id:general,resolution:""
  }),headers:{Authorization:token,"Content-Type":"application/json",...opts.headers}} : {})
});
const call=async r=>{const resp=await handleOperatorRequest(r,db,env);
  return {status:resp.status,data:await resp.json(),headers:resp.headers};};

let t;
t=await call(new Request(url)); assert.equal(t.status,401);
envState.ALPHA_SUPPORT_OPERATOR_ROSTER=undefined;
t=await call(request());assert.equal(t.status,503);
envState.ALPHA_SUPPORT_OPERATOR_ROSTER=roster;
user=outsider;t=await call(request());assert.equal(t.status,403);
user=general;authFails=true;t=await call(request());assert.equal(t.status,401);
authFails=false;role="anon";t=await call(request());assert.equal(t.status,401);
role="authenticated";
t=await call(request());assert.equal(t.status,200);
assert.equal(t.data.cases.length,1);
assert.deepEqual(t.data.visible_roles,["GENERAL_SUPPORT"]);
assert.equal(t.data.page,0);
t=await call(request("GET","?page=101"));assert.equal(t.status,400);
t=await call(request("GET","?page=1"));assert.equal(t.status,200);
assert.equal(t.data.page,1);
assert.ok(!("description" in t.data.cases[0]),"list must not disclose case text");
t=await call(request("GET","?case_id="+id));assert.equal(t.status,200);
assert.equal(t.data.contact.email,contact.email);
assert.equal(t.data.events[0].action,"CREATED");
selectedTicket={...ticket,assigned_role:"SECURITY_REVIEW",category:"security_incident"};
const readsBefore=readCount;
t=await call(request("GET","?case_id="+id));assert.equal(t.status,404);
assert.equal(readCount-readsBefore,1,"unauthorized detail must not load PII or events");
user=security;aal="aal1";t=await call(request());assert.equal(t.status,403);
aal="aal2";t=await call(request("GET","?case_id="+id));assert.equal(t.status,200);
user=general;aal="aal1";selectedTicket=ticket;
t=await call(request("POST"));assert.equal(t.status,423);assert.equal(rpcCount,0);
t=await call(request("POST","",{body:{action:"CLAIM",case_id:id}}));
assert.equal(t.status,423);assert.equal(claimCount,0);
envState.ALPHA_SUPPORT_MUTATIONS_ENABLED="true";
t=await call(request("POST","",{body:{action:"CLAIM",case_id:id,owner_user_id:security}}));
assert.equal(t.status,422);assert.equal(claimCount,0);
t=await call(request("POST","",{body:{action:"CLAIM",case_id:id}}));
assert.equal(t.status,200);assert.equal(t.data.action,"CLAIM");assert.equal(claimCount,1);
finish={data:false,error:null};
t=await call(request("POST","",{body:{action:"CLAIM",case_id:id}}));
assert.equal(t.status,409);assert.equal(claimCount,2);
finish={data:true,error:null};
t=await call(request("POST","",{body:{
  case_id:id,status:"TRIAGED",owner_user_id:security,resolution:""}}));
assert.equal(t.status,403);assert.equal(rpcCount,0);
t=await call(request("POST","",{body:{
  case_id:id,status:"CLOSED",owner_user_id:general,resolution:""}}));
assert.equal(t.status,422);assert.equal(rpcCount,0);
t=await call(request("POST"));assert.equal(t.status,200);
assert.equal(t.data.committed,true);assert.equal(rpcCount,1);
finish={data:false,error:null};
t=await call(request("POST"));assert.equal(t.status,409);assert.equal(rpcCount,2);
t=await call(request("GET","",{headers:{Origin:"https://untrusted.invalid"}}));
assert.equal(t.status,403);
t=await call(request("GET","",{headers:{Origin:"https://ops.example.invalid"}}));
assert.equal(t.status,200);
assert.equal(t.headers.get("Access-Control-Allow-Origin"),"https://ops.example.invalid");
const pre=await handleOperatorRequest(new Request(url,{method:"OPTIONS",
  headers:{Origin:"https://ops.example.invalid"}}),db,env);
assert.equal(pre.status,204);
assert.equal(pre.headers.get("Access-Control-Allow-Headers"),"Authorization, apikey, Content-Type");
const config=readFileSync(new URL("../supabase/config.toml",import.meta.url),"utf8");
assert.match(config,/\[functions\.alpha-public-intake\]\s*verify_jwt\s*=\s*false/);
assert.match(config,/\[functions\.alpha-support-operator\]\s*verify_jwt\s*=\s*true/);
const supportPage=readFileSync(new URL("../frontend/public/support.html",import.meta.url),"utf8");
assert.ok(!supportPage.includes("SUPABASE_SERVICE_ROLE_KEY"));
assert.ok(!supportPage.includes("ALPHA_SUPPORT_OPERATOR_ROSTER"));

// Staging-only alternate: metadata-backed grants live in a non-exposed,
// privileged database schema. These are zero-network synthetic fixtures.
const stageOrigin="https://bliss-task21ci-alpha-auto-s-projects.vercel.app";
const stageRpcOriginal=db.rpc;
let stageGrantEnabled=true, badRole=false, originChecked=0;
let trialAllowed=false,trialActions=0;
envState={
  ALPHA_SUPPORT_OPERATOR_ROSTER:undefined,
  ALPHA_SUPPORT_OPERATOR_ORIGIN:undefined,
  ALPHA_SUPPORT_MUTATIONS_ENABLED:"false",
  SUPABASE_URL:"https://wdlmcriajgsretgyusmo.supabase.co"
};
user=general;role="authenticated";aal="aal1";selectedTicket=ticket;
db.rpc=async(name,args)=>{
  if(name==="alpha_support_staging_origin_allowed"){
    originChecked++;
    return {data:stageGrantEnabled&&args.p_origin===stageOrigin,error:null};
  }
  if(name==="alpha_support_staging_roles"){
    return {data:badRole?["SECURITY_REVIEW"]:
      (stageGrantEnabled&&args.p_user_id===general&&
       args.p_origin===stageOrigin&&["aal1","aal2"].includes(args.p_aal)
       ?["GENERAL_SUPPORT"]:[]),error:null};
  }
  if(name==="alpha_support_staging_trial_allowed"){
    return {data:trialAllowed&&stageGrantEnabled&&
       args.p_case_id===id&&args.p_actor_user_id===general&&
       args.p_origin===stageOrigin,error:null};
  }
  if(name==="alpha_support_staging_trial_action"){
    trialActions++;
    return {data:trialAllowed&&stageGrantEnabled&&
      args.p_case_id===id&&args.p_actor_user_id===general&&
      args.p_origin===stageOrigin,error:null};
  }
  return stageRpcOriginal(name,args);
};
const stagingRequest=(method="GET",urlPath="")=>request(method,urlPath,{
  headers:{Origin:stageOrigin}
});
t=await call(stagingRequest());
assert.equal(t.status,200,"Approved staging grant should show only GENERAL_SUPPORT");
assert.deepEqual(t.data.visible_roles,["GENERAL_SUPPORT"]);
assert.equal(t.data.operator_user_id,general);
t=await call(stagingRequest("GET","?case_id="+id));
assert.equal(t.status,200);
assert.equal(t.data.synthetic_trial_enabled,false);
t=await call(stagingRequest("POST"));
assert.equal(t.status,423,"Staging owner must NOT receive mutation ability");
assert.equal(originChecked>0,true);
const trialPost=(body)=>request("POST","",{
  headers:{Origin:stageOrigin},body
});
// The earlier denied POST also reached the private RPC while disabled;
// reset this synthetic test counter before the positive grant.
trialActions=0;
trialAllowed=true;
t=await call(stagingRequest("GET","?case_id="+id));
assert.equal(t.status,200);
assert.equal(t.data.synthetic_trial_enabled,true,
  "Case detail may only advertise server-approved trial");
t=await call(trialPost({action:"CLAIM",case_id:id}));
assert.equal(t.status,200);
assert.equal(t.data.case_id,id);
assert.equal(trialActions,1,"Only approved synthetic claim reaches trial action RPC");
t=await call(trialPost({action:"CLAIM",case_id:requestId}));
assert.equal(t.status,423);
assert.equal(trialActions,2,"Wrong case is refused by DB trial RPC");
trialAllowed=false;
t=await call(stagingRequest("GET","?case_id="+id));
assert.equal(t.status,200);
assert.equal(t.data.synthetic_trial_enabled,false);
t=await call(trialPost({action:"CLAIM",case_id:id}));
assert.equal(t.status,423,"Revoked trial must reject POST without global activation");
assert.equal(trialActions,3);
t=await call(request("GET","",{headers:{Origin:"https://bliss-other-alpha-auto-s-projects.vercel.app"}}));
assert.equal(t.status,403,"Lookalike hostname refused");
t=await call(request());
assert.equal(t.status,503,"No Origin may not authorize a roster-less browser session");
user=outsider;
t=await call(stagingRequest());
assert.equal(t.status,403,"Wrong verified user must not receive a granted role");
user=general;stageGrantEnabled=false;
t=await call(stagingRequest());
assert.equal(t.status,403,"Revoked origin/roster must remain denied");
stageGrantEnabled=true;badRole=true;
t=await call(stagingRequest());
assert.equal(t.status,503,"Any unauthorized sensitive role from DB is a hard configuration failure");
badRole=false;
envState.SUPABASE_URL="https://bkutbglzivfdnoerigyb.supabase.co";
t=await call(stagingRequest());
assert.equal(t.status,403,"Production project URL must never activate the staging fallback");
db.rpc=stageRpcOriginal;

console.log("TASK21_OPERATOR_AUTH_CONTRACT_PASS: 2FA-sensitive queue, JWT/auth guard, read-only default, scoped owner, audit RPC and CORS.");
