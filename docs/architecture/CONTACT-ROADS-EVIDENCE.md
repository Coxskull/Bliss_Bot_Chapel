# Contact roads

Phase 9 of the acquisition tracker. A prospect can keep several public
contact roads, and the operator can suppress the prospect. Finding a
road does not authorize a send. Outreach eligibility stays closed until
a later phase supplies a policy and an approved adapter.

## What works

- A road needs a legitimate business kind, a value copied from a public
  page, and its own absolute http or https source. A missing source is
  rejected and is not stored.
- Restaurant Puerto Azul in Quito can hold a company marketing address
  and a published messaging value from `https://example.com/puerto-azul/contact`.
  Both start as `DISCOVERED`. `outreachEligible` stays false. Delivery
  stays `NOT_SENT`. Decision-maker confidence stays `UNVERIFIED`.
- The same kind and value are not stored twice.
- A blank suppression reason is rejected. Alpha does not invent one.
- The reason “The business asked Alpha to stop” marks the prospect
  suppressed and moves every road to `SUPPRESSED`. A road recorded after
  that is stored already suppressed. Delivery stays `NOT_SENT`.
- Asking Alpha to email the road receives: a public road is not
  permission to send, the prospect is suppressed, and delivery remains
  `NOT_SENT`.

Puerto Azul and `example.com` are fixtures. They are not a claim about
a real business, a real mailbox, or a real messaging number.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **229 passed, 0 failed, 0 skipped**.

The new proofs are `ContactRoadTests` and `ContactRoadApiTests`.

## Browser path

On Source Media, record the Puerto Azul marketing road and the
messaging road, then suppress the prospect. The notice says outreach is
not eligible and delivery is `NOT_SENT`. The preserved page and the
preview repeat that a public road is not permission to send.
