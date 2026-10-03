# Bliss rematch

Phase 18 of the acquisition tracker. When the current creator cannot
serve the budget, Bliss looks for another creator already stored in the
database. `DeterministicRuleEvaluator` remains the matching authority.
An approved alternate is not a win. A missing active opportunity, or no
other approved match, keeps the advertiser. Delivery stays `NOT_SENT`.

## What works

- `BlissRematch.Select` calls `DeterministicRuleEvaluator.Evaluate` for
  each other creator. It does not keep its own weights.
- The chosen row is the `APPROVED` result with the highest overall
  score. An equal score breaks the tie by name.
- The spoken notice names that creator and says this is not a win.
  Delivery remains `NOT_SENT`. The notice does not state the score or a
  currency amount.
- When every other creator is ineligible, the notice says Bliss found
  no other approved match and the advertiser is kept. No name is
  invented.
- When the opportunity is missing, inactive, or the active rule
  document is absent, Bliss will not rematch. The advertiser is kept.
  Delivery remains `NOT_SENT`.
- The prospect row stays. The state does not become `WON`. Nothing is
  transmitted.
- Mesa Norte has no linked active opportunity. The page says Bliss will
  not rematch without an active opportunity.

`Luz Canal`, `Harbor Voice`, `Current Harbor`, and `Far Coast` are
fixture creator names in automated tests. They are not claims about
real people. `example.com` fixtures are not claims about real
businesses.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **276 passed, 0 failed, 0 skipped**.

The new proofs are `BlissRematchTests` and `BlissRematchApiTests`.

## Browser path

The Mesa Norte demonstration page asks Bliss to look for another match.
The reply is: Bliss will not rematch without an active opportunity. The
advertiser is kept. Delivery remains `NOT_SENT`. No person is named.

The recording is `bliss_rematch_click_keeps_the_advertiser.mp4`. The sentence is absent when the page opens and appears after Look for another Bliss match.
The report is `docs/architecture/evidence/Alpha-Bliss-Rematch-Report.pdf`.
