# Economics price answer

Phase 14 of the acquisition tracker. Ask Alpha reads an accepted
Economics quote and may say that amount. A missing, draft, or declined
result leaves the reply with no number. Nothing is sent.

## What works

- A price question with no linked quote says it cannot invent a price
  and contains no digits.
- A quote id that Economics has not accepted is refused. The amount is
  not stored and is not spoken.
- An accepted quote of `215 PHP` is spoken as `Economics accepted 215
  PHP`. That is the only number in the reply.
- The same quote, after Economics marks it declined, is no longer
  spoken.
- A symbol, a range, and a short currency code are not a speakable
  amount.
- Delivery stays `NOT_SENT`. Negotiation stays in Economics.

`215 PHP` is the existing Economics acceptance fixture. It is not a
price for Mesa Norte and not a claim about a real business.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **252 passed, 0 failed, 0 skipped**.

The new proofs are `EconomicsPriceSpeechTests` and
`EconomicsPriceApiTests`.

## Browser path

On the Mesa Norte demonstration, ask the price. The reply states no
number. Submit an Economics quote id that is not accepted. The notice
says Ask Alpha will not state a number. Ask the price again. The reply
still states no number.

Mesa Norte and `example.com` are fixtures. They are not a claim about a
real business.
