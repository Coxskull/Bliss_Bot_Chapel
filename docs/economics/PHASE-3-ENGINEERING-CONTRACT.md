# Economics Phase 3 — Market, Industry, Inventory & FX Profiles

## Authorization

Authorized by the owner on 2026-09-23 after Economics Phase 2 was
accepted and merged. Phase 4 must not begin until Phase 3 evidence is
reviewed and accepted.

## Objective

Persist versioned market context and comparable inventory benchmarks for
future deterministic rate recommendations.

Phase 3 stores economic observations. It does **not** recommend a rate.

## Required behavior

1. Persist versioned market economic profiles with purchasing-power,
   competition, scarcity, source, confidence, verification, and
   effective/superseded dates.
2. Persist versioned industry/category profiles, optionally scoped to a
   market, with acquisition-cost ranges, currency, provenance, and dates.
3. Persist inventory-rate benchmark observations by market, pricing
   model, slot type, platform, content format, optional duration band,
   range, currency, source, confidence, verification, and dates.
4. Persist append-only exchange-rate observations with base/quote
   currencies, rate, observation/retrieval dates, and provenance.
5. New profile versions and observations must not overwrite history.
6. Country/city, category, platform, currency, format, slot type, and
   duration bands remain data—not engine branches.
7. Provide authenticated GET-only APIs and read-only operator views.
8. Seed only explicitly synthetic Development TEST fixtures.

## API

- `GET /api/economics/market-profiles`
- `GET /api/economics/market-profiles/{id}`
- `GET /api/economics/industry-profiles`
- `GET /api/economics/industry-profiles/{id}`
- `GET /api/economics/inventory-benchmarks`
- `GET /api/economics/inventory-benchmarks/{id}`
- `GET /api/economics/exchange-rates`
- `GET /api/economics/exchange-rates/{id}`

List APIs may filter by market or category/currency as appropriate. No
Phase 3 write API is authorized.

## Explicit exclusions

- Combining inputs into low/target/high recommendations
- Pricing-rule versions or calculation execution
- Quotes, compensation, settlement
- AI/n8n research ingestion
- Wedding Planner integration
- Treating TEST ranges or FX as production truth
- Changes to matching

## Acceptance criteria

- Two versions of one market profile coexist.
- Inventory duration is represented as an optional band, never a
  dollars-per-minute multiplier.
- Industry and inventory rows retain market/source provenance.
- Exchange observations are dated and append-only.
- APIs/UI label synthetic values and confidence clearly.
- No recommendation, quote, or write endpoint exists.
- Full automated suite passes.
- API/database/browser snapshots and recording are preserved under
  `docs/economics/evidence/phase3/`.
