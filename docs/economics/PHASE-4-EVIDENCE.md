# Economics Phase 4 — Evidence

## Result

Phase 4 deterministic, versioned, explainable rate recommendations are
implemented and verified. Phase 5 has not started.

## Delivered

- immutable `PricingRuleVersion` documents
- persisted low/target/high `RateRecommendation` records
- ordered recommendation factors and linked source snapshots
- canonical input snapshots and benchmark-as-of dates
- source-system + idempotency-key replay
- controlled POST plus recommendation/rule GET APIs
- operator workflow that clearly labels recommendations as not quotes
- EF migration `EconomicsPhase4Recommendations`

The bootstrap pricing rule is versioned data, not a universal rate.
Duration selects a compatible benchmark band; it is not multiplied by a
per-minute price.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **140 passed, 0 failed, 0 skipped**.

Evidence: `evidence/phase4/tests/dotnet-test.txt`.

Dedicated proofs cover:

- deterministic PHP 198 / 242 / 286 low/target/high output
- idempotent replay returns the same recommendation
- a 600-second segment is not priced as 10× a 60-second placement
- historical output remains unchanged after benchmark mutation
- missing provenance cannot yield `HIGH` confidence
- API response includes factors, sources, market, basis, rule, and reach
- recommendation relationships use restrict delete
- quote/calculation endpoints remain absent

## PostgreSQL verification

The Economics database was migrated through:

`20260923061423_EconomicsPhase4Recommendations`.

A live API write persisted recommendation
`407b9033-03f1-43d3-b918-47d54ef0ea02`, five factor rows, and one
material-input source row. Repeating the same idempotency key returned
that row with `isReplay: true`.

Database snapshot:
`evidence/phase4/database/recommendation-graph.txt`.

## API snapshots

- `evidence/phase4/api/pricing-rule-versions.json`
- `evidence/phase4/api/recommendation-detail.json`
- `evidence/phase4/api/recommendation-replay.json`

The verified recommendation is:

- low: `198 PHP`
- target: `242 PHP`
- high: `286 PHP`
- estimated impressions: `42,000`
- confidence: `MEDIUM`
- basis/market/duration: `CPM` / `PH-MNL` / `60 seconds`
- rule: `economics-rate/1.0.0`

## Browser verification

Route: `http://127.0.0.1:5106/operations#/economics`

User-facing artifacts:

- `/opt/cursor/artifacts/economics_phase4_recommendation_summary.png`
- `/opt/cursor/artifacts/economics_phase4_factors_provenance.png`
- `/opt/cursor/artifacts/economics_phase4_explainable_rate_workflow.mp4`

The recording demonstrates form input, successful generation, the full
range and confidence, five explainable factors, one source set, and the
persisted recommendation row. Independent video review found the flow
clear, legible, successful, and free of visible errors.

## Boundaries preserved

- recommendation is not a quote or approval
- no compensation or settlement calculation
- no hard-coded 20/80 split
- no AI/n8n pricing authority
- no Wedding Planner integration
- no automated campaign placement
- no matching changes
- no Phase 5 work
