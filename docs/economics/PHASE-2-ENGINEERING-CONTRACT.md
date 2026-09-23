# Economics Phase 2 — Creator Audience & Performance Snapshots

## Authorization

Authorized by the owner on 2026-09-23 after Economics Phase 1 was
accepted and merged. Phase 3 must not begin until Phase 2 evidence is
reviewed and accepted.

## Objective

Persist versioned creator/audience measurements for future economic
analysis without turning follower count, duration, or any single metric
into a rate.

Creator → audience snapshot history + performance snapshot history →
future recommendation inputs.

Phase 2 stores observations. It does **not** calculate value.

## Required behavior

1. Persist append-only `CreatorAudienceSnapshot` rows with:
   creator, capture time, subscribers, age range, gender percentages,
   language, geography/market, source, confidence, and verification.
2. Persist append-only `CreatorPerformanceSnapshot` rows with:
   creator, optional content item, platform/format, average/daily/
   weekly/monthly views or listens, historical reach, engagement,
   retention, publishing frequency, source, confidence, verification.
3. Keep unknown values nullable. Unknown is not zero.
4. Link snapshots to existing creators and approved/unapproved
   `ResearchSource` rows with restrict-delete FKs.
5. Preserve history: newer snapshots never overwrite older snapshots.
6. Provide authenticated GET-only APIs and optional creator filters.
7. Extend the read-only Economics operator workspace to inspect both
   snapshot types and provenance.
8. Seed clearly fictional Development TEST snapshots only.

## API

- `GET /api/economics/audience-snapshots`
- `GET /api/economics/audience-snapshots/{id}`
- `GET /api/economics/performance-snapshots`
- `GET /api/economics/performance-snapshots/{id}`

List APIs accept optional `creatorId`. No snapshot write API is
authorized.

## Explicit exclusions

- Rate recommendations, ranges, or calculations
- Market and industry economic profiles
- Inventory-rate benchmarks or exchange rates
- Quotes or compensation
- AI/n8n research ingestion
- Wedding Planner integration
- Duration arithmetic
- Settlement or Alpha Auto
- Changes to matching

## Acceptance criteria

- A creator can retain multiple audience and performance snapshots.
- A content-specific performance snapshot can coexist with a
  creator-wide snapshot.
- Null retention or demographic values remain null.
- API responses include source, confidence, verification, and capture
  time.
- No recommendation/quote/snapshot-write endpoint exists.
- Operator UI visibly states these metrics are inputs, not prices.
- Full automated suite passes.
- API/database/browser snapshots and a screen recording are preserved
  under `docs/economics/evidence/phase2/`.
