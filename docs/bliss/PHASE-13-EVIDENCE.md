# Phase 13 Creator Case File Export Evidence

## Acceptance result

Phase 13 adds a per-creator JSON case file for identity, platforms, content,
matches, ingestions, and provenance. Snapshots and secrets are omitted.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **93 passed, 0 failed, 0 skipped**.

Dedicated tests prove a seeded Brazil creator export contains that creator and
the Brazil-approved match, omits snapshots, returns `404` for a missing creator,
records `AuditExported`, and the frontend includes the creator export path.

## Live API verification

`GET /api/audit/export/creators/11111111-1111-1111-1111-111111111113` returned
`200` with `kind=creator-case`, one match, and one platform. A missing id
returned `404`. Files are under `docs/bliss/evidence/phase13/`.

## Browser verification

The creator profile drawer **Export case file** control downloaded the pack and
toasted the request identifier.

## Safety boundaries

- Export does not ingest, form, or evaluate.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
