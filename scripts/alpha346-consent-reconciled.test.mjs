import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// One source-only release candidate: modern explicit consent + bounded manual
// planning offer. No browser submissions, network, service keys or customer data.
const read = file => readFileSync(new URL("../"+file,import.meta.url),"utf8");
const creator=read("frontend/public/join-creator.html");
const advertiser=read("frontend/public/join-advertiser.html");
const js=read("frontend/public/alpha-intake.js");
const edge=read("supabase/functions/alpha-public-intake/index.ts");
const notice=read("frontend/public/alpha-privacy.html");
const service=read("frontend/public/services.html");
const contact=read("frontend/public/contact.html");
const homepage=read("frontend/public/global-media.html");
const build=read("scripts/build-vercel-portal.mjs");
const vercel=read(".vercel-portal/join-advertiser.html");
const liveJs=read(".vercel-portal/alpha-intake.js");
const version="AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1";
assert.ok(js.includes(version), "browser submits current notice version");
assert.ok(notice.includes(version), "published privacy notice matches current consent version");
assert.ok(edge.includes("contact_consent"), "server handles explicit contact consent");
assert.ok(edge.includes("normalizeStructuredAdvertiser"), "server validates structured advertiser budget");
assert.ok(js.includes('contact_consent: checked("contact_consent")'), "client sends consent boolean");
assert.ok(js.includes('kind !== "privacy" && !draft.contact_consent'), "client fails closed on missing contact consent");
assert.ok(js.includes('credentials: "omit"'), "no cross-origin cookies");
assert.ok(js.includes('response.status === 201'), "only a recorded receipt counts as submission");
assert.ok(js.includes("result.receipt_id"), "receipt readback required");
assert.ok(js.includes("request_key: pendingKey"), "stable replay intent");
assert.ok(!js.includes("SUPABASE_SERVICE_ROLE_KEY"), "no service secret in browser");
for(const [kind,html] of [["creator",creator],["advertiser",advertiser]]) {
  assert.ok(html.includes('data-kind="'+kind+'"'),kind+" form kind");
  assert.ok(html.includes('name="contact_consent" required'),kind+" explicit contact consent is mandatory");
  assert.ok(html.includes('name="processing_acknowledged" required'),kind+" processing acknowledgment");
  assert.ok(html.includes('name="adult_confirmed" required'),kind+" adult confirmation");
  assert.ok(html.includes('name="marketing_opt_in"'),kind+" separate optional marketing consent");
  assert.ok(!html.includes('name="marketing_opt_in" checked'),kind+" no prechecked marketing");
  assert.ok(html.includes("/alpha-privacy.html"),kind+" privacy disclosure");
  assert.ok(!html.includes('type="payment"'),kind+" no payment");
}
for(const field of ["market_code","budget_amount","budget_currency","campaign_objective","preferred_media_formats","preferred_languages"]) {
  assert.ok(advertiser.includes('name="'+field+'"'),"structured advertiser input "+field);
  assert.ok(vercel.includes('name="'+field+'"'),"generated portal retains "+field);
}
assert.ok(vercel.includes('name="contact_consent" required'),"generated advertiser portal preserves explicit consent");
assert.ok(liveJs.includes(version),"generated portal ships matching intake client");
for(const part of ["discovery brief","creator/channel suitability","creative direction","coordination schedule","final planning summary"]) {
  assert.ok(advertiser.includes(part),"intake mentions five-service scope "+part);
  assert.ok(service.toLowerCase().includes(part.toLowerCase()),"service scope includes "+part);
}
assert.ok(service.includes("No online checkout is enabled"),"no website checkout");
assert.ok(contact.includes("paid-customer support service and response time are not yet verified"),"support disclosure");
assert.ok(homepage.includes('href="/join-advertiser.html"'),"homepage has advertiser CTA");
assert.ok(homepage.includes('href="/join-creator.html"'),"homepage has creator CTA");
assert.ok(!build.includes('alpha346-proposal-desk.html'),"internal proposal desk stays private");
console.log("ALPHA346_CONSENT_FIRST_CUSTOMER_STATIC_PASS: source and generated portal aligned; no HTTP POST");
