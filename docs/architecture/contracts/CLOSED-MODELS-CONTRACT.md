# Engineering Contract: Closed models

**Status:** the contract after the marketplace reading.
One reading stores that seven grooming models are not configured.
Configured models stay at zero. The learning page sentence is left
in place.

## Objective

Store one closed-model reading from the stored research notes and the
conversation laboratory. Leave every grooming model unconfigured.

## AI capability

None. No model is called. A model is not named.

## Deterministic capability

`ClosedModels` reads the stored model-call count, the research-note
count, and the laboratory result. It does not configure a model and it
does not change production.

## Existing reusable assets

The research notes, the conversation laboratory, and PostgreSQL.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row is purchased.

## Build-versus-buy review

The learning page already says seven grooming models are not
configured. This contract stores that posture. It does not buy a model
or name one.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend.

## Inputs

The stored research notes and the laboratory result. A reading key. A
request to configure a model is refused. A request to record traffic
or an engagement count is refused.

## Outputs

`GET /api/operations/models` and `POST /api/operations/models`. The
page is `/operations#/models`.

## Cache policy

The reading row is the record. The same reading key returns that row
and does not write again. The stored model-call count stays as
recorded.

## Failure handling

A missing history is refused. A laboratory result that does not match
its counts is refused. Configuring a model is refused. Authorized
traffic is refused. An engagement count is refused. Delivery remains
`NOT_SENT`.

## Authorization

The reading is on the operations console. The write uses the existing
operator write policy. The contract cannot configure a model, record
traffic, change production, or send.

## Persistence

New `ClosedModelReadings` rows in the existing Bliss database. One
reading key is one row. Research notes stay as they were.

## Tests

`ClosedModelsTests` and `ClosedModelsApiTests`.

## Evidence

`docs/architecture/CLOSED-MODELS-EVIDENCE.md` and the PDF report beside
it.

## Acceptance criteria

- One reading stores configured models at zero, the measured model-call count, and the laboratory counts.
- A grooming model is not configured. A model is not named.
- The same key does not write a second row and does not raise the model-call count.
- The learning page still says seven grooming models are not configured.
- Authorized traffic stays false. Production stays unchanged.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Naming seven models. Conversation AI. An engagement count. A traffic
census. Rewriting the learning notice. A live send. Hosted Bliss
acceptance.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The reading is the stored note count
and the laboratory result.

Can we reuse something? Yes. The research notes and the conversation
laboratory.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
