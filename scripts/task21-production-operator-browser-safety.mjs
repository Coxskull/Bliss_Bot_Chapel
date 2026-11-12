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
