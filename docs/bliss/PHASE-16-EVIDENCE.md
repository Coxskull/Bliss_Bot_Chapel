# Phase 16 Export Pack Verification Evidence

## Acceptance result

Phase 16 lets an operator recompute the SHA-256 domain digest of a downloaded
audit pack and compare it to `contentSha256`. The uploaded file is not stored.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **102 passed, 0 failed, 0 skipped**.

Dedicated tests prove exported ledger and case-file packs verify as matched, a
tampered evaluations pack returns `matched: false`, invalid packs return `400`,
and a well-formed verification records `AuditVerified`.

## Live API verification

A Development process on `http://127.0.0.1:5106` returned:

- `POST /api/audit/verify` of an evaluations export → `matched: true` with
  digest `c79d5e91a603…`;
- the same pack with a tampered status → `matched: false` and a different
  computed digest;
- a match case file → `matched: true`;
- invalid JSON → `400`.

Captured files live under `docs/bliss/evidence/phase16/`.

## Browser verification

Audit **Verify pack** accepted the evaluations JSON file and showed
`Verified evaluations sha256:c79d5e91a603…`.

## Safety boundaries

- Verification does not persist the upload, recompute scores, or mutate records.
- No KMS, APM, PDF, or object storage was added.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
