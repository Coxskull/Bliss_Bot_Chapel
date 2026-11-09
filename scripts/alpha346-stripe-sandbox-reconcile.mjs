import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

// Contract for the EXISTING ALPHA planning-only sandbox product and payment link.
// No Stripe credentials, provider calls, DB writes, outreach, or fulfillment.
const expected = Object.freeze({
  linkId: 'plink_1UZkkuH23maIy3rzRSbuvCtT',
  productId: 'prod_VawZ1nLovQnzq6',
  priceId: 'price_1UZkfyH23maIy3rzzhYzRWFD',
  amountMinor: 14900,
  currency: 'usd'
});
function requireCondition(ok, reason) {
  if (!ok) throw Error('SANDBOX_RECONCILIATION_REJECTED:' + reason);
}
function isId(value, prefix) {
  return typeof value === 'string' && value.startsWith(prefix) && value.length > prefix.length + 6;
}
function eventMatches(events, type, id) {
  return Array.isArray(events) && events.some(e =>
    e?.type === type && e?.livemode === false && e?.resource_id === id &&
    isId(e?.id, 'evt_'));
}
export function reconcilePlanningSandboxEvidence(snapshot) {
  requireCondition(snapshot?.environment === 'STRIPE_SANDBOX_ONLY', 'invalid_environment');
  requireCondition(snapshot?.origin === 'STRIPE_API_READ_ONLY', 'unverified_evidence_source');
  const {link, product, price, session, lineItems, paymentIntent, failedPaymentIntent, events} = snapshot;
  requireCondition(link?.id === expected.linkId && link?.livemode === false &&
    link?.active === true, 'link_identity_or_state');
  requireCondition(link?.metadata?.environment === 'SANDBOX_ONLY' &&
    link?.metadata?.payment === 'TEST_MODE_ONLY' &&
    link?.metadata?.policy_status === 'DRAFT' &&
    link?.metadata?.real_customer_use === 'PROHIBITED', 'link_policy_boundary');
  requireCondition(link?.automatic_tax?.enabled === true, 'tax_not_configured');
  requireCondition(product?.id === expected.productId && product?.active === true &&
    product?.name?.startsWith('[SANDBOX ONLY] Campaign Planning & Coordination'), 'wrong_product');
  requireCondition(price?.id === expected.priceId && price?.product === product.id &&
    price?.active === true && price?.unit_amount === expected.amountMinor &&
    price?.currency === expected.currency && price?.type === 'one_time', 'wrong_price');
  requireCondition(isId(session?.id,'cs_test_') && session?.livemode === false &&
    session?.payment_link === link.id && session?.mode === 'payment' &&
    session?.status === 'complete' && session?.payment_status === 'paid', 'checkout_incomplete');
  requireCondition(session?.amount_total === expected.amountMinor &&
    session?.amount_subtotal === expected.amountMinor &&
    session?.currency === expected.currency, 'checkout_amount_mismatch');
  requireCondition(session?.automatic_tax?.enabled === true &&
    session?.automatic_tax?.status === 'complete' &&
    Number.isSafeInteger(session?.total_details?.amount_tax) &&
    session?.total_details?.amount_tax >= 0, 'tax_readback_incomplete');
  requireCondition(session?.metadata?.environment === 'SANDBOX_ONLY' &&
    session?.metadata?.payment === 'TEST_MODE_ONLY' &&
    session?.metadata?.real_customer_use === 'PROHIBITED', 'checkout_policy_boundary');
  requireCondition(Array.isArray(lineItems) && lineItems.length === 1 &&
    lineItems[0]?.price === price.id && lineItems[0]?.quantity === 1 &&
    lineItems[0]?.amount_total === expected.amountMinor &&
    lineItems[0]?.currency === expected.currency, 'line_item_mismatch');
  requireCondition(isId(paymentIntent?.id,'pi_') &&
    session?.payment_intent === paymentIntent.id &&
    paymentIntent?.livemode === false && paymentIntent?.status === 'succeeded' &&
    paymentIntent?.amount === expected.amountMinor &&
    paymentIntent?.amount_received === expected.amountMinor &&
    paymentIntent?.currency === expected.currency, 'payment_not_confirmed');
  requireCondition(eventMatches(events,'checkout.session.completed',session.id) &&
    eventMatches(events,'payment_intent.succeeded',paymentIntent.id), 'success_events_missing');
  requireCondition(isId(failedPaymentIntent?.id,'pi_') &&
    failedPaymentIntent.id !== paymentIntent.id &&
    failedPaymentIntent?.livemode === false &&
    failedPaymentIntent?.status === 'requires_payment_method' &&
    failedPaymentIntent?.amount === expected.amountMinor &&
    failedPaymentIntent?.amount_received === 0 &&
    failedPaymentIntent?.currency === expected.currency &&
    failedPaymentIntent?.last_payment_error?.decline_code === 'insufficient_funds' &&
    eventMatches(events,'payment_intent.payment_failed',failedPaymentIntent.id),
    'failure_case_missing');
  return Object.freeze({
    status: 'SANDBOX_READBACK_VERIFIED',
    paidTestAmountMinor: 14900,
    currency: 'USD',
    successfulSandboxCheckout: true,
    declinedSandboxPayment: true,
    checkoutTaxCalculated: true,
    webhookDeliveryVerified: false,
    productionPaymentAuthorized: false,
    realRevenueRecognized: false,
    customerOutreachPerformed: false,
    fulfillmentAuthorized: false,
    requiresManualReview: true
  });
}
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const fixture = new URL('./fixtures/alpha346-stripe-sandbox-readback-20261109.json', import.meta.url);
  try { console.log(JSON.stringify(reconcilePlanningSandboxEvidence(JSON.parse(await readFile(fixture,'utf8'))))); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
