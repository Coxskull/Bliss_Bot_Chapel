# ALPHA-346 — Support Mailbox Acceptance Receipt and Manual Triage SOP

**Record date:** 2026-11-07 (Philippine time)  
**Scope:** ALPHA GLOBAL MEDIA LLC manual *business inquiries* (not yet paid-order support)  
**Global STOP:** ENGAGED  
**Incremental spending:** USD 0  
**Production site:** Not changed by this procedure

## Controlled execution performed

- Connected Gmail account identified as `dejesuserwin1@gmail.com` (owner-selected operator contact).
- Sent **one internal self-test email** from this account to itself, subject `ALPHA-346 INTERNAL support mailbox verification — 2026-11-07`. The message contained no customer information, payment or support promises.
- Gmail returned sent message ID **`1a1b45e6543896e1`** and thread ID **`1a1b45e6543896e1`**, with labels `SENT` and `INBOX`.
- Independently searched the connected Gmail mailbox for this exact test subject and confirmed the same message ID has `SENT`, `INBOX` and `UNREAD` labels.
- Created Gmail label **`ALPHA / Commercial Inquiries`** and applied it to this controlled self-test message. The label exists in the actual connected account.
- **No customer recipient, unrelated inbox content, CRM write, external provider API, campaign, payment, marketing outreach, or new subscription was touched.**

**ACCEPTED:** Authenticated Gmail ownership, self-addressed message send/receipt and label organization.

**NOT TESTED:** An unrelated external sender delivering inbound email; custom-domain support alias; SMTP/SPF/DKIM/DMARC for custom domain; assigned operator monitoring shifts; human reply turnaround, complaint escalation, paid-order support availability, or mail-to-CRM automation. A self-addressed message must NEVER be presented as equivalent to external customer deliverability or support acceptance.

## Daily manual inbox triage — no automated external operations

1. Open the owner-selected Gmail account and select the `ALPHA / Commercial Inquiries` label. Review relevant incoming *business inquiries* using an authorized human operator; the label does **not** automatically collect all future mail without a separately configured Gmail filter.
2. For an inbound contact, verify consent and intended recipient; detect spam, sensitive personal data, fraudulent payment requests, and possible unrelated mail.
3. If the sender references an advertiser public-inquiry receipt, validate the receipt **privately** against approved Supabase/GHL operator views. Do not copy visitor names, email addresses, ID documents, phone numbers, confidential receipt payloads, or tokens into GitHub issues/CI artifacts.
4. Record the human disposition as `INQUIRY_RECEIVED`, `NEEDS_MORE_INFORMATION`, `INTERNAL_PROPOSAL_DRAFT`, `DECLINED`, or `PENDING_OWNER_APPROVAL` in an owner-approved private record. **Do not** silently convert a public inquiry into a paid order.
5. Use the draft response below only after checking that the original sender has a lawful, consented basis for receiving a reply. Replies are human-reviewed and separately sent, not automated by this repository.
6. For any price/contract request, use the internal `tools/commercial/alpha346-proposal-desk.html`, but keep the resulting proposal `INTERNAL REVIEW DRAFT` until the owner-specific service/finance/legal gates are satisfied.
7. Escalate concerns about privacy, refund, disputed payments, account access, identity verification, unsolicited outreach or suspicious attachments. **No paid-order support SLA or guarantee is approved here.**
8. Manual monitoring owner/coverage schedule: **NOT ASSIGNED / NOT VERIFIED**. Owner action required before representing this route as a monitored paid-client support channel.

## Human-reviewed business inquiry acknowledgement (unsent example)

> Subject: ALPHA GLOBAL MEDIA — We received your inquiry
>
> Thank you for contacting ALPHA GLOBAL MEDIA LLC. We received your inquiry about potential advertising planning or creator collaboration. A team member may review the request and ask for more details about your campaign goals and target market.
>
> Your inquiry does not create an advertising order, reserve a creator, or require payment. Any specific service scope, quotation, and timeline will be separately reviewed and agreed in writing before a commitment.
>
> Please do not email payment-card details, passwords, or identity documents.

**Do not claim a response deadline, offer actual creator inventory, give a payment link, or send this acknowledgement automatically.**

## Open gates / next business acceptance

- [x] Owner-selected Gmail account authenticated
- [x] Controlled self-addressed test visible in mailbox
- [x] Owner's Gmail account label created and applied to test
- [ ] Inbound test from a separately authorized non-self address (no real customer outreach)
- [ ] Named human monitoring owner and checked response/escalation workflow
- [ ] Approved paid-service customer support channel, terms, refund/cancellation policy, and privacy notice
- [ ] Owner-approved customer-specific price, currency, taxes and milestones
- [ ] Written customer acceptance and separate approved payment release

**Status:** `SELF_TEST_PASSED_PAID_CUSTOMER_SUPPORT_NOT_VERIFIED`. **Task #1 remains IN_PROGRESS**; synthetic recovery PASS, real recovery deferred. **Commercial payment release: NOT AUTHORIZED.**
