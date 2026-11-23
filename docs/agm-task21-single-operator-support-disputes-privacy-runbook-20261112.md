# ALPHA GLOBAL MEDIA — Task #21 single-operator support, privacy and disputes runbook

**Interim operating procedure** · November 12, 2026 · Internal use only

This operational procedure governs the currently approved **one human primary operator** under Task #21. It is **not** a customer contract, a promise of 24/7 staffing, legal advice, a substitute for applicable law, a refund authorization, or a claim that the production incident dispatcher has been tested with a real high-risk case. Any customer-facing refund right must come from the signed service agreement and law, not this memo.

## Current permitted operational mode

- Only the existing public Support & Disputes page at `/support.html`, owner-designated business mailbox and consented privacy-request form provide customer-facing contact. Email intake cases and privacy-form cases enter private metadata-only tables under the one verified operator's restricted portal.
- The operator signs in with their **own** production Supabase Auth identity and TOTP MFA. Case listing and exclusive `OPEN → IN_REVIEW` claims are the only supported in-app staff actions. Customer names, email bodies, provider message IDs and passwords must not be copied to GitHub, reports or the public operator UI.
- No backup operator is currently designated. There is **no** 24-hour/on-call staffing commitment and no guaranteed response-time SLA. Unattended items remain queued for later manual review.
- No live payment collection, automatic refunds/credits, automatic case closure, mass contact, campaign activation or outreach is authorized by this procedure. Existing factory STOP remains unchanged.

## Intake and triage

1. **General, creator and advertiser inquiries:** Review the original inquiry receipt and the private case metadata. Do not assume that an email creates a case unless the existing hourly support-inbox watch actually records its Gmail provider ID.
2. **Privacy:** The existing consented `/privacy-request.html` form creates a protected privacy case with only a source UUID, category and timestamp. Verify requester identity and the specific right/request before granting access, correction, withdrawal or deletion. Do not request passwords, MFA codes, government identification or financial data by ordinary email. If identity or authority is uncertain, hold rather than disclose or delete. Consult applicable retention obligations before changing records.
3. **Security:** Treat a genuine security support email as a critical private incident and review promptly **when available**. The database-generated production incident queue and hourly single-operator agent are for internal escalation only. Email provider acceptance is not human acknowledgment; a distinct verified human reply must be logged before marking ACK. If there is immediate danger or a serious live compromise, follow appropriate independent emergency/security escalation without claiming the unstaffed mailbox is emergency response.
4. **Billing, refund and failed service delivery:** Check the genuine order, agreed scope, payment-provider transaction and actual delivery facts through their authorized source, not a customer allegation alone. Only the owner can approve any refund or cancellation subject to the signed terms and applicable law. The portal does not collect money or issue refunds. Never invent a promised remedy, processing time or resolved outcome.
5. **HighLevel CRM consent and opt-out:** Distinguish one-time inquiry contact consent from optional marketing consent. Where current consent is absent/withdrawn or marketing opt-in is false, automatic GHL contact transport and all marketing follow-up remain **on hold** under the server/database gates; the inquiry remains in Supabase for manual assistance. An earlier remote GHL contact ID or HTTP201 does not prove consent is currently valid or provider DND is set. For previously delivered contacts, verify remote suppression and CRM identity in the intended HighLevel sub-account before any new outreach; record evidence in the private review ledger, not as customer text in GitHub.

## Human handling and acknowledgment

- For a queued case, the primary operator signs in with fresh AAL2 MFA, reviews only the authorized source in the proper provider, and claims the case once. Claiming means `IN_REVIEW`, not customer notification, resolution, refund, legal decision or human emergency ACK.
- The incident alert dispatcher is **hourly**, may be delayed, and sends at most one internal notification per run, with no automatic retry after an uncertain send. A real provider message ID may produce `PROVIDER_ACCEPTED`. The operator must separately reply in the same thread with the precise ACK marker; only a later distinct Gmail reply ID/timestamp permits `HUMAN_ACKNOWLEDGED`.
- If no actual case or human reply exists, leave the queue/ACK empty. Never insert fictional production customer cases, replay stale messages or fake a human acknowledgment.
- Record only task ID, coarse category, current state, timestamps and sanitized evidence links in the existing Daily Operations workbook. Keep private facts in the source systems. Follow the configured 90-day retention jobs; do not defeat retention through non-cascading audit locks.

## Acceptance boundaries still independent

- **Production notification wiring:** metadata outbox, provider receipt and hourly agent can be accepted as deployed; a true **real incident → provider → operator ACK** cannot pass until a genuine case and reply exist.
- **CRM:** local opt-out enforcement can pass offline and via source readback. Actual HighLevel DND/opt-out status for the five historical remote contacts requires real provider-level suppression/readback; do not mark remotely verified without evidence.
- **Refund/privacy policy:** the above is conservative interim internal procedure. The owner must approve any customer-specific remedy or binding commercial/refund terms before they take effect.

No new subscriptions, infrastructure, outreach campaign, secondary staff member, secrets disclosure or protected-data transfer are authorized by this runbook.
