import assert from "node:assert/strict";
import { assessManualOrder } from "./alpha346-manual-order-gates.mjs";

// All identifiers, approval receipts, and addresses below are FICTIONAL TEST DATA.
const approval = () => ({
  approved: true, by: "FICTIONAL-APPROVER", at: "2099-01-02T00:00:00Z",
  evidence_ref: "SYNTHETIC-TEST-ONLY"
});
const policy = () => ({ ...approval(), version: "SYNTHETIC-V1", location: "fixture://policy" });
const fixture = () => ({
  service_code: "ALPHA-346-MANUAL-PLAN",
  proposal_ref: "FICTIONAL-ORDER-1",
  advertiser_legal_name: "Example Advertiser (fictional)",
  campaign_goal: "Draft a single campaign plan",
  market: "Synthetic testing market",
  quote: { amount: "200.00", currency: "USD",
    taxes_and_fees: "Synthetic quote, no charges",
    payee_legal_entity: "Fictional Test Company",
    payment_milestones: "No money movement in this test" },
  schedule: { draft_due: "2099-02-01", final_due: "2099-02-09",
    review_window: "Fictional one-week review" },
  deliverables_and_one_revision: "Four planning documents and one in-scope revision",
  ownership_and_usage: "Synthetic rights term",
  exclusions: "No creator booking, no production, no real outreach",
  support: { verified: true, contact: "test-support@example.invalid",
    proof_ref: "SYNTHETIC-MAIL-TEST", assigned_operator: "FICTIONAL-OPERATOR" },
  policies: { commercial_terms: policy(), refund_cancellation: policy(),
    commercial_privacy: policy() },
  approvals: { captain: approval(), finance: approval(), legal: approval(),
    authorized_send: approval() },
  customer_acceptance: { accepted: true, evidence_ref: "SYNTHETIC-ACCEPTANCE",
    terms_version: "SYNTHETIC-V1" },
  payment: { merchant_capability_verified: approval(),
    live_collection_release: approval(), approved_rail: "SECURE_PROCESSOR_INVOICE" }
});
const blank = assessManualOrder({});
assert.equal(blank.proposal_status, "BLOCKED");
assert.equal(blank.payment_status, "BLOCKED");
assert.ok(blank.proposal_missing.includes("ACTUAL_MONITORED_SUPPORT_EVIDENCE"));
assert.ok(blank.payment_missing.includes("SEPARATE_LIVE_PAYMENT_RELEASE"));

const example = fixture();
const passing = assessManualOrder(example);
assert.equal(passing.proposal_status, "READY_FOR_AUTHORIZED_HUMAN_SEND");
assert.equal(passing.payment_status, "READY_FOR_AUTHORIZED_HUMAN_PAYMENT_REVIEW");
for (const field of ["sent", "invoice_created", "payment_captured", "outreach_performed",
  "global_stop_changed", "actual_payment_authorized_by_this_check"]) {
  assert.equal(passing[field], false, field + " must never become true");
}

const cases = [
  [o => { o.quote.amount = "0.00"; }, "OWNER_APPROVED_EXACT_PRICE_AND_CURRENCY"],
  [o => { o.quote.amount = "200.001"; }, "OWNER_APPROVED_EXACT_PRICE_AND_CURRENCY"],
  [o => { o.quote.currency = "php"; }, "OWNER_APPROVED_EXACT_PRICE_AND_CURRENCY"],
  [o => { o.service_code = "MARKETPLACE-PLACEMENT"; }, "SCOPE_MANUAL_SERVICE_ONLY"],
  [o => { o.support.verified = false; }, "ACTUAL_MONITORED_SUPPORT_EVIDENCE"],
  [o => { o.support.proof_ref = ""; }, "ACTUAL_MONITORED_SUPPORT_EVIDENCE"],
  [o => { o.policies.commercial_terms.approved = false; }, "APPROVED_COMMERCIAL_TERMS_VERSION"],
  [o => { o.policies.refund_cancellation.location = ""; }, "APPROVED_REFUND_CANCELLATION_VERSION"],
  [o => { o.policies.commercial_privacy = null; }, "APPROVED_COMMERCIAL_PRIVACY_VERSION"],
  [o => { o.approvals.finance.approved = false; }, "FINANCE_CURRENCY_TAX_AND_MERCHANT_REVIEW"],
  [o => { o.approvals.authorized_send = null; }, "SEPARATE_PROPOSAL_SEND_AUTHORIZATION"]
];
for (const [mutate, blocker] of cases) {
  const order = fixture();
  mutate(order);
  const result = assessManualOrder(order);
  assert.equal(result.proposal_status, "BLOCKED", blocker);
  assert.equal(result.payment_status, "BLOCKED", blocker);
  assert.ok(result.proposal_missing.includes(blocker), blocker);
}

for (const [mutate, blocker] of [
  [o => { o.customer_acceptance.accepted = false; }, "SIGNED_CUSTOMER_ORDER_AND_POLICY_ACCEPTANCE"],
  [o => { o.payment.live_collection_release = null; }, "SEPARATE_LIVE_PAYMENT_RELEASE"],
  [o => { o.payment.merchant_capability_verified = null; }, "PROCESSOR_MERCHANT_ELIGIBILITY_EVIDENCE"],
  [o => { o.payment.approved_rail = "WEB_FORM_CARD_DATA"; }, "SECURE_CUSTOMER_PAYMENT_RAIL"]
]) {
  const order = fixture();
  mutate(order);
  const result = assessManualOrder(order);
  assert.equal(result.proposal_status, "READY_FOR_AUTHORIZED_HUMAN_SEND");
  assert.equal(result.payment_status, "BLOCKED");
  assert.ok(result.payment_missing.includes(blocker));
}
console.log("ALPHA-346 offline order gates PASS: no payment or proposal-send shortcuts");
