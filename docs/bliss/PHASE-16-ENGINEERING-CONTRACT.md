# Phase 16 Engineering Contract — Export Pack Verification

## Objective

Let an operator recompute the SHA-256 digest of a previously downloaded audit
pack and compare it to the digest declared in that file, without storing the
upload or changing matching behavior.

## Required behavior

1. Publish `POST /api/audit/verify` for same-origin JSON packs.
2. Recompute the Phase 15 domain digest (ledger `records` or case-file
   collections). Ignore export timestamps and request identifiers.
3. Return `packKind`, `declaredSha256`, `computedSha256`, and `matched`.
4. Return `200` for a well-formed pack even when the digest does not match.
5. Return `400` for invalid JSON, missing `contentSha256`, or an unsupported
   pack shape.
6. Cap the request body at 512 KB and do not persist the uploaded pack.
7. Record an `AuditVerified` operational event for a well-formed verification.
8. Provide a **Verify pack** control on the Audit workspace.
9. Keep the same access policy as other directory reads.

## Explicit exclusions

- KMS, signed URLs, or detached signature files
- durable object storage or log shipping
- PDF, CSV, or email delivery
- APM or new identity providers
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- an exported evaluations pack verifies as matched;
- match, creator, and campaign case files verify as matched;
- a tampered pack returns `matched: false` with distinct declared/computed
  digests;
- invalid or unsupported packs return `400`;
- a successful verification appears as `AuditVerified` on `/api/runtime/status`;
- the frontend Audit workspace includes **Verify pack**;
- existing tests continue to pass.
