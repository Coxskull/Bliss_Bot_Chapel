// Task21 public/static staff UI guard: no credentials embedded or exposed.
// No login attempts, no Supabase requests, no customer reads, no paid provider.
import assert from "node:assert/strict";
import {readFileSync,existsSync} from "node:fs";
const get=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");
const html=get("frontend/public/support-operator.html");
const css=get("frontend/public/support-operator.css");
const js=get("frontend/public/support-operator.js");
const build=get("scripts/build-vercel-portal.mjs");
const vercel=JSON.parse(get("vercel.json"));
const edge=get("supabase/functions/alpha-support-manual-operator/handler.mjs");
for(const phrase of [
 'content="noindex,nofollow,noarchive"',
 'content="no-referrer"',
 'id="signin-form"',
 'id="mfa-verify-form"',
 'id="enroll-verify-form"',
 'id="pending-user-id"',
 'id="case-rows"',
 'src="/support-operator.js"',
 'href="/support-operator.css"',
 "No 24-hour staffed service is promised"
])assert.ok(html.includes(phrase),"Missing private staff UI marker: "+phrase);
for(const phrase of [
 "const PROJECT=\"https://bkutbglzivfdnoerigyb.supabase.co\"",
 "sb_publishable_",
 'grant_type=password',
 'method:"POST"',
 '"/factors"',
 '"/challenge"',
 '"/verify"',
 '"/user"',
 "mfa-enroll-secret",
 "resetSession()",
 '"OPERATOR_NOT_AUTHORIZED"',
 '"/functions/v1/alpha-support-manual-operator"',
 'action:"CLAIM"',
 "case_id:caseId",
 'credentials:"omit"',
 'cache:"no-store"',
 'referrerPolicy:"no-referrer"',
 'await loadQueue(0)'
])assert.ok(js.includes(phrase),"Missing auth safety marker: "+phrase);
for(const forbidden of [
 /window\.localStorage\b/,/window\.sessionStorage\b/,
 /(?:localStorage|sessionStorage)\.setItem\s*\(/,
 /document\.cookie/,/\binnerHTML\s*=/,
 /SUPABASE_SERVICE_ROLE_KEY/,/sb_secret_/,
 /<script[^>]+https?:/i,
 /https?:\/\/(?:cdn\.|esm\.|unpkg\.)/i,
 /(?:sendRefund|approveRefund|sendCustomerEmail|issuePayment|deleteCase)\s*\(/
])assert.doesNotMatch(js,forbidden,"Unsafe browser staff script");
assert.doesNotMatch(html,/<script[^>]+https?:/i);
assert.doesNotMatch(html,/<form[^>]+action=/i);
assert.doesNotMatch(js,/if\(busy\)throw Error\("BUSY"\)/);
assert.ok(css.length>1000);
const route=vercel.headers.find(h=>h.source==="/support-operator.html");
assert.ok(route,"No exact staff route security headers");
const dict=Object.fromEntries(route.headers.map(x=>[x.key,x.value]));
assert.match(dict["Content-Security-Policy"],/script-src 'self'/);
assert.match(dict["Content-Security-Policy"],/connect-src https:\/\/bkutbglzivfdnoerigyb.supabase.co/);
assert.match(dict["Content-Security-Policy"],/frame-ancestors 'none'/);
assert.match(dict["Content-Security-Policy"],/form-action 'none'/);
assert.equal(dict["X-Frame-Options"],"DENY");
assert.match(dict["Cache-Control"],/no-store/);
assert.match(dict["X-Robots-Tag"],/noindex/);
assert.match(build,/'support-operator.html', 'support-operator.css', 'support-operator.js'/);
assert.ok(edge.includes('claims.aal!=="aal2"'));
assert.ok(edge.includes("alpha_support_manual_operator_authorized"));
assert.ok(!html.includes("Staff account created"));
assert.ok(existsSync(new URL("../.vercel-portal/support-operator.html",import.meta.url)),
 "Staff UI absent from actual public build output");
for(const file of ["support-operator.css","support-operator.js"]){
 assert.ok(existsSync(new URL("../.vercel-portal/"+file,import.meta.url)),
   "Missing staff asset from public build output: "+file);
}
console.log("TASK21_SINGLE_OPERATOR_STATIC_ROUTE_DEFAULT_LOCKED_PASS — no browser storage, no external scripts, private server authorization");

// Task330 browser fixture: no network, real user, or actual credentials.
for(const marker of ['id="intake-review-refresh"','id="intake-review-details" hidden','id="intake-review-rows"'])
 assert.ok(html.includes(marker));
assert.ok(js.includes('"/functions/v1/alpha-intake-operator-review"'));
const {runInNewContext}=await import("node:vm");
const nodes=new Map();
function node(){
 return {hidden:false,disabled:false,value:"",textContent:"",children:[],listeners:{},
  addEventListener(type,fn){this.listeners[type]=fn;},
  replaceChildren(){this.children=[];},append(...children){this.children.push(...children);}};
}
const doc={getElementById(id){if(!nodes.has(id))nodes.set(id,node());return nodes.get(id);},
 querySelectorAll(){return [];},createElement(){return node();},addEventListener(){},hidden:false};
let nextResponse,requests=[];
const ctx={document:doc,window:{addEventListener(){}},AbortSignal,Intl,Date,Set,
 setTimeout:()=>1,clearTimeout:()=>{},fetch:async(url,opts)=>{requests.push({url,opts});return nextResponse;}};
runInNewContext(js,ctx);
runInNewContext('token="fictional-token";expiresAt=Date.now()+60000;showPanel("queue-panel");',ctx);
const item={request_id:"11111111-1111-4111-8111-111111111111",kind:"advertiser",
 received_at:"2026-11-24T01:00:00Z",receipt_id:"22222222-2222-4222-8222-222222222222",
 crm_delivery_state:"DELIVERED",review_state:"REVIEW_READY",manual_identity_and_rights_review_required:true,
 invitation_authorized:false,campaign_authorized:false,payment_authorized:false};
const queue={source:"BLISS_PUBLIC_INTEREST_ONLY",readonly:true,count:1,review_ready:1,items:[item]};
const run=()=>runInNewContext("loadIntakeReview()",ctx);
nextResponse={ok:true,status:200,json:async()=>queue};
await run();
assert.equal(nodes.get("intake-review-rows").children.length,1);
assert.equal(nodes.get("intake-review-details").hidden,false);
assert.equal(requests[0].opts.method,"GET");
assert.deepEqual(Object.keys(requests[0].opts.headers),["Authorization"]);
assert.equal(requests[0].opts.cache,"no-store");
assert.match(nodes.get("intake-review-result").textContent,/Ready for human review: 1/);
for(const invalid of [{...queue,count:2},{...queue,items:[{...item,email:"fixture@example.test"}]},
 {...queue,items:[{...item,invitation_authorized:true}]}]){
 nextResponse={ok:true,status:200,json:async()=>invalid};await run();
 assert.equal(nodes.get("intake-review-rows").children.length,0);
 assert.equal(nodes.get("intake-review-details").hidden,true);
}
nextResponse={ok:false,status:403};await run();
assert.equal(nodes.get("intake-review-rows").children.length,0);
nextResponse={ok:true,status:200,json:async()=>queue};await run();
runInNewContext("resetSession()",ctx);
assert.equal(nodes.get("intake-review-rows").children.length,0);
assert.equal(nodes.get("intake-review-details").hidden,true);
const before=requests.length;await run();assert.equal(requests.length,before);
console.log("TASK330_BROWSER_QUEUE_FIXTURE_PASS — restricted GET, strict response, no PII, no automatic approval, clear on deny/signout");

// Support queue lifecycle: refusal and invalid readback erase actionable data.
runInNewContext('token="fictional-token";expiresAt=Date.now()+60000;showPanel("queue-panel");',ctx);
const fixtureCase={case_id:"33333333-3333-4333-8333-333333333333",category:"GENERAL_SUPPORT",status:"OPEN",observed_at:"2026-11-24T01:00:00Z"};
const loadCases=()=>runInNewContext("loadQueue(0)",ctx);
nextResponse={ok:true,status:200,json:async()=>({cases:[fixtureCase]})};
await loadCases();
assert.equal(nodes.get("case-rows").children.length,1);
assert.equal(runInNewContext("queueVerified",ctx),true);
nextResponse={ok:false,status:503,json:async()=>({error:"unavailable"})};await loadCases();
assert.equal(nodes.get("case-rows").children.length,0);
assert.equal(runInNewContext("queueVerified",ctx),false);
nextResponse={ok:true,status:200,json:async()=>({cases:[fixtureCase,fixtureCase]})};await loadCases();
assert.equal(nodes.get("case-rows").children.length,0);
nextResponse={ok:true,status:200,json:async()=>({cases:[fixtureCase]})};await loadCases();
runInNewContext("clearQueue()",ctx);
assert.equal(nodes.get("case-rows").children.length,0);
assert.equal(runInNewContext("queueVerified",ctx),false);
console.log("TASK21_SUPPORT_QUEUE_LIFECYCLE_PASS");
