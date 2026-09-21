# Phase 14 Campaign Case File Export Evidence

## Acceptance result

Phase 14 adds a per-campaign JSON case file for identity, planned placements,
placement-run summaries, and related match certificates. Snapshots, delivery,
and payment data are omitted.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **95 passed, 0 failed, 0 skipped**.

Dedicated tests prove a seeded overlay campaign export contains that campaign
and its placements, omits snapshots, returns `404` for a missing campaign,
records `AuditExported`, and the frontend includes the campaign export path.

## Live API verification

`GET /api/audit/export/campaigns/dddddddd-dddd-dddd-dddd-ddddddddddd4` returned
`200` with `kind=campaign-case` and two planned placements. A missing id
returned `404`. Files are under `docs/bliss/evidence/phase14/`.

## Browser verification

The campaign drawer **Export case file** control downloaded the pack and toasted
the request identifier.

## Safety boundaries

- Export does not reserve, schedule, deliver, measure, or pay.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
