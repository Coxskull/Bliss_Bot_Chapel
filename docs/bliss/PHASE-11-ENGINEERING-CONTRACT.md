# Phase 11 Engineering Contract — Operator Audit Export

## Objective

Let an operator download a bounded JSON evidence pack of one immutable Bliss
ledger without changing matching behavior or exposing secrets.

## Required behavior

1. Publish `GET /api/audit/export/{ledger}` for the existing ledgers:
   evaluations, formations, reviews, placements, ingestions, and provenance.
2. Return a JSON file with export metadata (`exportedAt`, `environment`,
   `ledger`, `requestId`, `recordCount`, `limit`, `truncated`) and summary
   records only.
3. Omit input/output snapshots, tokens, connection strings, and identity-provider
   secrets from the pack.
4. Cap each pack at 250 records and mark `truncated` when more exist.
5. Reject unknown ledgers with `400`.
6. Keep the same access policy as other directory reads.
7. Record an `AuditExported` operational event for a successful export.
8. Provide an Audit workspace control that downloads the current ledger pack and
   surfaces the request identifier.

## Explicit exclusions

- CSV, PDF, or email delivery
- full snapshot dumps of evaluation or placement input payloads
- durable object storage or log shipping
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- a seeded evaluations export contains known run identifiers and no snapshots;
- an unknown ledger is rejected;
- a successful export appears as `AuditExported` on `/api/runtime/status`;
- the frontend Audit view includes an export control;
- existing authentication, health, observability, and business tests continue to pass.
