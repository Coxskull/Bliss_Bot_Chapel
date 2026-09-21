# Phase 10 Operator Observability Evidence

## Acceptance result

Phase 10 gives operators a same-origin Status workspace for process health,
database readiness, request correlation, write-quota policy, and a bounded
in-memory operational event log. Matching, review, placement, and identity
behavior are unchanged. Secrets, tokens, and identity-provider credentials are
not displayed.

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **87 passed, 0 failed, 0 skipped**.

Dedicated tests prove:

- generated `X-Request-Id` values are GUIDs;
- a valid incoming `X-Request-Id` is echoed;
- an invalid incoming identifier is replaced;
- the write-quota probe returns `429` with `Retry-After` after the configured
  permit count;
- a `RateLimited` event appears on `GET /api/runtime/status`;
- the frontend includes a Status workspace.

## Live API verification

A Development process listened on `http://127.0.0.1:5106` with authentication
disabled and `Runtime:WriteRateLimitPermitLimit=2`. It used the local controlled
test database. Connection strings and passwords are not stored in this evidence
tree.

Observed results:

- `/health/live` returned `200`;
- `/health/ready` returned `200` with a healthy database check;
- `/api/creators` returned `200`;
- `/api/runtime/status` reported `processStatus=Healthy` and
  `databaseStatus=Healthy`;
- a generated response included `X-Request-Id: 04206ebd-ba9b-485d-ba5a-65705b49f8d1`;
- `X-Request-Id: aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee` was echoed unchanged;
- `X-Request-Id: not-a-guid` was replaced with a new GUID;
- `POST /api/runtime/throttle-check` returned `204`, then `204`, then `429` with
  `Retry-After: 60`;
- the subsequent status document included a `RateLimited` event correlated to
  request `227b879b-b509-4f0d-ba1f-96a43214461e`.

Captured files:

- `docs/bliss/evidence/phase10/tests/dotnet-test.txt`
- `docs/bliss/evidence/phase10/tests/phase10.trx`
- `docs/bliss/evidence/phase10/api/runtime-status.json`
- `docs/bliss/evidence/phase10/api/runtime-status-after-throttle.json`
- `docs/bliss/evidence/phase10/api/request-id-generated.hdr`
- `docs/bliss/evidence/phase10/api/request-id-echo.hdr`
- `docs/bliss/evidence/phase10/api/request-id-invalid.hdr`
- `docs/bliss/evidence/phase10/api/throttle-1.hdr` through `throttle-3.hdr`
- `docs/bliss/evidence/phase10/api/health-live.json`
- `docs/bliss/evidence/phase10/api/health-ready.json`

## Browser verification

The operations frontend at `http://127.0.0.1:5106/#/status` rendered with the
API connected:

- process and database health cards reported Healthy;
- last request identifier was a GUID;
- write throttle policy showed 2 writes / 60 seconds;
- **Verify write quota** recorded a `RateLimited` operational event for
  `POST /api/runtime/throttle-check` with status `429`;
- a toast confirmed the quota was written to the in-memory event log.

Screen recording: Status workspace, click Verify write quota, 429 event and toast.

## Safety boundaries

- Request bodies, passwords, and identity-provider secrets are not persisted in
  the operational event log.
- Health probes remain anonymous; the status document uses the same access
  policy as the rest of the API.
- Bliss remains separate from Alpha Auto.
- Deterministic rules remain the scoring authority.
