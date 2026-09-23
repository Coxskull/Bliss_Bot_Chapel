# Economics Phase 5 — Quotes and Negotiation History

## Authorization

Authorized by the owner on 2026-09-23 after Economics Phase 4 was
accepted and merged. Phase 6 must not begin until Phase 5 evidence is
reviewed and accepted.

## Objective

Turn one or more persisted rate recommendations into a versioned,
human-controlled commercial quote while preserving the distinction
between market-value intelligence and the amount offered to an
advertiser.

## Required behavior

1. Persist `Quote`, immutable `QuoteVersion`, `QuoteLineItem`,
   `QuoteApprovalDecision`, and `QuoteOutcome` records.
2. A quote starts as `DRAFT`. The operator explicitly supplies each
   quoted amount; recommendation targets may be displayed but are never
   silently treated as contracted values.
3. Every line item links to the exact recommendation and inventory
   context from which it was prepared.
4. Revisions insert a new quote version and line items. Existing
   versions are never edited or deleted.
5. Human approval applies to an exact current version. Approval and
   rejection record reviewer, rationale, time, source, and idempotency
   key.
6. Advertiser outcomes are `ACCEPTED`, `DECLINED`, or `NEGOTIATED`.
   `NEGOTIATED` records an outcome against the prior approved version
   and creates a new draft version containing the proposed amount.
7. Only an approved current version can receive an advertiser outcome.
   Negotiated versions require a new human approval.
8. Accepted amount is commercial history only. It does not calculate
   Alpha, creator, or participant compensation and does not settle.
9. All controlled writes are idempotent and .NET remains business
   authority.
10. Provide quote list/detail APIs and an operator demonstration
    workflow that visibly separates recommendation, quote, approval,
    and outcome.

## Lifecycle

```text
RECOMMENDATION
      ↓ explicit operator amount
DRAFT quote version
      ↓ human approval
APPROVED
      ├─ ACCEPTED → terminal commercial outcome
      ├─ DECLINED → terminal commercial outcome
      └─ NEGOTIATED → outcome + new DRAFT version → approval required
```

`REJECTED` approval leaves the quote available for a new immutable
revision. No status in this lifecycle places inventory or executes
payment.

## API

- `GET /api/economics/quotes`
- `GET /api/economics/quotes/{id}`
- `POST /api/economics/quotes`
- `POST /api/economics/quotes/{id}/versions`
- `POST /api/economics/quotes/{id}/approvals`
- `POST /api/economics/quotes/{id}/outcomes`

Create, revise, and outcome POSTs require operator write authority.
Approval POST requires reviewer authority.

## Explicit exclusions

- compensation rules, splits, commissions, or 20/80 defaults
- settlement, payout, invoice, or payment-provider integration
- automatic advertiser acceptance
- Wedding Planner integration
- campaign placement or inventory reservation
- AI/n8n quote authority
- changes to matching

## Acceptance criteria

- recommendation range and quoted amount remain distinct fields
- versions 1 and 2 coexist after revision or negotiation
- an old approved version cannot silently authorize a new version
- accepted/declined outcomes require current-version approval
- negotiation records the prior response and creates a new draft
- idempotent retries return the same quote/version/decision/outcome
- no update/delete quote API exists
- full suite and live PostgreSQL verification pass
- API/database/browser snapshots and recording are preserved under
  `docs/economics/evidence/phase5/`
