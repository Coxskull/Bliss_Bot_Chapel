# Economics Phase 9 — Historical Learning

## Authorization

Authorized by the owner on 2026-09-23 after Economics Phase 8 was
accepted and merged. This is the final phase in the current Economics
roadmap.

## Objective

Preserve Alpha's append-only commercial and delivery actuals so external
benchmarks can be compared with real Alpha economics without mutating
recommendations, quotes, placements, compensation illustrations, or
settlement records.

## Required behavior

1. Persist append-only `HistoricalPlacementEconomics` and
   `CampaignPerformanceEconomics` records.
2. A placement-economics record must link an existing planned placement
   to an accepted quote outcome, exact quote line, and exact
   `RateRecommendation`.
3. The recommendation's inventory slot must match the placement slot.
   The quote line must belong to the accepted outcome's quote version.
4. Snapshot quoted and contracted amounts, currency, recommendation
   low/target/high, external inventory-benchmark low/high, and dated
   delivery actuals.
5. Preserve impressions, views, listens, engagements, and conversions
   as nullable measurements. Unknown never becomes zero.
6. Calculate effective CPM/CPV and contracted-versus-recommendation /
   external-midpoint variances only when the required denominator and
   comparable currency are present.
7. An optional compensation illustration may be referenced and
   snapshotted for analytical comparison. It remains an illustration,
   not money owed or paid.
8. Campaign-performance records snapshot campaign-level metrics and the
   then-current Alpha placement count / contracted total when currencies
   are comparable.
9. Corrections append a new row with a `Supersedes...Id`; existing rows
   are never updated or deleted.
10. Every write is idempotent by source system and key. .NET validates
    and calculates; PostgreSQL is the permanent system of record.

## Learning flow

```text
Accepted quote + planned placement
   ↓
Append placement economics actual
   ├── quote / contracted snapshots
   ├── delivery measurements
   ├── optional compensation illustration snapshot
   └── external benchmark versus Alpha comparisons
        ↓
Append campaign performance snapshot
        ↓
Operator comparison and future model input
```

Phase 9 records evidence for later analysis. It does not automatically
change pricing rules or future recommendations.

## API

- `GET /api/economics/historical-placements`
- `GET /api/economics/historical-placements/{id}`
- `POST /api/economics/historical-placements`
- `GET /api/economics/campaign-performance`
- `GET /api/economics/campaign-performance/{id}`
- `POST /api/economics/campaign-performance`

POSTs require Economics write authority. No PUT, PATCH, or DELETE route
is authorized.

## Explicit exclusions

- recommendation, benchmark, quote, outcome, or placement mutation
- automatic pricing-rule adjustment or model training
- AI/n8n authority over calculations or permanent records
- payable, invoice, wallet, transfer, payout, or settlement creation
- treating a compensation illustration as settlement
- delivery execution or campaign lifecycle changes
- overwriting historical corrections
- Wedding Planner pricing calculations

## Acceptance criteria

- only accepted quote outcomes can anchor placement economics
- quote line, recommendation, slot, and placement relationships match
- unknown measurements remain null and negative values fail
- effective CPM/CPV and comparison percentages are reproducible
- corrections append and link without changing prior rows
- campaign performance snapshots Alpha totals without mutation
- retries return the same records without duplicates
- no upstream recommendation/quote/placement rows change
- no settlement/payable record is created
- full suite and live PostgreSQL verification pass
- API/database/browser snapshots and a recording are preserved under
  `docs/economics/evidence/phase9/`
