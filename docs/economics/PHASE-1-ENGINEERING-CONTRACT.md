# Economics Phase 1 — Reference Data Foundation

## Authorization

Authorized by the owner on 2026-09-23 with one-phase-at-a-time approval.
Phase 2 must not begin until Phase 1 evidence is reviewed and accepted.

## Objective

Establish durable, read-only visibility for structured market economics:

Geographic market → pricing-model vocabulary → research source →
versioned benchmark observation.

Phase 1 stores reference facts. It does **not** calculate a rate.

## Required behavior

1. Persist geographic markets as data: country + optional city/metro,
   market code, currency, active state.
2. Persist pricing-model catalog codes: CPM, CPV, flat placement, fixed
   campaign, sponsorship, host-read, CPA, CPL, CPS, and hybrid.
3. Persist approved/unapproved research sources.
4. Persist benchmark observations with market, optional source,
   metric, value/range, currency, dates, confidence, and
   `VERIFIED` / `ESTIMATED` / `INFERRED` / `UNKNOWN`.
5. Preserve observation history by adding rows. Expose no update or
   delete API.
6. Provide authenticated GET-only APIs under `/api/economics/`.
7. Provide a read-only operator workspace showing markets, models,
   observations, provenance, and an explicit “no rate engine” notice.
8. Seed six launch markets as **data** in Development. Seeded benchmark
   examples must be clearly fictional TEST fixtures, not production
   Alpha rates.
9. PostgreSQL/Supabase remains permanent truth; .NET remains
   application authority.

## API

- `GET /api/economics/markets`
- `GET /api/economics/markets/{id}`
- `GET /api/economics/pricing-models`
- `GET /api/economics/pricing-models/{id}`
- `GET /api/economics/research-sources`
- `GET /api/economics/research-sources/{id}`
- `GET /api/economics/observations`
- `GET /api/economics/observations/{id}`

No Economics POST, PUT, PATCH, or DELETE endpoint is authorized.

## Explicit exclusions

- Rate recommendations or calculators
- Quotes or compensation rules
- Creator/audience economic snapshots
- Market/industry profiles and inventory-rate benchmarks
- n8n workflows or AI research
- Wedding Planner integration
- Seeded production CPMs or city rate cards
- Duration multiplication
- Settlement, payouts, or Alpha Auto
- Changes to deterministic Bliss matching

## Acceptance criteria

- Six launch markets are rows and arbitrary future markets require no
  engine-code change.
- All ten pricing models are returned from durable storage.
- Observation responses include provenance, verification status, and
  confidence.
- A newer observation does not overwrite an older one.
- No recommendation, quote, calculation, or compensation endpoint exists.
- Operator UI is visibly read-only and does not imply that fixture
  observations are rates.
- Automated suite passes.
- API/database/browser snapshots and a screen recording are preserved
  under `docs/economics/evidence/phase1/`.
