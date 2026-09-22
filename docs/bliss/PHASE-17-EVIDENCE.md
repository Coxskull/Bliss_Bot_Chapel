# Phase 17 Verification Receipt Evidence

## Acceptance result

Phase 17 returns a verification receipt (`requestId`, `verifiedAt`, digest
fields) and records a short `AuditVerified` detail on Status. The uploaded pack
is not stored.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **104 passed, 0 failed, 0 skipped**.

Dedicated tests prove a matched evaluations verify includes correlation fields
and a Status event detail, and a mismatched pack records a `mismatch` detail.

## Live API verification

A Development process on `http://127.0.0.1:5106` returned:

- matched evaluations verify with `requestId` and `verifiedAt`;
- tampered pack `matched: false` with a distinct computed digest;
- `/api/runtime/status` events `evaluations matched c79d5e91a603` and
  `evaluations mismatch deaaff27721a`.

Captured files live under `docs/bliss/evidence/phase17/`.

## Browser verification

Audit **Verify pack** toasted `Verified evaluations sha256:c79d5e91a603…` and
downloaded a receipt. Status showed the Detail column with
`evaluations matched c79d5e91a603`.

## Safety boundaries

- Receipts do not persist the uploaded pack or include snapshots/secrets.
- No KMS, APM, PDF, or object storage was added.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
