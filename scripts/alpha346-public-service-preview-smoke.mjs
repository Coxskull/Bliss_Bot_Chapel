import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";

// ALPHA-346: public-only manual advertising planning offer.
// Runs after scripts/build-vercel-portal.mjs copies the approved ALPHA public allowlist.
// This is an offline source/build assertion, NOT proof that legal, finance or payment gates passed.
const output = ".vercel-portal/";
const path = output + "services.html";
assert.ok(existsSync(path), "generated Vercel services page exists");
const page = readFileSync(path, "utf8");
for (const [label, text] of [
  ["manual offer", "One-Campaign Planning &amp; Coordination"],
  ["campaign brief", "campaign objective and audience brief"],
  ["format suitability", "suitability comparison of up to three creator-content formats"],
  ["creative direction", "creative direction and required-assets sheet"],
  ["coordination plan", "execution and coordination checklist"],
  ["revision", "one consolidated round of in-scope document revisions"],
  ["limited public inquiry", "inquiries for review"],
  ["no prices", "No online checkout is enabled"],
  ["custom quote", "individually scoped and quoted"],
  ["separate commercial contract", "not a substitute for an agreed paid service contract"]
]) assert.ok(page.includes(text), `Missing ${label} / scoped commercial disclosure`);
for(const url of ["/join-advertiser.html","/alpha-terms.html","/alpha-privacy.html","/contact.html"]) {
  assert.ok(page.includes('href="' + url + '"'), "Missing internal public route " + url);
  assert.ok(existsSync(output + url.slice(1)), "Target route unpublished " + url);
}
for(const unsafe of ["checkout.stripe.com","<form action=","paymentElement","application/json+payment"]) {
  assert.ok(!page.includes(unsafe), "Public page unexpectedly contains payment/purchase entrypoint");
}
assert.ok(!existsSync(output + "alpha346-proposal-desk.html"), "Internal proposal desk must not be published");
assert.ok(!existsSync(output + "creator-dashboard.html"), "Creator dashboard must not be published");
assert.ok(!existsSync(output + "advertiser-dashboard.html"), "Advertiser dashboard must not be published");
console.log("ALPHA346_PUBLIC_PLANNING_SERVICE_PREVIEW_PASS — honest manual service, links, exclusions and no checkout");
