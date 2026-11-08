// ALPHA-FIRST-CUSTOMER-LAUNCH-346
// Source-only, offline Stripe website/document preflight. NO Stripe API, network,
// payments, secrets, customer data or change to the factory STOP.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const read = path => readFileSync(resolve(root, path), "utf8");

export function inspectStripePreflight({ services, contact, advertiser, terms, refund, privacy, decisions, portalBuilder }) {
  const checks = Object.freeze({
    providerIdentified: services.includes("ALPHA GLOBAL MEDIA LLC") && contact.includes("ALPHA GLOBAL MEDIA LLC"),
    planningOnlyServiceDescribed: services.includes("One-Campaign Planning &amp; Coordination") && services.includes("creator outreach or contracts"),
    quotedCurrencyDisclosure: services.includes("transaction currency") && services.includes("taxes/fees"),
    customerInquiryRouteExists: services.includes('href="/join-advertiser.html"') && advertiser.includes('data-kind="advertiser"'),
    supportContactStaged: contact.includes('href="mailto:dejesuserwin1@gmail.com"'),
    responseTimeNotPromised: contact.includes("Response times are not guaranteed"),
    noLiveCheckoutClaim: services.includes("No online checkout is enabled") && contact.includes("not currently available"),
    noPaymentFormOrCardRequest: !advertiser.includes('name="card_number"') && !advertiser.includes('name="cvc"') &&
      !services.includes("https://checkout.stripe.com/") && !contact.includes("https://checkout.stripe.com/"),
    quotedAmountNotPubliclyAdvertised: !services.includes("USD $50.00–$1,000.00") && !advertiser.includes("USD $50.00–$1,000.00"),
    termsRemainDraft: terms.includes("NOT EFFECTIVE") && terms.includes("PAYMENT_BLOCKED"),
    refundRemainsDraft: refund.includes("NOT APPROVED") && refund.includes("PAYMENT_BLOCKED"),
    privacyRemainsDraft: privacy.includes("NOT PUBLISHED") && privacy.includes("PAYMENT_BLOCKED"),
    approvalsAndLiveChargeNotImplied: decisions.includes("REMAINDER UNSIGNED / NOT APPROVED") &&
      decisions.includes("PAYMENT_BLOCKED") && decisions.includes("No money collection authorized"),
    draftPoliciesExcludedFromPublicBuild: !portalBuilder.includes("ALPHA-346-MANUAL-SERVICE-CUSTOMER-TERMS-REVIEW") &&
      !portalBuilder.includes("ALPHA-346-MANUAL-SERVICE-REFUND-CANCELLATION-REVIEW") &&
      !portalBuilder.includes("ALPHA-346-PAID-SERVICE-PRIVACY-REVIEW")
  });
  const failed = Object.entries(checks).filter(([,ok]) => !ok).map(([name]) => name);
  return Object.freeze({
    directive: "ALPHA-FIRST-CUSTOMER-LAUNCH-346",
    checkType: "LOCAL_SOURCE_ONLY_NO_NETWORK",
    sourceChecks: checks,
    sourceStatus: failed.length ? "SOURCE_REPAIR_REQUIRED" : "SOURCE_CHECKS_PASSED",
    failedSourceChecks: failed,
    merchantDashboardStatus: "OWNER_REPORTED_VERIFIED_NOT_INDEPENDENTLY_API_CONFIRMED",
    actualChargesEnabled: "NOT_INDEPENDENTLY_VERIFIED",
    actualPayoutsEnabled: "NOT_INDEPENDENTLY_VERIFIED",
    supportHumanMonitoring: "NOT_VERIFIED",
    effectiveTermsRefundPrivacy: "NOT_APPROVED_OR_PUBLISHED",
    specificCustomerQuote: "NOT_APPROVED",
    securePaymentCollection: "PAYMENT_BLOCKED",
    productionPublication: "NOT_AUTHORIZED",
    stripeApiCalls: 0,
    liveCharges: 0
  });
}

export function inspectLocalSources() {
  return inspectStripePreflight({
    services: read("frontend/public/services.html"),
    contact: read("frontend/public/contact.html"),
    advertiser: read("frontend/public/join-advertiser.html"),
    terms: read("docs/launch/ALPHA-346-MANUAL-SERVICE-CUSTOMER-TERMS-REVIEW-20261107.md"),
    refund: read("docs/launch/ALPHA-346-MANUAL-SERVICE-REFUND-CANCELLATION-REVIEW-20261107.md"),
    privacy: read("docs/launch/ALPHA-346-PAID-SERVICE-PRIVACY-REVIEW-20261107.md"),
    decisions: read("docs/launch/ALPHA-346-FIRST-CUSTOMER-CAPTAIN-DECISIONS.md"),
    portalBuilder: read("scripts/build-vercel-portal.mjs")
  });
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = inspectLocalSources();
  console.log(JSON.stringify(result, null, 2));
  if (result.failedSourceChecks.length) process.exitCode = 2;
}
