# Economics Phase 8 — Wedding Planner Handshake

## Authorization

Authorized by the owner on 2026-09-23 after Economics Phase 7 was
accepted and merged. Phase 9 must not begin until Phase 8 evidence is
reviewed and accepted.

## Objective

Allow an advertiser-owned Wedding Planner session to ask the Economics
Engine for an explainable rate recommendation for already-compatible
inventory. The Planner may present the returned range and evidence, but
it does not calculate, alter, quote, approve, place, compensate, or
settle inventory.

## Required behavior

1. Persist one durable `WeddingPlannerEconomicsRequest` for every
   Planner-to-Economics handshake.
2. Bind each request to a Wedding Planner workspace/session, advertiser,
   approved `BlissMatch`, compatible `AdInventorySlot`, geographic
   market, pricing model, and resulting `RateRecommendation`.
3. Require the match opportunity to belong to the workspace advertiser.
4. Require the match to be `APPROVED`, the slot to belong to the matched
   creator, and the slot to remain available.
5. Call the existing .NET `RateRecommendationService`; do not duplicate
   pricing calculations in Wedding Planner, JavaScript, AI, or n8n.
6. Return the persisted low/target/high range, pricing basis,
   confidence, factors, sources, and exact rule version for presentation.
7. Preserve tenant isolation: an advertiser can only request and read
   recommendations in its own workspace/session. Cross-tenant resources
   return not found.
8. Persist source-system/idempotency-key uniqueness. A retry returns the
   same handshake and recommendation.
9. Append a Wedding Planner audit event identifying the recommendation;
   never write a conversational AI message as pricing authority.
10. Keep recommendation, quote, approval, placement, compensation, and
    settlement as distinct lifecycle concepts.

## Handshake

```text
Advertiser-owned Planner session
   ↓ approved Bliss match
Compatible available inventory
   ↓ bounded request
.NET Economics recommendation service
   ↓ immutable recommendation + factors + sources
Durable Planner handshake link
   ↓ presentation only
Human-controlled quote workflow (separate)
```

## API

- `GET /api/wedding-planner/sessions/{sessionId}/economics/recommendations`
- `GET /api/wedding-planner/economics/recommendations/{requestId}`
- `POST /api/wedding-planner/sessions/{sessionId}/economics/recommendations`

The POST uses Wedding Planner ownership checks and write access. It does
not broaden direct advertiser access to Economics write endpoints.

## Explicit exclusions

- price calculation in Wedding Planner or the browser
- direct LLM/n8n pricing authority
- unapproved matches or incompatible/unavailable inventory
- automatic quote creation, revision, approval, or advertiser outcome
- campaign creation, reservation, placement, or delivery
- compensation illustration, settlement, ledger, invoice, or payout
- changes to matching scores or approvals
- public-site chat or autonomous Planner conversation
- Economics Phase 9 historical-learning tables

## Acceptance criteria

- an owned session can request an explainable recommendation through
  the handshake
- the linked recommendation uses the existing Economics rule engine
- unapproved, cross-advertiser, mismatched, and unavailable inputs fail
- cross-tenant reads do not disclose another advertiser's request
- retries return the same request and recommendation without duplicates
- factors, sources, confidence, range, and pricing rule are visible
- no quote, placement, compensation, or settlement row is created
- full suite and live PostgreSQL verification pass
- API/database/browser snapshots and a recording are preserved under
  `docs/economics/evidence/phase8/`
