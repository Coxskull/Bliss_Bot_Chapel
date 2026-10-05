# Hosted dock evidence

**Date:** 2026-10-04
**Milestone:** HOSTED ACCEPTANCE — PARTIAL / BLOCKED
**Delivery:** `NOT_SENT`

The dock is prepared. Hosted acceptance is not claimed. A loopback database is not a hosted database. An identity provider was not contacted. No service was purchased and no price is invented. Green does not send.

## Phase 1 — chart the dock

The contract is `docs/architecture/contracts/HOSTED-DOCK-CONTRACT.md`.
The report is `docs/architecture/evidence/dock/Alpha-Dock-Phase-1-Report.pdf`.
The recording is `dock_phase1_chart_and_unclaimed_hosted_page.mp4`.

Work this run can finish is separated from the owner inputs: application host, hosted PostgreSQL with `VerifyFull`, organizational OIDC, a secret-store binding, DNS and the reverse proxy, and a hosted backup restore. Outreach stays locked.

## Phase 2 — production gate and secrets

Outside Development, `SSL Mode=VerifyFull` is required and `Trust Server Certificate` is rejected. A Production process with no secrets exited 134 and reported `Production posture is incomplete`. The refusal is `docs/architecture/evidence/dock/production-startup-refusal.txt`.

`dotnet publish` was scanned. Result: `Publish scan passed. No embedded secret was found.` The record is `docs/architecture/evidence/dock/publish-scan.txt`. The Dockerfile contains no secret. Docker is not installed on this machine, so the image was not built and is not reported as deployed.

The report is `docs/architecture/evidence/dock/Alpha-Dock-Phase-2-Report.pdf`.
The recording is `dock_phase2_fail_closed_startup_and_publish_scan.mp4`.

## Phase 3 — VerifyFull rehearsal

PostgreSQL 16 on loopback presented a certificate for `bliss-rehearsal.local` and `127.0.0.1`.

- Matching name: success
- Loopback address: success
- Wrong certificate authority: refused
- Wrong hostname: refused
- `dotnet Bliss.Api.dll --migrate`: 31 history rows
- Backup over verify-full: 260905 bytes
- Restore marker: match
- Restored history rows: 31

The password was not written down. The record is `docs/architecture/evidence/dock/verifyfull-rehearsal.txt`. The local procedure drill also restored `marker-20261004T235306Z-7804`. Neither drill is a hosted restore.

The report is `docs/architecture/evidence/dock/Alpha-Dock-Phase-3-Report.pdf`.
The recording is `dock_phase3_verifyfull_backup_and_restore.mp4`.

## Phase 4 — regression and classification

```text
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
Passed: 452  Failed: 0  Skipped: 0
```

The report is `docs/architecture/evidence/dock/Alpha-Dock-Phase-4-Report.pdf`.
The recording is `dock_phase4_regression_and_claim_refused.mp4`.

## Classification

🟡 **HOSTED ACCEPTANCE — PARTIAL / BLOCKED**

The owner still has to supply the host, the hosted database, organizational OIDC, the secret-store binding, DNS and the proxy address, and authorization for a hosted backup and restore.

## Owner acknowledgment

On 2026-10-05 the owner acknowledged the four phase reports and recordings. The local engineering work, the VerifyFull rehearsal, the wrong-authority and wrong-hostname refusals, the 31 migrations, the backup and restore, the 452 passing tests, and the refusal to claim hosted acceptance were received. The owner kept the milestone at PARTIAL / BLOCKED. Hosted acceptance stays unclaimed until the hosted requirements are authorized, implemented, tested, and evidenced. Delivery remains `NOT_SENT`.
