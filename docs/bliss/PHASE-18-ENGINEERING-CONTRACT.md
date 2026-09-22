# Phase 18 Engineering Contract — Last Verification and Drawer Verify

## Objective

Keep the latest audit-pack verification receipt on the Status workspace and
let operators start **Verify pack** from match, creator, and campaign drawers,
without storing the uploaded pack.

## Required behavior

1. Remember the latest well-formed `POST /api/audit/verify` receipt in process
   memory only.
2. Include that receipt as `lastVerification` on `GET /api/runtime/status`.
3. Omit pack bodies, snapshots, and secrets from the status document.
4. Render a **Last verification** panel on the Status workspace.
5. Provide **Verify pack** on match, creator, and campaign drawers using the
   existing same-origin verify control.
6. Keep the same access policy as other directory reads.

## Explicit exclusions

- KMS, signed URLs, or detached signature files
- durable object storage or log shipping
- PDF, CSV, or email delivery
- APM or new identity providers
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- before any verify, `lastVerification` is absent;
- after a match case file verifies, status returns that receipt;
- the frontend Status view includes **Last verification**;
- drawers include **Verify pack**;
- existing tests continue to pass.
