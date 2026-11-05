import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
const base = ".vercel-portal/";
const read = name => readFileSync(base+name,"utf8");
const forms = [
  ["join-creator.html","creator"],
  ["join-advertiser.html","advertiser"],
  ["privacy-request.html","privacy"]
];
for(const [file,kind] of forms){
  assert.ok(existsSync(base+file),file+" deployed");
  const html = read(file);
  assert.ok(html.includes('data-kind="'+kind+'"'),kind+" form");
  assert.ok(html.includes('name="processing_acknowledged" required'),"purpose acknowledgement");
  assert.ok(html.includes('name="adult_confirmed" required'),"adult confirmation");
  assert.ok(html.includes('name="marketing_opt_in"'),"optional marketing checkbox");
  assert.ok(!html.includes('name="marketing_opt_in" checked'),"never pre-check marketing");
  assert.ok(html.includes('name="middle_name"'),"honeypot");
  assert.ok(html.includes("/alpha-privacy.html"),"policy disclosed");
  assert.ok(html.includes("type=\"email\""),"email field");
}
const portal=read("index.html");
assert.ok(portal.includes('href="/join-creator.html"'),"public creator home CTA");
assert.ok(portal.includes('href="/join-advertiser.html"'),"public advertiser home CTA");
for(const file of ["alpha-privacy.html","alpha-terms.html","alpha-intake.css","alpha-intake.js"]){
  assert.ok(existsSync(base+file),file+" copied");
}
for(const file of ["creator-registration-preview.html","creator-registration-preview.js","oauth-consent.html","brand-intake.html"]){
  assert.ok(!existsSync(base+file),file+" must remain unpublished");
}
const js=read("alpha-intake.js");
assert.ok(js.includes('response.status === 201'),"don't claim success without HTTP 201");
assert.ok(js.includes('result.receipt_id'),"receipt readback required");
assert.ok(js.includes('credentials: "omit"'),"no auth cookies across origins");
assert.ok(!js.includes("SUPABASE_SERVICE_ROLE_KEY"),"no server secret in browser");
assert.ok(!js.includes("localStorage"),"no local PII cache");
console.log("Public ALPHA intake forms: 35+ privacy, routing and publication invariants PASS");
