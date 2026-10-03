# Hosted acceptance reading

The reading after Economics Phase 9 acceptance. One row stores the
process posture. Hosted acceptance is not claimed. A local database is
not a hosted database. An identity provider was not contacted. A backup
drill was not run. Green does not send. Delivery stays `NOT_SENT`.

## What works

- The reading copies the environment and the posture flags.
- Hosted acceptance claimed stays no.
- Identity contacted stays no. A backup drill stays no.
- Satisfied gates still leave the claim refused.
- The same reading key does not write a second row.
- A request to claim hosted acceptance is refused.

Phase 20 remains the production-posture proof. This reading does not
replace it and does not finish hosted production.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **424 passed, 0 failed, 0 skipped**.

The new proofs are `HostedAcceptanceTests` and `HostedAcceptanceApiTests`.

## Browser path

`/operations#/hosted` reads the process that is running.

The contract is `docs/architecture/contracts/HOSTED-ACCEPTANCE-CONTRACT.md`.
