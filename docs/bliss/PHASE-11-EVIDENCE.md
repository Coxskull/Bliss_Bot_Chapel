# Phase 11 Operator Audit Export Evidence

## Acceptance result

Phase 11 adds a same-origin JSON evidence pack for one immutable ledger at a
time. Matching arithmetic is unchanged. Input/output snapshots and secrets are
not included.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **89 passed, 0 failed, 0 skipped**.

Dedicated tests prove:

- an evaluations export is a JSON file with a request id and known seeded run
  identifiers;
- `inputSnapshot` and `outputSnapshot` are absent;
- an unknown ledger returns `400`;
- a successful export records `AuditExported` on `/api/runtime/status`;
- the frontend includes **Export ledger**.

## Live API verification

A Development process on `http://127.0.0.1:5106` returned:

- `GET /api/audit/export/evaluations` → `200` application/json attachment with
  summary records only;
- `GET /api/audit/export/payments` → `400` `Unsupported ledger.`;
- subsequent `/api/runtime/status` included an `AuditExported` event.

Captured files live under `docs/bliss/evidence/phase11/`.

## Browser verification

The Audit workspace showed the evaluations ledger and **Export ledger**. Clicking
the control downloaded the pack and toasted the request identifier.

## Safety boundaries

- Export does not recompute scores or mutate records.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
