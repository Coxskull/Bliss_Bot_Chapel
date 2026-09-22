# Phase 19 Engineering Contract — Verification History

## Objective

Keep a bounded in-process history of well-formed audit-pack verification
receipts on the Status workspace, without storing uploaded packs.

## Required behavior

1. Remember the latest 10 well-formed `POST /api/audit/verify` receipts in
   process memory only, newest first.
2. Include that list as `recentVerifications` on `GET /api/runtime/status`.
3. Keep `lastVerification` equal to the newest receipt, or omit both when none
   exist.
4. Omit pack bodies, snapshots, and secrets from the status document.
5. Render a **Recent verifications** table on the Status workspace.
6. Keep the same access policy as other directory reads.

## Explicit exclusions

- KMS, signed URLs, or detached signature files
- durable object storage or log shipping
- PDF, CSV, or email delivery
- APM or new identity providers
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- after two well-formed verifies, status returns both receipts with the newest
  as `lastVerification` and `recentVerifications[0]`;
- history never exceeds 10 receipts;
- the frontend Status view includes **Recent verifications**;
- existing tests continue to pass.
