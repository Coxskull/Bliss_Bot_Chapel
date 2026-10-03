# Ask Alpha

Phase 13 of the acquisition tracker. One voice answers the question,
then advances. A repeated question is studied. Ask Alpha does not
invent a person, a price, or a win. Nothing is sent.

## What works

- The page and the reply name one representative: Ask Alpha.
- Teaching, integrity, and handoff are gears of that voice.
- A first explanation answers, names the missing commission, states the
  private-placement value, and asks what a human handoff requires.
- The same question again says Ask Alpha already answered it and moves
  to the handoff.
- A verified public name is the only personal address. Sir, Madam, and
  Señor are not chosen.
- A price question still says it cannot invent a price. The next step
  is a human handoff.
- A human request still says this is not a win.
- Delivery stays `NOT_SENT`.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **248 passed, 0 failed, 0 skipped**.

The new proofs are `AskAlphaTests` and `AskAlphaApiTests`.

## Browser path

On the Mesa Norte demonstration, ask how it works, ask again, then ask
the price. The first reply advances. The second says the question was
already answered. The price reply contains no dollar amount. The gear
line stays on Ask Alpha.

Mesa Norte and `example.com` are fixtures. They are not a claim about a
real business.
