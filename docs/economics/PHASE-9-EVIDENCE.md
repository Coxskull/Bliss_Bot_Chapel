# Economics Phase 9 — Evidence

## Result

Phase 9 historical learning is implemented and verified. It is pending
owner acceptance.

## Delivered

- append-only `HistoricalPlacementEconomics` placement actuals
- append-only `CampaignPerformanceEconomics` campaign snapshots
- exact links to placement, accepted quote outcome/line, recommendation,
  and optional compensation illustration
- external benchmark versus Alpha contracted-amount comparisons
- effective CPM/CPV calculations that preserve unknown denominators
- correction chains through explicit supersedes identifiers
- idempotent write and read APIs
- operator workflows and ledgers for placement and campaign actuals

Historical evidence does not mutate recommendations, quotes, placements,
or compensation illustrations. It creates no settlement or payment
authority and does not automatically feed pricing rules.

## Automated verification

```bash
dotnet test BlissBotChapel.sln --no-restore
```

Result: **172 passed, 0 failed, 0 skipped**.

Additional focused checks verified the Phase 9 API, persistence,
architecture, EF constraints, frontend, correction behavior, and
latest-measurement campaign rollups.

Evidence: `evidence/phase9/tests/dotnet-test.txt`.

## PostgreSQL verification

The live Economics database was migrated through
`20260923115315_EconomicsPhase9HistoricalLearning`.

A planned mid-roll placement was linked to its accepted `215 PHP` quote,
`198 / 242 / 286 PHP` recommendation, `180–260 PHP` external benchmark,
and optional compensation illustration. With `50,000` actual impressions
and `43,000` views, .NET recorded:

- effective CPM: `4.3`
- effective CPV: `0.005`
- contracted versus recommendation target: `-11.157025%`
- contracted versus external midpoint: `-2.272727%`
- campaign snapshot: one current placement and `215 PHP` contracted

The source quote remained `ACCEPTED`, the source placement remained
`CREATED`, and the recommendation range remained unchanged. No
settlement, payable, or payment table exists.

Database snapshot:
`evidence/phase9/database/historical-learning-graph.txt`.

## API snapshots

- `evidence/phase9/api/historical-placement-created.json`
- `evidence/phase9/api/historical-placement-replay.json`
- `evidence/phase9/api/campaign-performance-created.json`
- `evidence/phase9/api/boundary-rejection.json`

## Browser verification

Route: `http://127.0.0.1:5106/operations#/economics`

User-facing artifacts:

- `/opt/cursor/artifacts/economics_phase9_alpha_actual_placement_table.png`
- `/opt/cursor/artifacts/economics_phase9_campaign_performance_table.png`
- `/opt/cursor/artifacts/economics_phase9_historical_learning_workflow.mp4`

The recording demonstrates successful placement-actual and
campaign-performance submissions, the external-versus-Alpha comparison,
and both append-only ledgers. Independent video review confirmed both
submissions, the displayed calculations, the one-placement campaign
rollup, and the absence of visible errors or sensitive information.

## Boundaries preserved

- .NET remains calculation and validation authority
- PostgreSQL remains the permanent system of record
- unknown metrics remain null rather than becoming zero
- source recommendations, quotes, and placements remain immutable
- compensation snapshots remain illustrations, not settlement
- no automatic pricing-rule updates or model training
- no AI or n8n pricing authority
- no payable, invoice, payment, or settlement behavior
