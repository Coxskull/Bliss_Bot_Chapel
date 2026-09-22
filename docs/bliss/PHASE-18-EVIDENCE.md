# Phase 18 Last Verification Evidence

## Acceptance result

Phase 18 keeps the latest verification receipt on Status and adds **Verify pack**
to match, creator, and campaign drawers. The uploaded pack is not stored.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **105 passed, 0 failed, 0 skipped**.

A dedicated test proves `lastVerification` is absent before verify and equals
the match-case receipt afterward, without snapshots or secrets.

## Live API verification

A Development process on `http://127.0.0.1:5106` returned:

- `/api/runtime/status` with `lastVerification: null` before verify;
- match case verify `matched: true`;
- subsequent status `lastVerification.packKind=match-case`.

Captured files live under `docs/bliss/evidence/phase18/`.

## Browser verification

The match drawer **Verify pack** control toasted
`Verified match-case sha256:abc6f31c0723…`. Status **Last verification** showed
Matched / match-case / the same digest prefix.

## Safety boundaries

- Receipts remain in-process only.
- No KMS, APM, PDF, or object storage was added.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
