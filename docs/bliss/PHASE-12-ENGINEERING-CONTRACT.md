# Phase 12 Engineering Contract — Match Case File Export

## Objective

Let an operator download one BlissMatch case file: the certificate summary plus
related formation, evaluation, review, and placement summaries, without
recomputing scores or exposing snapshots or secrets.

## Required behavior

1. Publish `GET /api/audit/export/matches/{id}`.
2. Return `404` when the match does not exist.
3. Return a JSON file with export metadata (`exportedAt`, `environment`,
   `kind`, `requestId`, `matchId`) and bounded related-ledger summaries.
4. Include stored score components and eligibility checks from the certificate;
   omit input/output snapshots, rule documents, tokens, and connection strings.
5. Cap related collections at the existing audit export limit of 250.
6. Record an `AuditExported` operational event for a successful case-file export.
7. Keep the same access policy as other directory reads.
8. Provide an **Export case file** control on the match certificate drawer.

## Explicit exclusions

- CSV, PDF, or email delivery
- bulk export of every match in one file
- recomputation or alteration of historical scores
- durable object storage or log shipping
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- a seeded match export includes that match and its evaluation run identifiers;
- a missing match returns `404`;
- snapshots and secrets are absent from the pack;
- a successful export appears as `AuditExported` on `/api/runtime/status`;
- the frontend match drawer includes an export control;
- existing authentication, health, observability, and business tests continue to pass.
