# Prospect demonstration evidence

Bounded Development feature. It does not send email, buy a subscription,
or change Bliss matching.

## What works

- A source slice of about 15 seconds can be created or uploaded.
- Exact duplicates, including a renamed file, earn 0 quota credit.
- A re-encoded copy is classified as a near-duplicate and earns 0 quota credit.
- One qualified slice can be reused for an ABC Pharmacy overlay.
- The overlay, QR code, and disclosure are produced by software. The QR
  points at the demonstration page.
- The private page and the message preview name the business, the market,
  and the likely buying roles. The decision-maker stays `UNVERIFIED`.
- The page conversation answers how the demonstration works, refuses to
  invent a person or a price, and escalates a request for a human.
- Delivery status stays `NOT_SENT`. No mailbox is contacted.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **209 passed, 0 failed, 0 skipped**.

The new proofs are `SourceMediaRulesTests` and
`ProspectDemonstrationApiTests`. The previous suite remains green.

## Limits

The reference videos are original studio slices, not downloaded podcasts.
A person can upload a slice they are allowed to use. External delivery
waits for an approved communication adapter and a verified contact route.
PostgreSQL persistence of this library is not in this pass. The library
is stored as files beside the API so the demonstration runs without a
hosted database. The long-term system of record remains PostgreSQL.
