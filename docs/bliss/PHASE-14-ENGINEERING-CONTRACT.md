# Phase 14 Engineering Contract — Campaign Case File Export

## Objective

Let an operator download one campaign case file: campaign identity, planned
placements, placement-run summaries, and related match certificates, without
snapshots, delivery, or payment data.

## Required behavior

1. Publish `GET /api/audit/export/campaigns/{id}`.
2. Return `404` when the campaign does not exist.
3. Return a JSON file with export metadata (`exportedAt`, `environment`,
   `kind`, `requestId`, `campaignId`) and bounded related summaries.
4. Omit input/output snapshots, tokens, connection strings, and identity-provider
   secrets.
5. Cap related collections at the existing audit export limit of 250.
6. Record an `AuditExported` operational event for a successful export.
7. Keep the same access policy as other directory reads.
8. Provide an **Export case file** control on the campaign drawer.

## Explicit exclusions

- CSV, PDF, or email delivery
- reservation, scheduling, delivery, measurement, or payout
- recomputation of match scores
- durable object storage or log shipping
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- a seeded campaign export includes that campaign and its planned placements;
- a missing campaign returns `404`;
- snapshots and secrets are absent;
- a successful export appears as `AuditExported` on `/api/runtime/status`;
- the frontend campaign drawer includes an export control;
- existing tests continue to pass.
