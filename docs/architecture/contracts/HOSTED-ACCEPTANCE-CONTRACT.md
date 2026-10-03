# Engineering Contract: Hosted acceptance reading

**Status:** the reading after Economics Phase 9 acceptance.
The process posture is stored. Hosted acceptance is not claimed.
Bliss hosted production stays unfinished.

## Objective

Store one reading of the process that is actually running. Keep hosted
acceptance unclaimed. Do not contact an identity provider. Do not run a
backup drill. Do not treat a local database as a hosted database.

## AI capability

None. No model is called.

## Deterministic capability

`HostedAcceptance` copies the posture flags already evaluated for this
process. It does not declare a deployment finished.

## Existing reusable assets

The Phase 20 production posture, the operations console, and PostgreSQL.

## Required provider

None. An identity provider is not contacted.

## Subscription impact

`FREE_SELF_HOSTED`. No new row is purchased.

## Build-versus-buy review

Phase 20 already evaluates production gates. This contract stores that
reading and refuses the hosted-acceptance claim. It does not buy
hosting, certificates, or a backup service.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend.

## Inputs

The process environment name. The posture flags already on the process.
A reading key. A request to claim hosted acceptance is refused.

## Outputs

`GET /api/operations/hosted` and `POST /api/operations/hosted`. The page
is `/operations#/hosted`.

## Cache policy

The reading row is the record. The same reading key returns that row
and does not write again. Hosted acceptance stays unclaimed.

## Failure handling

A missing environment is refused. A request to claim hosted acceptance
is refused. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The write uses the existing
operator write policy. The contract cannot mark production finished,
contact an identity provider, or send.

## Persistence

New `HostedAcceptanceReadings` rows in the existing Bliss database. One
reading key is one row. The posture evaluator is not rewritten.

## Tests

`HostedAcceptanceTests` and `HostedAcceptanceApiTests`.

## Evidence

`docs/architecture/HOSTED-ACCEPTANCE-EVIDENCE.md` and the PDF report
beside it. Phase 20 evidence stays in `docs/bliss/PHASE-20-EVIDENCE.md`.

## Acceptance criteria

- The stored reading copies the process environment and posture flags.
- Hosted acceptance claimed stays no.
- Identity contacted stays no. A backup drill stays no.
- Satisfied gates still leave hosted acceptance unclaimed.
- The same key does not write a second row.
- A request to claim hosted acceptance is refused.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

A dedicated hosted acceptance database. Organizational OIDC tokens.
A hosted backup and restore drill. Production capacity. A live send.
Crawlers. A new subscription.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The reading is the posture already
evaluated for this process.

Can we reuse something? Yes. The Phase 20 posture.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
