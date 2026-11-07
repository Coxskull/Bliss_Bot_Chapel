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
const advertiser=read("join-advertiser.html");
assert.ok(advertiser.includes('href="/services.html"'), "advertiser inquiry links to manual service overview");
assert.ok(advertiser.includes('aria-describedby="campaign-help"'), "optional campaign details accessibility help");
assert.ok(advertiser.includes("main goal, target audience, preferred format"), "asks for useful optional planning details");
assert.ok(!/<textarea[^>]*name="description"[^>]*\\brequired\\b/.test(advertiser), "capture-first description remains optional");
const serviceSpec=readFileSync("docs/launch/ALPHA-346-ONE-CAMPAIGN-PLANNING-SERVICE.md","utf8");
assert.ok(serviceSpec.includes("NO OWNER-APPROVED QUOTE AMOUNT"), "do not claim unverified commercial pricing approval");
assert.ok(!serviceSpec.includes("Owner-approved quote band"), "unverified owner pricing band must not be asserted");
const portal=read("index.html");
assert.ok(portal.includes('href="/join-creator.html"'),"public creator home CTA");
assert.ok(portal.includes('href="/join-advertiser.html"'),"public advertiser home CTA");
assert.ok(portal.includes('href="/creator-account.html"'),"creator account entry point");
assert.ok(portal.includes('href="/creator-login.html"'),"creator-specific home login");
assert.ok(portal.includes('href="/advertiser-login.html"'),"advertiser-specific home login");
for(const file of ["services.html","contact.html"]){
  assert.ok(existsSync(base+file), file + " published");
  assert.ok(read(file).includes("ALPHA GLOBAL MEDIA LLC"), file + " identifies legal business");
}
const services=read("services.html");
assert.ok(services.includes("Advertising and creator collaboration services"), "services identified");
assert.ok(services.includes("transaction currency"), "quote currency disclosed per engagement");
assert.ok(services.includes("No online checkout is enabled"), "unavailable payments disclosed");
assert.ok(services.includes("/contact.html"), "services link to contact");
// ALPHA-346: one manual planning service reuses the public advertiser form; no checkout.
assert.ok(services.includes("One-Campaign Planning &amp; Coordination"), "single manual service visible");
assert.ok(services.includes("one consolidated round"), "included revision scope visible");
assert.ok(services.includes("Request a planning proposal through the existing advertiser inquiry form"), "existing inquiry CTA");
assert.ok(services.includes("creator outreach or contracts"), "creator commitments excluded");
assert.ok(services.includes("before payment or work begins"), "paid order is separately authorized");
assert.ok(!services.includes("https://checkout.stripe.com/"), "public service description must not open Stripe checkout");
assert.ok(!services.includes('href="/commercial-terms.html"') && !services.includes('href="/refund-cancellation.html"'), "unapproved commercial policy paths remain unpublished");

const contact=read("contact.html");
assert.ok(contact.includes('href="mailto:dejesuserwin1@gmail.com"'), "Captain-designated support contact staged");
assert.ok(contact.includes("Response times are not guaranteed"), "no unsupported support SLA");
assert.ok(!contact.includes("alpha7media@gmail.com"), "replaced prior support contact");

for(const path of ["/join-advertiser.html","/join-creator.html","/privacy-request.html"])
  assert.ok(contact.includes('href="'+path+'"'), "contact route available: "+path);
assert.ok(portal.includes('href="/contact.html"'), "public contact link visible on homepage");
assert.ok(portal.includes('href="/services.html"'), "public services link visible on homepage");
for(const file of ["join-creator.html","join-advertiser.html"]){
  assert.ok(read(file).includes("PUBLIC INQUIRY · NOT AN ACCOUNT"), "clear inquiry-only status");
  assert.ok(!read(file).includes("ALPHA INTEREST FORM · NOT YET VERIFIED"), "no misleading unverified business banner");
}
assert.ok(existsSync(base+"creator-account.html"),"creator account explainer published");
const account=read("creator-account.html");
assert.ok(account.includes("INVITATION ONLY"),"creator account is invite-only");
assert.ok(account.includes('href="https://bliss-bot-production-5eb8.up.railway.app/creator-dashboard.html"'),"dashboard link stays on Railway origin");
assert.ok(account.includes('href="https://bliss-bot-production-5eb8.up.railway.app/api/auth/login?returnUrl=%2Fcreator-dashboard.html"'),"login and OIDC callback stay on Railway origin");
assert.ok(!existsSync(base+"creator-dashboard.html"),"authenticated dashboard is not split onto Vercel");
assert.ok(!existsSync(base+"creator-dashboard.js"),"dashboard API script remains on Railway");
for(const file of ["alpha-privacy.html","alpha-terms.html","alpha-intake.css","alpha-intake.js"]){
  assert.ok(existsSync(base+file),file+" copied");
}
for(const file of ["creator-registration-preview.html","creator-registration-preview.js","oauth-consent.html","brand-intake.html"]){
  assert.ok(!existsSync(base+file),file+" must remain unpublished");
}

for (const [file, route] of [
  ["creator-login.html", "/creator-signin-preview.html"],
  ["advertiser-login.html", "/advertiser-signin-preview.html"]
]) {
  assert.ok(existsSync(base + file), file + " is publicly discoverable");
  const page = read(file);
  const expected = "href=\"https://bliss-bot-production-5eb8.up.railway.app" + route + "\"";
  assert.ok(page.includes(expected), file + " authenticates on protected Railway origin");
  assert.ok(!page.includes('<input type="password"'), file + " does not solicit passwords on Vercel");
  assert.ok(!page.includes("SUPABASE_SERVICE_ROLE_KEY"), file + " contains no service credential");
}
assert.ok(!existsSync(base + "advertiser-dashboard.html"), "private advertiser dashboard stays on Railway");
assert.ok(!existsSync(base + "advertiser-dashboard.js"), "advertiser profile client stays on Railway");

const js=read("alpha-intake.js");
assert.ok(js.includes('response.status === 201'),"don't claim success without HTTP 201");
assert.ok(js.includes('result.receipt_id'),"receipt readback required");
assert.ok(js.includes('credentials: "omit"'),"no auth cookies across origins");
assert.ok(!js.includes("SUPABASE_SERVICE_ROLE_KEY"),"no server secret in browser");
assert.ok(!js.includes("localStorage"),"no local PII cache");
console.log("Public ALPHA intake forms: 35+ privacy, routing and publication invariants PASS");

// Stripe P0: all published pages must have valid public links and no unapproved paid policies.
await import("./stripe-public-link-smoke.mjs");
// ALPHA-346 first-customer manual proposal and payment gates (offline fixtures only).
await import("./alpha346-manual-order-gates.test.mjs");
// ALPHA-346 actual four-deliverable planning kit and fictional demo proof.
await import("./alpha346-delivery-kit.test.mjs");
// Consolidated original offline proposal desk, private-only and four-document promise.
await import("./alpha346-proposal-desk-smoke.test.mjs");
