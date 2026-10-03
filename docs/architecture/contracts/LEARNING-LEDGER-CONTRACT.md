# Engineering Contract: Research ledger

**Status:** the priority contract after human creative approval.
One research note is appended after the conversation laboratory
graduates. Bliss hosted acceptance and Economics Phase 9 stay on their
own paths. Conversation AI stays closed.

## Objective

Keep an append-only research note for one stored prospect. The note
does not change production, invent traffic, or call a model.

## AI capability

None. A model is not the record. Seven grooming models are not
configured.

## Deterministic capability

`LearningLedger` checks the laboratory result and appends one fixed
note. It does not rewrite a prospect.

## Existing reusable assets

The conversation laboratory, the stored prospects, and PostgreSQL.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row. Nothing is purchased.

## Build-versus-buy review

The laboratory already decides whether a behavior change is blocked.
This contract stores that graduation as a research note. It does not
add an engagement brain or a demand map.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`. No research cost is
invented.

## Maximum cost

No new spend.

## Inputs

A stored prospect. An idempotency key. The laboratory result. Flags
that ask to apply the note, claim traffic, or supply an engagement
count are refused.

## Outputs

`GET /api/operations/learning` and `POST /api/operations/learning`. The
page is `/operations#/learning`.

## Cache policy

The note row is the record. The same prospect and idempotency key
return that row and do not append again.

## Failure handling

A missing prospect is refused. A laboratory that has not graduated is
refused. A request to change production is refused. A claim of
authorized traffic is refused. An engagement count is refused. Delivery
remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The note write uses the
existing operator write policy. The contract cannot change a prospect,
send, or invent a count.

## Persistence

New `LearningNotes` rows in the existing Bliss database. One prospect
and idempotency key is one row. Prospect messages and state stay where
they are.

## Tests

`LearningLedgerTests` and `LearningLedgerApiTests`.

## Evidence

`docs/architecture/LEARNING-LEDGER-EVIDENCE.md` and the PDF report
beside it.

## Acceptance criteria

- A graduated laboratory appends one research note.
- The same key does not append a second row.
- A failed laboratory does not append.
- Research does not change production or behavior.
- No authorized traffic is on file. An engagement count is refused.
- Another prospect's note is not shown.
- Model calls stay at 0. Seven grooming models are not configured.
- The prospect state is unchanged.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

An engagement brain, a demand map, or commercial memory from invented
traffic. Models as the system of record. Research controlling
production. Seven permanent grooming models. A live send. Hosted Bliss
acceptance. Economics Phase 9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The note is a stored sentence after
the laboratory result.

Can we reuse something? Yes. The laboratory and the stored prospects.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
