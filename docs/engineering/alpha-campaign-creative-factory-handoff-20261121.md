# ALPHA customer campaign → creative QA → Henry Ford Factory (held mode)

**Scope:** ALPHA-GLOBAL-MEDIA Task #3B and controlled image pilot. This
handoff is a read-only, no-cost, customer-owned DRAFT preview. It is **not**
creative generation, a paid order, legal clearance, campaign execution,
Sailor assignment, or permission to change Factory STOP.

## Existing components reused

- Customer registration and verified OIDC identity.
- LIVE ACTIVE advertiser membership joined to immutable registration consent receipt.
- Existing AdvertiserProgram → AdvertiserOpportunity → Campaign draft data.
- Existing budgeted ControlledAiService image pilot: separately held; no invocation in this change.
- Existing Factory `factory-poc/creative_image_pilot_handoff.py` validates **artwork** evidence only if real model receipts and private PNGs exist.
- New Factory `factory-poc/campaign_creative_handoff.py` validates only a **brief** preview; it does not claim an image exists.

## Owner/customer flow

1. Approved advertiser signs into `/advertiser-dashboard.html` and saves an advertising draft.
2. The separately gated POST `/api/advertisers/my-campaign-drafts/creative-handoff-preview`
   accepts only `{ "campaignId": "<own-draft-uuid>" }`. The existing customer cookie,
   CSRF token, flag, ACTIVE membership, and receipt all must pass; client tenant IDs
   are ignored.
3. The customer dashboard can prepare and download an **untrusted private** JSON
   manifest with the creative brief. It must not be attached to public GitHub
   issues, sent to vendors, or treated as an authorization credential.
4. A human operator with permission to view this customer's brief may inspect a
   *local copy* using the existing Factory POC:
   `python factory-poc/campaign_creative_handoff.py --handoff <private-file.json>`
5. QA prints a receipt with the campaign ID, SHA-256 consistency hash and explicit
   HELD / NOT_REQUESTED / NOT_SENT statuses, excluding the private brief text.

## Security and commercial meaning

- `briefSha256` is SHA-256 over UTF-8 `campaignId + LF + campaignName + LF + creativeBrief`.
  A digest is **not** proof of identity, approval, consent or an issuer signature.
- Model and budget data are descriptive only. Even a valid offline receipt does
  not enable the GPT-Image project or run the model; no runtime token is used.
- `globalStop: NOT_READ_NOT_CHANGED` explicitly disclaims a live STOP readback.
- The public service remains restricted-preview; only its exact registration,
  dashboard and safe draft paths are open.
- No provider sharing consent, quotation, customer payment, real placement
  permission, creator slot authorization, paid billing approval, delivery
  schedule or Factory start action is created.
- If a draft is revoked, customer access to preview fails on the next request.
- Production feature switch `Authentication__AdvertiserCreativeHandoffPreviewRequested`
  is OFF until the reviewed code is released and explicit activation occurs.

## Independent future release gates

1. Validated live QA contract and existing customer profile continuity.
2. Independent customer-facing consent for external creative processing and
   human review, if later sharing with an AI provider is proposed.
3. OpenAI project billing permission and independently confirmed cost controls;
   owner-approved image pilot budget and private credentials.
4. Idempotent paid request ledger, real provider usage receipts, human visual QA.
5. Real authorization and live status readback for Factory dispatch and STOP;
   no branch merge or QA receipt authorizes production outreach.
6. Approved commercial quote, creator permission, fulfillment and payment procedures.

**No new service, subscriptions, paid image calls, outbound messages, customer
data movement, or Factory STOP changes are performed by this handoff.**
