# Phase 13 Engineering Contract — Creator Case File Export

## Objective

Let an operator download one creator case file: canonical identity, platforms,
content summaries, match certificates, ingestions, and provenance, without
snapshots or secrets.

## Required behavior

1. Publish `GET /api/audit/export/creators/{id}`.
2. Return `404` when the creator does not exist.
3. Return a JSON file with export metadata (`exportedAt`, `environment`,
   `kind`, `requestId`, `creatorId`) and bounded related summaries.
4. Omit input/output snapshots, tokens, connection strings, and identity-provider
   secrets.
5. Cap related collections at the existing audit export limit of 250.
6. Record an `AuditExported` operational event for a successful export.
7. Keep the same access policy as other directory reads.
8. Provide an **Export case file** control on the creator profile drawer.

## Explicit exclusions

- CSV, PDF, or email delivery
- nested match evaluation snapshots
- recomputation of scores
- durable object storage or log shipping
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- a seeded creator export includes that creator and at least one related match;
- a missing creator returns `404`;
- snapshots and secrets are absent;
- a successful export appears as `AuditExported` on `/api/runtime/status`;
- the frontend creator drawer includes an export control;
- existing tests continue to pass.
