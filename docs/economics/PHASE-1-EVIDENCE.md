# Economics Phase 1 — Evidence

## Result

Phase 1 reference-data foundation is implemented and verified.
Economics remains a separate bounded context and later phases remain
blocked pending owner acceptance.

## Delivered

- `GeographicMarkets`: six launch metros as rows, with no city-specific
  calculation branches
- `PricingModels`: ten durable pricing-basis vocabulary rows
- `ResearchSources`: source identity, URL, type, and approval state
- `MarketBenchmarkObservations`: append-only metric/range facts with
  retrieval/publication dates, confidence, verification, and provenance
- GET-only `/api/economics/*`
- read-only Economics operator workspace
- EF migration `EconomicsPhase1Foundation`

Development observations are explicitly synthetic TEST fixtures. They
are not Alpha rates, quotes, CPM recommendations, or market truth.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **126 passed, 0 failed, 0 skipped**.

Evidence: `evidence/phase1/tests/dotnet-test.txt`.

Dedicated proofs cover:

- six markets and ten pricing models persist idempotently
- inserting a new observation preserves the older observation
- market and pricing-model natural keys are unique
- observation source/market FKs use restrict delete
- API returns structured source, status, and confidence
- POST observations and recommendation/quote routes are unavailable
- Economics domain vocabulary contains no default 20/80 percentages
- operator frontend exposes the read-only Phase 1 boundary

## PostgreSQL verification

A dedicated local PostgreSQL database was migrated through:

`20260923045008_EconomicsPhase1Foundation`.

The relational live test found and corrected an ordering expression that
the in-memory provider accepted but PostgreSQL could not translate.
After that correction all Economics endpoints returned `200`.

Database snapshot:
`evidence/phase1/database/reference-data.txt`.

It records six market rows, ten pricing-model rows, and six observations
linked to `TEST Synthetic Market Fixture`, each `ESTIMATED` / `LOW`.

## API snapshots

- `evidence/phase1/api/markets.json`
- `evidence/phase1/api/pricing-models.json`
- `evidence/phase1/api/research-sources.json`
- `evidence/phase1/api/observations.json`

No POST, PUT, PATCH, or DELETE Economics action exists.

## Browser verification

Route: `http://127.0.0.1:5106/operations#/economics`

Snapshot:
`evidence/phase1/browser/economics-markets.png`.

The live PostgreSQL-backed workspace showed:

- the “No rate engine in Phase 1” guardrail
- 6 geographic markets
- 10 pricing models
- 6 benchmark observations
- all six launch cities as data
- tabs for pricing-model and observation provenance

User-facing recording:
`/opt/cursor/artifacts/economics_phase1_reference_data_walkthrough.mp4`.

The recording demonstrates tab navigation, persisted catalogs,
`ESTIMATED` verification, `LOW` confidence, provenance, read-only
filtering, and return to the market list.

## Boundaries preserved

- no recommendation engine or false precision
- no quotes
- no compensation rules or universal 20/80
- no dollars-per-minute calculation
- no n8n or AI research
- no Wedding Planner coupling
- no matching arithmetic change
- no settlement, payments, payouts, or Alpha Auto
- no Phase 2 work
