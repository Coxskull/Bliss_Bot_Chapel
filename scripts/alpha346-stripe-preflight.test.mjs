import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { inspectLocalSources, inspectStripePreflight } from "./alpha346-stripe-preflight.mjs";

const current = inspectLocalSources();
assert.equal(current.sourceStatus, "SOURCE_CHECKS_PASSED",
  "Public source/doc safety checks must pass before considering website review: " + current.failedSourceChecks.join(", "));
assert.deepEqual(current.failedSourceChecks, []);
assert.equal(current.stripeApiCalls, 0);
assert.equal(current.liveCharges, 0);
assert.equal(current.securePaymentCollection, "PAYMENT_BLOCKED");
assert.equal(current.productionPublication, "NOT_AUTHORIZED");
assert.equal(current.supportHumanMonitoring, "NOT_VERIFIED");
assert.equal(current.actualChargesEnabled, "NOT_INDEPENDENTLY_VERIFIED");
assert.equal(current.effectiveTermsRefundPrivacy, "NOT_APPROVED_OR_PUBLISHED");

const root = "docs/launch/";
const input = {
  services: readFileSync("frontend/public/services.html", "utf8"),
  contact: readFileSync("frontend/public/contact.html", "utf8"),
  advertiser: readFileSync("frontend/public/join-advertiser.html", "utf8"),
  terms: readFileSync(root + "ALPHA-346-MANUAL-SERVICE-CUSTOMER-TERMS-REVIEW-20261107.md", "utf8"),
  refund: readFileSync(root + "ALPHA-346-MANUAL-SERVICE-REFUND-CANCELLATION-REVIEW-20261107.md", "utf8"),
  privacy: readFileSync(root + "ALPHA-346-PAID-SERVICE-PRIVACY-REVIEW-20261107.md", "utf8"),
  decisions: readFileSync(root + "ALPHA-346-FIRST-CUSTOMER-CAPTAIN-DECISIONS.md", "utf8"),
  portalBuilder: readFileSync("scripts/build-vercel-portal.mjs", "utf8")
};

function rejectChange(name, change) {
  const altered = inspectStripePreflight({...input, ...change});
  assert.equal(altered.sourceStatus, "SOURCE_REPAIR_REQUIRED", name);
  assert.ok(altered.failedSourceChecks.length > 0, name);
  assert.equal(altered.securePaymentCollection, "PAYMENT_BLOCKED", name + " cannot turn on charges");
  assert.equal(altered.stripeApiCalls, 0);
}
rejectChange("unverified support removed", {contact: input.contact.replace('href="mailto:dejesuserwin1@gmail.com"', 'href="mailto:other@example.net"')});
rejectChange("false paid order statement", {contact: input.contact.replace("not currently available", "available now")});
rejectChange("live checkout added", {services: input.services + '<a href="https://checkout.stripe.com/test">Pay</a>'});
rejectChange("draft terms treated as final", {terms: input.terms.replace("NOT EFFECTIVE", "EFFECTIVE")});
rejectChange("refund policy removed", {refund: input.refund.replace("NOT APPROVED", "APPROVED")});
rejectChange("commercial privacy published without approval", {privacy: input.privacy.replace("NOT PUBLISHED", "PUBLISHED")});
rejectChange("unapproved policy in public build", {portalBuilder: input.portalBuilder + "ALPHA-346-PAID-SERVICE-PRIVACY-REVIEW"});
rejectChange("customer payment card collection added", {advertiser: input.advertiser + '<input name="card_number">'});
assert.equal(inspectStripePreflight(input).securePaymentCollection, "PAYMENT_BLOCKED");
console.log("ALPHA346_STRIPE_SOURCE_PREFLIGHT_PASS: checked source gate, 8 negative cases, no Stripe requests/payments");
