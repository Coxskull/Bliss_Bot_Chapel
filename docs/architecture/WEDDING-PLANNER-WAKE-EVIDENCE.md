# Wedding Planner wakes after commercial progression

Phase 19 of the acquisition tracker. Wedding Planner stays asleep until
Economics has accepted a result. The wake uses the existing Phase 1
workspace. It inherits the business, the market, and that accepted
amount. It does not plan a campaign, invent an advertiser, or send
anything.

## What works

- A discovered business with no accepted Economics result stays
  `ASLEEP`. The notice says commercial progression has not been
  accepted and this business is not planned. Delivery remains
  `NOT_SENT`. No workspace row is opened.
- Puerto Azul stays `PRESERVED` and asleep. Suppression also keeps the
  planner asleep, and the accepted amount is not spoken.
- An approved quote that Economics has not accepted does not wake the
  planner.
- An accepted result with no advertiser already on file keeps the
  context and opens no campaign.
- An accepted result plus an advertiser already stored opens the
  existing primary workspace, one session, and one `SYSTEM` message.
  The message states the accepted amount and says no campaign is
  planned. A second wake replays that workspace, session, and message.
- A second discovered business without an accepted result stays asleep
  and does not open another workspace.
- The prospect state does not become `WON`.

`215 PHP` in the automated fixture is an Economics amount, not a Mesa
Norte price. `example.com` fixtures are not claims about real
businesses. `Fixture advertiser` is not a claim about a real company.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **284 passed, 0 failed, 0 skipped**.

The new proofs are `WeddingPlannerWakeTests` and
`WeddingPlannerWakeApiTests`.

## Browser path

The Mesa Norte page asks Wedding Planner to wake. Mesa Norte has no
accepted Economics result, so the reply is: Wedding Planner stays
asleep. Commercial progression has not been accepted. This business is
not planned. Delivery remains `NOT_SENT`.

The recording is `wedding_planner_stays_asleep.mp4`.
The report is `docs/architecture/evidence/Alpha-Wedding-Planner-Wake-Report.pdf`.
