# Economics Phase 6 — Compensation Policy Separation

## Authorization

Authorized by the owner on 2026-09-23 after Economics Phase 5 was
accepted and merged. Phase 7 must not begin until Phase 6 evidence is
reviewed and accepted.

## Objective

Apply an immutable, configurable compensation-policy version to an
accepted quote amount and persist an auditable **illustration** of
participant allocations without creating settlement, payout, payable,
invoice, or ledger obligations.

## Required behavior

1. Persist immutable `CompensationRuleVersion` documents with structured
   participant allocations.
2. Participant roles include `ALPHA`, `CREATOR`, and
   `OTHER_AUTHORIZED`; policy rows, not compiled constants, define their
   shares.
3. The bootstrap calculator supports percentage policies whose
   allocations must total exactly 100%.
4. Generate an illustration only from an accepted quote's exact current
   version and accepted commercial amount.
5. Persist gross amount, currency, exact quote/version, exact
   compensation-rule version, canonical input snapshot, and allocation
   lines.
6. Round monetary lines to two decimals and deterministically assign any
   rounding remainder to the final configured line so allocations equal
   the gross amount.
7. Retries use source-system + idempotency-key and return the same
   illustration.
8. Historical illustrations never change when a policy or quote changes.
9. Clearly label every result as an illustration, not settlement.
10. .NET owns calculation authority; PostgreSQL remains the permanent
    record.

## Bootstrap TEST policy

Development evidence may seed one explicitly synthetic TEST policy with
three configurable rows:

- `ALPHA`: 18%
- `CREATOR`: 72%
- `OTHER_AUTHORIZED`: 10%

This is test data, not a production policy and not a universal split.
No 20/80 rule is compiled or implied.

## API

- `GET /api/economics/compensation-rule-versions`
- `GET /api/economics/compensation-illustrations`
- `GET /api/economics/compensation-illustrations/{id}`
- `POST /api/economics/compensation-illustrations`

POST requires operator write authority, an accepted quote/version,
policy version, source system, and idempotency key.

## Explicit exclusions

- settlement, payable, payout, transfer, invoice, or payment execution
- creator wallet or balance
- tax, withholding, fee, refund, or chargeback calculation
- a universal or default 20/80 split
- modification of quote amounts or advertiser outcomes
- AI/n8n compensation authority
- Wedding Planner integration
- campaign placement or matching changes

## Acceptance criteria

- percentages are database rows linked to an immutable policy version
- invalid policies that do not total 100% cannot produce illustrations
- an unaccepted or stale quote version cannot produce an illustration
- gross amount comes from the accepted commercial outcome
- line amounts sum exactly to gross after deterministic rounding
- changing policy rows cannot alter a historical illustration
- idempotent replay returns the same illustration
- no settlement or payout API exists
- full suite and live PostgreSQL verification pass
- API/database/browser snapshots and recording are preserved under
  `docs/economics/evidence/phase6/`
