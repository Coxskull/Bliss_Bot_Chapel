# Conversation laboratory

Phase 20 of the acquisition tracker. Persona scenarios run against the
production Ask Alpha replies before a behavior change. The laboratory
does not edit those replies and does not write a prospect. Delivery
stays `NOT_SENT`.

## What works

- Ten scenarios call `DemonstrationConversation.Reply`.
- An unverified price question states no number.
- An unverified owner question does not use a personal name.
- A stale public-name fixture is not spoken.
- An illustrative current fixture may use the recorded public name.
  That name is a fixture, not a claim about a real person.
- A public road is not permission to send. Suppression and the preview
  adapter keep transmission at `NOT_SENT`.
- The Economics fixture says `215 PHP` and no other amount. That figure
  is not a Mesa Norte price.
- A repeated explanation says Ask Alpha already answered.
- Recorded interest is not a contract and not a win.
- The laboratory response reports zero AI calls. Reading it does not
  add a message to Mesa Norte.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **289 passed, 0 failed, 0 skipped**.

The new proofs are `ConversationLaboratoryTests` and
`ConversationLaboratoryApiTests`.

## Browser path

`/acquisition/laboratory.html` shows 10 of 10 scenarios passed. The
notice says production conversation was not changed and delivery
remains `NOT_SENT`.

The recording is `conversation_laboratory_scenarios_passed.mp4`.
The report is `docs/architecture/evidence/Alpha-Conversation-Laboratory-Report.pdf`.
