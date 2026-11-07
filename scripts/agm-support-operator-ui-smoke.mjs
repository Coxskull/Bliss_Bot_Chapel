import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {validCase,parseQueue,parseDetail,validateTransition,safeOperatorConfig}
  from "../frontend/private/support-operator/operator-view-model.mjs";

const read=(path)=>readFileSync(new URL(path,import.meta.url),"utf8");
const id="00000000-0000-4000-8000-000000000001";
const request="00000000-0000-4000-8000-000000000002";
const owner="00000000-0000-4000-8000-000000000003";
const open={
  case_id:id,request_id:request,owner_user_id:null,status:"OPEN",
  category:"general",severity:"NORMAL",assigned_role:"GENERAL_SUPPORT",
  created_at:"2026-11-08T00:00:00Z",updated_at:"2026-11-08T00:00:00Z"
};
assert.equal(validCase(open),true);
assert.equal(validCase({...open,case_id:"not-a-uuid"}),false);
assert.equal(validCase({...open,assigned_role:"SUPER_ADMIN"}),false);
const page=parseQueue({cases:[open],page:0,page_size:20,
  visible_roles:["GENERAL_SUPPORT"]},0);
assert.equal(page.hasNext,false);
assert.equal(page.cases.length,1);
assert.throws(()=>parseQueue({cases:[open],page:1,page_size:20,
  visible_roles:["GENERAL_SUPPORT"]},0));
assert.throws(()=>parseQueue({cases:[{...open,assigned_role:"SECURITY_REVIEW"}],
  page:0,page_size:20,visible_roles:["GENERAL_SUPPORT"]},0));
assert.throws(()=>parseQueue({cases:[open],page:0,page_size:20,
  visible_roles:["SUPER_ADMIN"]},0));
assert.equal(parseDetail({case:open,contact:{email:"test@example.invalid",
  full_name:"Synthetic",description:"Fictional case"},events:[]},
  ["GENERAL_SUPPORT"],id).case.case_id,id);
assert.throws(()=>parseDetail({case:open,contact:{email:"test@example.invalid",
  full_name:"Synthetic",description:"Fictional case"},events:[]},
  ["SECURITY_REVIEW"],id));
assert.throws(()=>parseDetail({case:open,contact:{email:"test@example.invalid",
  full_name:"Synthetic",description:"Fictional case"},events:[]},
  ["GENERAL_SUPPORT"],owner));
assert.throws(()=>validateTransition(id,"CLOSED",owner,""));
assert.throws(()=>validateTransition(id,"IN_REVIEW","not-a-uuid",""));
assert.equal(validateTransition(id,"RESOLVED",owner,"Synthetic resolution").status,"RESOLVED");

const base={supabaseUrl:"https://bkutbglzivfdnoerigyb.supabase.co",
  publishableKey:"sb_publishable_testOnlyPUBLICkey",
  operatorOrigin:"https://staff.example.invalid",mutationsEnabled:false};
assert.equal(safeOperatorConfig(base,base.operatorOrigin),base);
for(const bad of [
  {...base,publishableKey:"sb_secret_badKey"},
  {...base,publishableKey:"REPLACE_WITH_PUBLISHABLE_OR_ANON_KEY"},
  {...base,operatorOrigin:"https://other.invalid"},
  {...base,supabaseUrl:"https://malicious.invalid"},
])assert.throws(()=>safeOperatorConfig(bad,base.operatorOrigin));
// Legacy JWTs must carry the anon role. Never allow the service_role token in
// an operator browser, even if it superficially has the JWT shape.
const jwt=(role)=>[
  Buffer.from(JSON.stringify({alg:"HS256",typ:"JWT"})).toString("base64url"),
  Buffer.from(JSON.stringify({role})).toString("base64url"),"signature"
].join(".");
assert.doesNotThrow(()=>safeOperatorConfig({...base,publishableKey:jwt("anon")},
  base.operatorOrigin));
assert.throws(()=>safeOperatorConfig({...base,publishableKey:jwt("service_role")},
  base.operatorOrigin));
const html=read("../frontend/private/support-operator/index.html");
const app=read("../frontend/private/support-operator/operator.js");
const build=read("../scripts/build-vercel-portal.mjs");
const configExample=read("../frontend/private/support-operator/operator-config.example.js");
const handler=read("../supabase/functions/alpha-support-operator/operator-handler.mjs");
assert.match(html,/noindex,nofollow,noarchive/);
assert.match(html,/Content-Security-Policy/);
assert.match(html,/id="signin-panel"/);
assert.match(html,/id="mfa-panel"/);
assert.match(html,/id="case-search-form"/);
assert.match(html,/id="transition-form"/);
assert.match(app,/auth\.signInWithPassword/);
assert.match(app,/auth\.mfa\.challengeAndVerify/);
assert.match(app,/persistSession:false/);
assert.match(app,/autoRefreshToken:false/);
assert.match(app,/credentials:"omit"/);
assert.match(app,/result\.committed!==true/);
assert.match(app,/canVerify=e\?\.message==="OPERATOR_DENIED"&&hasMfa/);
assert.match(handler,/resolution_summary,created_at/);
assert.match(handler,/.order\("severity",\{ascending:true\}\).order\("created_at",\{ascending:true\}\)/);
assert.ok(!/\.innerHTML\s*=/.test(app),"No untrusted case text may be rendered as HTML");
assert.ok(!/(?:localStorage|sessionStorage)\s*\.\s*(?:getItem|setItem)|(?:window|globalThis)\.(?:localStorage|sessionStorage)/.test(app),
  "Sensitive tokens must not use browser storage APIs");
assert.ok(!/SERVICE_ROLE_KEY|sb_secret_[A-Za-z0-9]+/.test(html+app+configExample),
  "No service-role or secret browser config");
assert.ok(!build.includes("support-operator"),
  "Private operator dashboard must never be in the public Vercel build");
assert.ok(!read("../frontend/private/support-operator/operator-config.example.js")
  .includes("BEGIN PRIVATE KEY"));
console.log("TASK21_OPERATOR_UI_SMOKE_PASS: role-scoped view-model, verified anon key, MFA path, private build exclusion, DOM XSS safety and memory-only session.");
