# ALPHA-346 — Stripe starter package handoff

Recorded 2026-11-25. Owner reports creation of **Alpha Global Media — Digital Advertising Starter Package**, USD 149 one-time payment, with Stripe Payment Link https://buy.stripe.com/14A00j8fN6El79Q8Df0Jq00. This is an owner-reported commercial milestone, not independent verification of merchant, product, price, taxes, checkout or collected funds. Connected Stripe MCP still exposes sandbox only.

## Operational handoff
- Do not duplicate the Stripe product. Once live account permission is available, retrieve and match merchant, Payment Link, product and price identifiers, active status, currency, amount and tax behavior.
- Confirm whether the package is the existing five-output Campaign Planning & Coordination service or a different scope. Do not silently promise finished advertising, creator placements, delivery dates or results. Confirm paid terms, cancellation/refunds, privacy and support before issuing the customer-specific offer.
- Match an accepted quotation to the Stripe checkout client_reference_id (non-secret order reference), successful live Checkout Session and PaymentIntent. Check actual product/price, amount received, customer/order match, refund/dispute status and duplicate use in the merchant account. Do not accept screenshots or redirects as sole proof of payment.
- The updated private order ledger requires matching quoteId on all events, checkoutReference equal to the quote ID, cs_live_ and pi_ identifiers, exact accepted amount, USD and successful payment status. It still labels evidence operator-reported, not independently verified; it cannot prevent duplicate use across separately stored files. Keep receipts private; no customer details in GitHub.
- Fulfill only the accepted order after authenticated merchant-side verification; continue existing manual production, review, delivery and actual expense workflow.

Public services page remains inquiry-only until actual package scope and checkout release are confirmed. No charge, public link publication, new subscription, automated outreach or Factory STOP change performed. Google Play Console verification is a separate game/mobile release workstream and is not required for the existing web advertising service.

Stripe reference: https://docs.stripe.com/checkout/fulfillment
