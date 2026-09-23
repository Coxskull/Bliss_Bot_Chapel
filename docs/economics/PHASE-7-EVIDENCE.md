# Economics Phase 7 — Evidence

## Result

Phase 7 bounded public-research orchestration is implemented and
verified. Phase 8 has not started.

## Delivered

- durable `EconomicsResearchRun` queue records
- untrusted `EconomicsResearchCandidate` staging records
- explicit `EconomicsResearchReviewDecision` human gates
- accepted-candidate promotion into append-only
  `MarketBenchmarkObservation` records
- source provenance without implicit source approval
- idempotent queue, stage, and review operations
- disabled-by-default n8n workflow template with externalized endpoints
  and credentials
- controlled APIs and operator workflow

The lifecycle is:

`QUEUED → STAGED / AWAITING_REVIEW → PROMOTED or REJECTED → COMPLETED`

AI and n8n cannot submit `VERIFIED` or `HIGH`, write PostgreSQL
directly, calculate rates, create quotes, or settle money.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **156 passed, 0 failed, 0 skipped**.

Evidence: `evidence/phase7/tests/dotnet-test.txt`.

Dedicated proofs cover:

- bounded queue, stage, accept, and reject transitions
- accepted candidates append one observation
- rejected candidates remain auditable and append none
- `ESTIMATED` classification survives human acceptance
- AI/n8n cannot claim `VERIFIED` or `HIGH`
- all write operations replay idempotently
- n8n contains no PostgreSQL node, secrets, or active workflow
- all new foreign keys use restrict delete
- no pricing, quote, settlement, or Wedding Planner authority was added

## Live public-source verification

The Economics database was migrated through
`20260923100111_EconomicsPhase7ResearchOrchestration`.

A live World Bank Open Data response was used for the Philippines
`NY.GDP.PCAP.CD` indicator. The returned 2025 value was
`4170.72347271451 USD` (source last updated 2026-07-13).

The candidate was staged for `PH-MNL` as `MEDIUM / ESTIMATED`, reviewed
by a named human, then appended as observation
`facee366-4cff-45af-a828-34c2a6279732`. Acceptance did not silently
upgrade the observation to `VERIFIED`, and the newly linked source
remained unapproved.

Database snapshot:
`evidence/phase7/database/research-provenance-graph.txt`.

## API snapshots

- `evidence/phase7/api/research-run-queued.json`
- `evidence/phase7/api/research-candidate-staged.json`
- `evidence/phase7/api/research-run-promoted.json`
- `evidence/phase7/api/promoted-observation.json`
- `evidence/phase7/api/research-source.json`
- `evidence/phase7/api/idempotent-replays.json`

The replay snapshot proves the same run, candidate, review, and
observation identifiers are returned with `isReplay: true`; no duplicate
rows are created.

## Browser verification

Route: `http://127.0.0.1:5106/operations#/economics`

User-facing artifacts:

- `/opt/cursor/artifacts/economics_phase7_research_forms.png`
- `/opt/cursor/artifacts/economics_phase7_research_runs.png`
- `/opt/cursor/artifacts/economics_phase7_research_candidates.png`
- `/opt/cursor/artifacts/economics_phase7_promoted_observation.png`
- `/opt/cursor/artifacts/economics_phase7_public_research_human_review.mp4`

The recording demonstrates queueing, staging a World Bank candidate,
the awaiting-review gate, named human acceptance, preserved
`ESTIMATED` classification, completion, promotion, and the run/candidate
audit tables. Independent video review found the recording legible and
error-free. The durable observation itself is additionally proven by
the API and PostgreSQL snapshots above.

## Boundaries preserved

- PostgreSQL remains the permanent system of record
- .NET remains validation and promotion authority
- n8n only fetches, retries, and calls .NET
- AI output remains an untrusted candidate
- no autonomous crawling or arbitrary fetch destination in .NET
- no secret or credential is committed
- no silent source approval or benchmark verification
- no recommendation, quote, compensation, settlement, placement, or
  Wedding Planner execution
- no Phase 8 work
