# Human creative approval

The priority contract after the marketplace handoff. A human records
one creative decision inside a Wedding Planner workspace that is
already open. A discovered business is not opened. Campaign ready is
refused. No price is invented. Six roles are not called. A match is
not written. Green does not send. Delivery stays `NOT_SENT`.

## What works

- An operator approval is `HUMAN_APPROVED`. Campaign ready stays false.
- A withhold stays `WITHHELD`.
- A system actor is refused. Model calls stay at 0.
- The same workspace and idempotency key do not write a second row or
  a second audit.
- Another workspace does not show the decision, the audit, or the
  conversation.
- The match list is left unchanged.

The seeded advertisers are fixtures. They are not claims about real
businesses.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **402 passed, 0 failed, 0 skipped**.

The new proofs are `CreativeApprovalTests` and
`CreativeApprovalApiTests`.

## Browser path

`/operations#/creative` reads one open workspace at a time.

On the live library, Sunrise Wellness Co. holds the human title
Table card. The status is `HUMAN_APPROVED`. Campaign ready, match
written, and price invented stay no. Model calls are 0. The
conversation in that workspace is the stored note Sunrise private
note. The audit is `CREATIVE_DECIDED`. The same decision was not
written again. TEST Dental Manila shows no decision, no conversation,
and no creative audit. Casa Verde and Mesa Norte are not in the
workspace list. The match list stayed at 7. Delivery is `NOT_SENT`.

The recording is `creative_approval_human_not_campaign_ready.mp4`.
The report is `docs/architecture/evidence/Alpha-Creative-Approval-Report.pdf`.

The contract is `docs/architecture/contracts/CREATIVE-APPROVAL-CONTRACT.md`.
