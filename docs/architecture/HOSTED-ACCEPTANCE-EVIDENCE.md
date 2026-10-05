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

On this process the environment is Development. Production gates are
not applied. Hosted database, server-certificate verification, identity
provider HTTPS, and a declared backup stay no. Secrets outside
appsettings and distinct role claims stay yes. `hosted-reading-1`
stores that posture. Hosted acceptance claimed, identity contacted, and
backup drill run stay no. The same key was not stored again. A request
to claim hosted acceptance was refused. Delivery is `NOT_SENT`.

The recording is `hosted_reading_leaves_acceptance_unclaimed.mp4`.
The report is `docs/architecture/evidence/Alpha-Hosted-Acceptance-Report.pdf`.

The contract is `docs/architecture/contracts/HOSTED-ACCEPTANCE-CONTRACT.md`.
The next contract is `docs/architecture/contracts/HOSTED-DOCK-CONTRACT.md`.
That contract does not claim hosted acceptance.
