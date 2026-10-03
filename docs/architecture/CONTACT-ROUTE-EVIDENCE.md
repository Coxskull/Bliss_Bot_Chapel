# Contact route audit

The priority contract after bounded advertiser discovery. The route
reads stored evidence, freshness, suppression, and the preview adapter.
A public address is not permission to send. An explicit transmission
request is recorded once. No adapter transmits it. This is not a
census. Green does not send. Delivery stays `NOT_SENT`.

A person who is not stored is not invented. Contact enrichment is not
purchased.

## What works

- Suppression comes before the adapter.
- Stale evidence is not used.
- A missing public evidence URL does not invent a person.
- A missing road is not invented.
- A current preview names `preview-adapter` and stays `NOT_SENT`.
- Evidence with a road and no preview stays withheld.
- The words "Send the message" are recorded once under one idempotency
  key. A second request with that key writes nothing.
- An address in the recorded words is not repeated on the reading.

`example.com` fixtures are not claims about real businesses or real
mailboxes.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **384 passed, 0 failed, 0 skipped**.

The new proofs are `ContactRouteAuditTests` and `ContactRouteApiTests`.

## Browser path

`/operations#/routes` reads the stored prospects and the transmission
audits. A business that is not stored is absent.

The contract is `docs/architecture/contracts/CONTACT-ROUTE-CONTRACT.md`.
