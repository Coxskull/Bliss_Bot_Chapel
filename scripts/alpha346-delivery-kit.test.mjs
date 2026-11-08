import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";

const root = "docs/launch/";
const files = [
  ["ALPHA-346-CAMPAIGN-BRIEF-TEMPLATE.md", ["Campaign brief", "Desired campaign result", "Intended audience", "Claims/evidence owner", "actual creator"]],
  ["ALPHA-346-CHANNEL-FORMAT-MATRIX-TEMPLATE.md", ["suitability matrix", "Up-to-three-format comparison", "NO VERIFIED INVENTORY", "not a verified creator list"]],
  ["ALPHA-346-CREATIVE-DIRECTION-TEMPLATE.md", ["Creative direction", "Message hierarchy", "Source assets", "NOT INCLUDE", "actual image/video licensing"]],
  ["ALPHA-346-COORDINATION-PLAN-TEMPLATE.md", ["coordination and customer handoff", "Work breakdown", "Revision control", "Final handoff checklist", "No creator contacted"]]
];
for (const [name, required] of files) {
  assert.ok(existsSync(root + name), "Required customer planning template missing: " + name);
  const body = readFileSync(root + name, "utf8");
  for (const token of required) {
    assert.ok(body.toLowerCase().includes(token.toLowerCase()), name + " must mention " + token);
  }
  assert.ok(body.includes("Order reference:"), name + " should bind to a real Order by private reference");
  assert.ok(!body.includes("checkout.stripe.com"), "No Stripe checkout in customer templates");
  assert.ok(!body.includes("alpha7media@gmail.com"), "No retired support address");
  assert.ok(!body.includes("THIS IS LIVE"), "No source template may imply campaign production");
}
const samplePath = root + "ALPHA-346-FICTIONAL-PLANNING-DEMO.md";
assert.ok(existsSync(samplePath), "Worked fictional demonstration must exist");
const sample = readFileSync(samplePath, "utf8");
for (const label of ["FICTIONAL ONLY", "Fictional brief", "Fictional format suitability matrix",
                     "Fictional creative direction", "Fictional coordination plan",
                     "NO FINISHED AD", "NO CREATOR PARTICIPATION"]) {
  assert.ok(sample.includes(label), "sample must explicitly distinguish fictional planning: " + label);
}
const index = readFileSync(root + "ALPHA-346-DELIVERY-KIT.md", "utf8");
for (const [name] of files) {
  assert.ok(index.includes(name), "delivery kit references each promised document");
}
assert.ok(index.includes("one in-scope consolidated revision"), "revision promise documented");
assert.ok(index.includes("Global STOP"), "automated STOP left intact");
assert.ok(index.includes("ALPHA-346-SUPPORT-INBOX-SOP.md"), "delivery kit links business support handling");
const support = readFileSync(root + "ALPHA-346-SUPPORT-INBOX-SOP.md", "utf8");
assert.ok(support.includes("dejesuserwin1@gmail.com"), "selected connected Gmail identity");
assert.ok(support.includes("UNVERIFIED"), "support monitoring proof must remain pending");
assert.ok(support.includes("without contacting customers"), "support test must be internal");
assert.ok(support.includes("do not promise"), "no unsupported customer SLA or refund promise");
assert.ok(index.includes("human"), "manual completion documented");
const order = readFileSync(root + "ALPHA-346-FIRST-CUSTOMER-CAPTAIN-DECISIONS.md", "utf8");
assert.ok(order.includes("dejesuserwin1@gmail.com"), "Captain-designated contact in decision sheet");
assert.ok(order.includes("NOT YET VERIFIED"), "support operation still needs independent verification");
// Explicit Captain Stripe configuration report is evidence of selected account settings,
// not verification, bank/payout readiness, or permission to collect any payment.
const service = readFileSync(root + "ALPHA-346-ONE-CAMPAIGN-PLANNING-SERVICE.md", "utf8");
assert.ok(service.includes("USD $50.00–$1,000.00 inclusive"), "Owner-approved internal USD quoting band is documented");
assert.ok(service.includes("Stripe account country is **United States (USA), as reported by the owner"), "Owner-reported US Stripe country must not be silently forgotten");
assert.ok(service.includes("UNVERIFIED"), "Service must not treat country/currency as verified live payments");
assert.ok(order.includes("Stripe account country (owner-reported"), "Captain decision sheet separately records operator report");
assert.ok(order.includes("Stripe Dashboard account verification status: VERIFIED — owner-confirmed"), "Owner-confirmed Stripe verification should be recorded");
assert.ok(order.includes("both live payments/charges and payouts show ENABLED"), "Owner reports live charge and payout capability enabled");
assert.ok(order.includes("No live invoice/charge/payment authorization"), "Stripe enablement must never authorize a real customer charge");
console.log("ALPHA-346: all four manual deliverables, fictional example and release guard checks PASS");
