# Negotiation inside Economics

Phase 15 of the acquisition tracker. Ask Alpha can record a visitor
proposal only when an approved Economics quote is linked and the figure
sits on that quote's existing envelope. The creator floor is the line's
recommendation low. The high is the line's recommendation high. The
write goes through the Phase 5 quote ledger as a negotiated draft.
Nothing is accepted, nothing is approved, and nothing is sent.

## What works

- A proposal with no approved quote says Ask Alpha will not negotiate
  without an approved Economics quote. The reply contains no digits.
- A figure below the creator floor names that floor and does not write
  an outcome. The quote stays approved.
- A figure above the envelope high does not write an outcome and does
  not raise the price.
- A figure inside the envelope, including the floor and the high, is
  recorded by `QuoteService` as `NEGOTIATED`. The quote returns to
  draft. The reply names that draft total and says it is not accepted.
- A later price question states no number, because a draft is not an
  accepted Economics result.
- A price question that is not a negotiation stays on the Phase 14
  path.
- A human request is still a human request.
- Delivery stays `NOT_SENT`. There is no second negotiation service.

The amounts `200 PHP`, `250 PHP`, `300 PHP`, and `220 PHP` are
automated fixtures on an in-memory quote. They are not a price for
Mesa Norte and not a claim about a real business.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **260 passed, 0 failed, 0 skipped**.

The new proofs are `NegotiationEnvelopeTests` and `NegotiationApiTests`.

## Browser path

On the Mesa Norte demonstration, no approved Economics quote is linked.
Ask Alpha to take 100. The reply refuses to negotiate without an
approved Economics quote, invents no price, and leaves delivery
unsent.
