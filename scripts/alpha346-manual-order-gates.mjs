/**
 * ALPHA-346: pure, offline preflight for ONE manual campaign-planning order.
 * No networking, secrets, invoices, sends, DB access, Stripe calls or mutation.
 * Passing preflight only authorizes human review of explicit evidence; it never
 * sends a proposal or requests money.
 */
const nonempty = v => typeof v === "string" && v.trim().length > 0;
const approved = p => p?.approved === true
  && nonempty(p?.by) && nonempty(p?.at) && nonempty(p?.evidence_ref);
const policy = p => approved(p)
  && nonempty(p?.version) && nonempty(p?.location);
// Captain-approved service QUOTING BAND only: USD 50.00–1,000.00 inclusive.
// This never approves an individual order, Stripe collection or tax treatment.
export const ALPHA346_PRICE_BAND_USD = Object.freeze({ min: 50, max: 1000, currency: "USD" });
const amount = value => typeof value === "string"
  && /^(?:0|[1-9]\d{0,9})(?:\.\d{1,2})?$/.test(value)
  && Number(value) >= ALPHA346_PRICE_BAND_USD.min
  && Number(value) <= ALPHA346_PRICE_BAND_USD.max;

export function assessManualOrder(order) {
  const o = order && typeof order === "object" ? order : {};
  const missingProposal = [];
  const requireProposal = (condition, code) => { if (!condition) missingProposal.push(code); };
  requireProposal(o.service_code === "ALPHA-346-MANUAL-PLAN", "SCOPE_MANUAL_SERVICE_ONLY");
  requireProposal(nonempty(o.proposal_ref) && nonempty(o.advertiser_legal_name), "PROPOSAL_AND_CUSTOMER_IDENTITY");
  requireProposal(nonempty(o.campaign_goal) && nonempty(o.market), "CUSTOMER_GOAL_AND_MARKET");
  requireProposal(amount(o.quote?.amount)
    && o.quote?.currency === ALPHA346_PRICE_BAND_USD.currency, "OWNER_APPROVED_EXACT_PRICE_AND_CURRENCY");
  requireProposal(nonempty(o.quote?.taxes_and_fees)
    && nonempty(o.quote?.payee_legal_entity)
    && nonempty(o.quote?.payment_milestones), "TAX_FEES_PAYEE_AND_PAYMENT_TERMS");
  requireProposal(nonempty(o.schedule?.draft_due)
    && nonempty(o.schedule?.final_due)
    && nonempty(o.schedule?.review_window), "DELIVERY_AND_REVIEW_MILESTONES");
  requireProposal(nonempty(o.deliverables_and_one_revision)
    && nonempty(o.ownership_and_usage)
    && nonempty(o.exclusions), "SCOPE_REVISIONS_RIGHTS_AND_EXCLUSIONS");
  requireProposal(o.support?.verified === true
    && nonempty(o.support?.contact)
    && nonempty(o.support?.proof_ref)
    && nonempty(o.support?.assigned_operator), "ACTUAL_MONITORED_SUPPORT_EVIDENCE");
  requireProposal(policy(o.policies?.commercial_terms), "APPROVED_COMMERCIAL_TERMS_VERSION");
  requireProposal(policy(o.policies?.refund_cancellation), "APPROVED_REFUND_CANCELLATION_VERSION");
  requireProposal(policy(o.policies?.commercial_privacy), "APPROVED_COMMERCIAL_PRIVACY_VERSION");
  requireProposal(approved(o.approvals?.captain), "CAPTAIN_SERVICE_AND_PRICE_APPROVAL");
  requireProposal(approved(o.approvals?.finance), "FINANCE_CURRENCY_TAX_AND_MERCHANT_REVIEW");
  requireProposal(approved(o.approvals?.legal), "LEGAL_POLICY_REVIEW");
  requireProposal(approved(o.approvals?.authorized_send), "SEPARATE_PROPOSAL_SEND_AUTHORIZATION");

  const missingPayment = [];
  const requirePayment = (condition, code) => { if (!condition) missingPayment.push(code); };
  requirePayment(o.customer_acceptance?.accepted === true
    && nonempty(o.customer_acceptance?.evidence_ref)
    && nonempty(o.customer_acceptance?.terms_version), "SIGNED_CUSTOMER_ORDER_AND_POLICY_ACCEPTANCE");
  requirePayment(approved(o.payment?.merchant_capability_verified), "PROCESSOR_MERCHANT_ELIGIBILITY_EVIDENCE");
  requirePayment(approved(o.payment?.live_collection_release), "SEPARATE_LIVE_PAYMENT_RELEASE");
  requirePayment(o.payment?.approved_rail === "SECURE_PROCESSOR_INVOICE", "SECURE_CUSTOMER_PAYMENT_RAIL");

  const canSendProposal = missingProposal.length === 0;
  // This is a read-only readiness verdict, NEVER an action or automatic release.
  return {
    assessment: "ALPHA-346-OFFLINE-ONLY",
    proposal_status: canSendProposal ? "READY_FOR_AUTHORIZED_HUMAN_SEND" : "BLOCKED",
    payment_status: canSendProposal && missingPayment.length === 0
      ? "READY_FOR_AUTHORIZED_HUMAN_PAYMENT_REVIEW" : "BLOCKED",
    proposal_missing: missingProposal,
    payment_missing: missingPayment,
    sent: false, invoice_created: false, payment_captured: false,
    outreach_performed: false, global_stop_changed: false,
    actual_payment_authorized_by_this_check: false
  };
}
