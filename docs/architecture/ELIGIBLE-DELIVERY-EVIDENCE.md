# Eligible delivery

Phase 16 of the acquisition tracker. A stored contact road can become
eligible for one approved adapter. The policy is `preview-only`. The
adapter is `preview-adapter`. Suppression still comes first. The
adapter prepares a copy on the prospect record and does not transmit
it. A public road remains short of permission to send. A delivery
event is still refused.

## What works

- A decision with no stored road is withheld. Alpha does not invent a
  road.
- A suppressed prospect, including Puerto Azul after “The business
  asked Alpha to stop,” stays withheld. The notice says suppression
  comes before the adapter.
- An unapproved policy, an unapproved adapter, and any authorization
  other than “Prepare the preview” are withheld. The road stays
  ineligible.
- Mesa Norte, with the fixture road `marketing@example.com` copied from
  `https://example.com/mesa-norte/contact`, becomes eligible. The
  prepared copy names the business and says the adapter did not
  transmit it. Transmission stays `NOT_SENT`.
- Repeating the same decision does not create a second eligible record.
- Asking Ask Alpha to email the road still says a public road is not
  permission to send. When the preview is eligible, the reply also says
  the preview adapter can prepare a copy.
- A `SENT` event is refused. Nothing was sent.

`marketing@example.com` and `example.com` are fixtures. They are not a
claim about a real mailbox.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **266 passed, 0 failed, 0 skipped**.

The new proofs are `DeliveryPolicyTests` and `EligibleDeliveryApiTests`.

## Browser path

On Source Media, Puerto Azul is already suppressed. Preparing the
preview says suppression comes before the adapter and delivery stays
`NOT_SENT`. Mesa Norte then receives the fixture marketing road. That
road notice stays ineligible. The preview authorization then says the
road is eligible and transmission remains `NOT_SENT`.

The recording is `preview_adapter_prepares_copy_nothing_transmitted.mp4`.
The report is `docs/architecture/evidence/Alpha-Eligible-Delivery-Report.pdf`.
