# Phase 15 Export Integrity Evidence

## Acceptance result

Phase 15 adds a SHA-256 digest of canonical domain JSON to existing audit
export packs. `contentSha256` in the file matches `X-Content-SHA256`. Export
timestamps and request identifiers are not part of the hashed payload.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **99 passed, 0 failed, 0 skipped**.

Dedicated tests prove:

- an evaluations pack digest equals SHA-256 of canonical `records` JSON;
- a second export of the same ledger shares the digest despite a different
  `exportedAt` / `requestId`;
- match, creator, and campaign case files include a matching header and body
  digest;
- missing and unsupported exports omit `X-Content-SHA256`;
- the frontend toast includes a `sha256:` prefix.

## Live API verification

A Development process on `http://127.0.0.1:5106` (authentication disabled,
write rate limit 2) returned:

- `GET /api/audit/export/evaluations` → `200` with `contentSha256` =
  `c79d5e91a603…` matching `X-Content-SHA256`;
- a second evaluations export reused the same digest;
- match, creator, and campaign case files each included a matching digest;
- a missing match id returned `404` without the digest header;
- `GET /api/audit/export/payments` returned `400` without the digest header.

Captured files live under `docs/bliss/evidence/phase15/`.

## Browser verification

The Audit workspace **Export ledger** control downloaded the evaluations pack
and toasted `Exported evaluations ledger [b700b828…] sha256:c79d5e91a603…`,
matching the live digest prefix.

## Safety boundaries

- Integrity hashing does not recompute scores or mutate records.
- No KMS, APM, PDF, or object storage was added.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
