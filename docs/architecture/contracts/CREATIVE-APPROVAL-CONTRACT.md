# Engineering Contract: Human creative approval

**Status:** the priority contract after the marketplace handoff.
One human decision is recorded inside a Wedding Planner workspace that
is already open. Bliss hosted acceptance and Economics Phase 9 stay on
their own paths. Conversation AI stays closed.

## Objective

Let an operator or an advertiser approve or withhold one human title.
The decision stays in that workspace. Campaign ready is refused.

## AI capability

None. Six logical roles are not called. Model calls stay at 0.

## Deterministic capability

`CreativeApproval` accepts approve or withhold from a human actor. It
stores the title the person wrote. It does not generate one.

## Existing reusable assets

The Wedding Planner workspace, its conversation messages, and the
append-only audit table.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row. Nothing is purchased.

## Build-versus-buy review

The workspace and the audit table already exist. A creative department,
a model, and a price engine are not required for one human decision.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`. No price is invented.

## Maximum cost

No new spend.

## Inputs

An open workspace. A human title. The decision approve or withhold. The
actor `OPERATOR` or `ADVERTISER`. An idempotency key.

## Outputs

`GET /api/operations/creative` and `POST /api/operations/creative`. The
page is `/operations#/creative`.

## Cache policy

The decision row is the record. The same workspace and idempotency key
return that row and do not write a second audit.

## Failure handling

A missing workspace is refused. A discovered business is not opened. A
system actor is refused. Campaign ready and won are refused. A blank
title is refused. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The decision write uses the
existing operator write policy. The contract cannot call a model, write
a match, or invent a price.

## Persistence

New `CreativeApprovals` rows in the existing Bliss database. One
workspace and idempotency key is one row. One `CREATIVE_DECIDED` audit
event is appended. Conversation messages stay on their workspace.

## Tests

`CreativeApprovalTests` and `CreativeApprovalApiTests`.

## Evidence

`docs/architecture/CREATIVE-APPROVAL-EVIDENCE.md` and the PDF report
beside it.

## Acceptance criteria

- An operator approval is `HUMAN_APPROVED` and not campaign ready.
- A withhold stays `WITHHELD`.
- A system actor is refused. A model is not called.
- Campaign ready is refused.
- The same key does not write a second row or a second audit.
- Another workspace does not show the decision, the audit, or the
  conversation.
- A discovered business is not opened.
- No price is invented. No match is written.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Conversation AI. Six model calls. Color Intelligence. The Curator.
Opening the planner for a discovered business. An invented price.
A match write. Campaign ready. A live send. Hosted Bliss acceptance.
Economics Phase 9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The decision is a stored human title
and an audit row.

Can we reuse something? Yes. The open workspace, the conversation, and
the audit table.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
