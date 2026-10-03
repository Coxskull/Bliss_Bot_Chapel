# Bounded advertiser discovery

The priority contract after source-media coverage. Discovery counts
stored prospects that have a public source URL. A duplicate source is
not a second prospect. A blank name is refused. A score below 100 stays
preserved. A crawler did not run. This is not a census. Green does not
send. Delivery stays `NOT_SENT`.

The operator supplies the name and the public page. A copyrighted
download is not part of this contract. Contact enrichment is not
purchased.

## What works

- One public source is one `ProspectMemories` row.
- The same source URL does not raise the count, including a trailing
  slash and a different business name.
- The same business name does not raise the count.
- A blank name and a name that cannot make a prospect id are refused.
- A named public source outside the initial markets is preserved.
- A stored prospect without a public source is withheld.
- The operations reading reports stored, scored, and preserved counts.

`example.com` fixtures are not claims about real businesses.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **378 passed, 0 failed, 0 skipped**.

The new proofs are `AdvertiserDiscoveryTests` and
`AdvertiserDiscoveryApiTests`.

## Browser path

`/operations#/discovery` reads the stored prospects. Each row is a
stored public source. A business that is not stored is absent.

The contract is `docs/architecture/contracts/ADVERTISER-DISCOVERY-CONTRACT.md`.
