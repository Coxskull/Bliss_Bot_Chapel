# Economics Phase 2 — Evidence

## Result

Phase 2 creator audience/performance snapshot history is implemented
and verified. Phase 3 has not started.

## Delivered

- append-only `CreatorAudienceSnapshots`
- append-only `CreatorPerformanceSnapshots`
- creator, market, content, and research-source restrict-delete links
- nullable unknown measurements
- creator-wide and content-specific performance history
- GET-only APIs with optional `creatorId` filtering
- read-only Audience snapshots and Performance snapshots operator tabs
- EF migration `EconomicsPhase2CreatorSnapshots`

All Development rows are fictional TEST fixtures. Metrics are future
recommendation inputs, not rates.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **130 passed, 0 failed, 0 skipped**.

Evidence: `evidence/phase2/tests/dotnet-test.txt`.

Dedicated proofs cover:

- two historical audience snapshots coexist for one creator
- creator-wide and content-specific performance snapshots coexist
- repeated seeding is idempotent
- unknown gender/retention stays null, not zero
- API creator filtering, metrics, source, confidence, and verification
- snapshot POST routes are unavailable
- all new FKs use restrict delete
- frontend contains both read-only history views

## PostgreSQL verification

The existing dedicated Economics database was migrated through:

`20260923050745_EconomicsPhase2CreatorSnapshots`.

Both snapshot endpoints returned `200` against Npgsql/PostgreSQL.

Database snapshot:
`evidence/phase2/database/creator-snapshots.txt`.

## API snapshots

- `evidence/phase2/api/audience-snapshots.json`
- `evidence/phase2/api/performance-snapshots.json`

The latest audience row demonstrates 42,000 subscribers, women 68%,
men unknown, PH-MNL, `ESTIMATED`, and `MEDIUM`. The performance history
demonstrates one creator-wide and one content-specific row; retention
is unknown on the older row and 58% on the newer row.

## Browser verification

Route: `http://127.0.0.1:5106/operations#/economics`

Snapshots:

- `evidence/phase2/browser/audience-snapshots.png`
- `evidence/phase2/browser/performance-snapshots.png`

User-facing recording:
`/opt/cursor/artifacts/economics_phase2_creator_snapshot_history.mp4`.

The recording demonstrates the Phase 2 guardrail, versioned audience
history, versioned performance history, explicit UNKNOWN values,
provenance/confidence, creator filtering, and read-only navigation.

## Boundaries preserved

- no rate recommendation or calculation
- no market/industry economic profiles
- no inventory-rate benchmark
- no quote or compensation
- no n8n/AI research
- no Wedding Planner coupling
- no matching changes
- no Phase 3 work
