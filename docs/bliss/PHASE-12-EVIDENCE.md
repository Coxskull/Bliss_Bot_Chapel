# Phase 12 Match Case File Export Evidence

## Acceptance result

Phase 12 adds a per-match JSON case file. Historical scores are not recomputed.
Snapshots, rule documents, and secrets are omitted.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **91 passed, 0 failed, 0 skipped**.

Dedicated tests prove:

- a seeded Brazil-approved match export contains that match id and both
  historical evaluation run identifiers;
- snapshots and rule documents are absent;
- a missing match returns `404`;
- a successful export records `AuditExported`;
- the frontend match drawer includes **Export case file**.

## Live API verification

`GET /api/audit/export/matches/77777777-7777-7777-7777-777777777776` returned
`200` with `kind=match-case`, three evaluation summaries, and three stored score
components. A random id returned `404`. Files are under
`docs/bliss/evidence/phase12/`.

## Browser verification

Opening the match certificate drawer and clicking **Export case file**
downloaded `bliss-match-*.json` and toasted the request identifier.

## Safety boundaries

- Export does not replay evaluation or change match status.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
