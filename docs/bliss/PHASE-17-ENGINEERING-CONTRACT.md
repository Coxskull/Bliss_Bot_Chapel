# Phase 17 Engineering Contract — Verification Receipts

## Objective

Return a same-origin verification receipt after an operator checks an audit
pack, and show a short non-secret detail on the Status operational event log,
without storing the uploaded pack.

## Required behavior

1. Successful `POST /api/audit/verify` responses include `requestId` and
   `verifiedAt` in addition to pack kind and digest fields.
2. Record `AuditVerified` with a bounded detail string of pack kind, matched
   or mismatch, and a digest prefix.
3. Omit pack bodies, snapshots, and secrets from the receipt and event log.
4. Keep `200` for well-formed mismatches.
5. Download a `bliss-verify-*.json` receipt from the Audit **Verify pack**
   control.
6. Render event `detail` on the Status operational events table.
7. Keep the same access policy as other directory reads.

## Explicit exclusions

- KMS, signed URLs, or detached signature files
- durable object storage or log shipping
- PDF, CSV, or email delivery
- APM or new identity providers
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- a matched evaluations verify returns request id, verified time, and a
  matching Status event detail;
- a mismatched pack still returns `200` and records a `mismatch` detail;
- the frontend downloads a verification receipt and shows event detail;
- existing tests continue to pass.
