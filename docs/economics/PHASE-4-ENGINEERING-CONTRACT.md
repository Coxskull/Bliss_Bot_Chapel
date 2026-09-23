# Economics Phase 4 — Deterministic Rate Recommendations

## Authorization

Authorized by the owner on 2026-09-23 after Economics Phase 3 was
accepted and merged. Phase 5 must not begin until Phase 4 evidence is
reviewed and accepted.

## Objective

Generate reproducible, explainable **rate ranges** from structured
inventory, market, creator, and audience inputs using an immutable
pricing-rule version.

The engine recommends. It does not quote, contract, approve, settle, or
pay.

## Required behavior

1. Persist immutable `PricingRuleVersion` documents.
2. Accept creator, content/inventory, market, advertiser context,
   campaign objective, duration, and pricing basis.
3. Select compatible versioned input rows from Phases 1–3.
4. Return and persist low/target/high, currency, estimated impressions,
   benchmark-as-of date, confidence, factors, supporting sources, exact
   rule version, and a canonical input snapshot.
5. Use a matching inventory benchmark duration band. Duration must not
   be multiplied by a per-minute rate.
6. Preserve every recommendation; retries use source-system +
   idempotency-key and return the same row.
7. Confidence may be `HIGH` only when all material inputs are sourced
   and verified. Missing sources can never produce `HIGH`.
8. .NET owns calculation authority. AI, n8n, and Wedding Planner do not
   calculate the rate.
9. Provide read/detail APIs plus one controlled POST and an operator
   demonstration workflow.

## Deterministic v1 calculation

1. Choose the latest effective inventory benchmark matching market,
   pricing model, slot type, and containing the requested duration.
2. Start with the benchmark low/high range.
3. Apply a versioned reach multiplier from latest average views:
   `<10k: 0.80`, `10k–49,999: 1.00`, `50k–99,999: 1.15`, `100k+: 1.30`.
4. Apply a versioned engagement multiplier:
   `<4%: 0.90`, `4%–6.999%: 1.00`, `7%+: 1.10`.
5. Round monetary outputs to 2 decimals. Target is the midpoint of
   adjusted low/high.
6. Estimated impressions use latest average views when present.

These values are a versioned bootstrap rule, not permanent universal
truth.

## API

- `GET /api/economics/pricing-rule-versions`
- `GET /api/economics/recommendations`
- `GET /api/economics/recommendations/{id}`
- `POST /api/economics/recommendations`

POST requires operator write authority, source system, and idempotency
key.

## Explicit exclusions

- Advertiser quote or quote acceptance
- Compensation rules or 20/80 defaults
- Contracted amount, settlement, payout, payment
- AI/n8n calculation
- Wedding Planner integration
- Automated placement
- Changes to matching

## Acceptance criteria

- Result contains low/target/high and never one false-precision price.
- A 300–600 second sponsored segment is not valued as 10× a 30–60
  second mid-roll merely because of duration.
- Every material adjustment appears as a factor row.
- Recommendation links its benchmark, creator snapshots, source, and
  pricing-rule version.
- Missing/unverified provenance cannot yield `HIGH`.
- Replaying one idempotency key returns the same recommendation.
- Historical recommendation output does not change when inputs change.
- Full suite and live PostgreSQL verification pass.
- API/database/browser snapshots and recording are preserved under
  `docs/economics/evidence/phase4/`.
