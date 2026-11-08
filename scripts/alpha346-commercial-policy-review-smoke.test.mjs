import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";

// ALPHA-346 — internal, NOT EFFECTIVE, country-neutral planning-only policy drafts.
// This is a static publication guard, not a legal review or merchant approval.
const base = "docs/launch/";
const drafts = [
  ["ALPHA-346-MANUAL-SERVICE-CUSTOMER-TERMS-REVIEW-20261107.md",
    ["NOT EFFECTIVE", "USD $50.00–$1,000.00", "One consolidated in-scope revision", "CUSTOMER_OFFER_BLOCKED", "PAYMENT_BLOCKED"]],
  ["ALPHA-346-MANUAL-SERVICE-REFUND-CANCELLATION-REVIEW-20261107.md",
    ["NOT APPROVED", "Before work starts", "After work begins", "PAYMENT_BLOCKED"]],
  ["ALPHA-346-PAID-SERVICE-PRIVACY-REVIEW-20261107.md",
    ["NOT PUBLISHED", "GoHighLevel", "Supabase", "separate public workflow", "COMMERCIAL_PRIVACY_APPROVAL_PENDING", "PAYMENT_BLOCKED"]]
];
for(const [name, markers] of drafts) {
  const file = base + name;
  assert.ok(existsSync(file), "Missing required internal commercial review draft: " + name);
  const body = readFileSync(file, "utf8");
  for(const marker of markers) assert.ok(body.toLowerCase().includes(marker.toLowerCase()), name + ": missing " + marker);
  assert.ok(!body.includes("checkout.stripe.com"), "Internal drafts must not provide checkout links");
  assert.ok(!body.includes("LIVE PAYMENTS ENABLED"), "Drafts cannot authorize payment");
  assert.ok(!body.includes("FINAL LEGALLY APPROVED"), "Drafts cannot claim legal approval");
}
const decisions = readFileSync(base + "ALPHA-346-FIRST-CUSTOMER-CAPTAIN-DECISIONS.md", "utf8");
for(const [name] of drafts) assert.ok(decisions.includes(name), "Captain must see draft " + name);
for(const item of ["REMAINDER UNSIGNED", "PAYMENT_BLOCKED", "No money collection authorized"]) {
  assert.ok(decisions.includes(item), "Captain decision sheet must retain gate " + item);
}
// Public portal allowlist intentionally excludes all internal launch contract drafts.
const builder = readFileSync("scripts/build-vercel-portal.mjs", "utf8");
assert.ok(!builder.includes("ALPHA-346-MANUAL-SERVICE-CUSTOMER-TERMS-REVIEW"), "Do not publish draft terms");
assert.ok(!builder.includes("ALPHA-346-MANUAL-SERVICE-REFUND-CANCELLATION-REVIEW"), "Do not publish draft refunds");
assert.ok(!builder.includes("ALPHA-346-PAID-SERVICE-PRIVACY-REVIEW"), "Do not publish draft privacy");
console.log("ALPHA346_COMMERCIAL_REVIEW_DRAFT_GUARD_PASS: 3 private policy drafts, approvals and no-publication checks");
