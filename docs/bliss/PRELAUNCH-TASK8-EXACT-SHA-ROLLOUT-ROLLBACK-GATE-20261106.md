# Pre-Launch Task #8 — Exact-SHA rollout, restoration, and rollback decision gate

**Captured:** 2026-11-06, connected GitHub/Railway read only. **Applies to:** existing `bliss-bot` Railway **production** environment, which remains in `Runtime__RestrictedPreview`. **Status: REVIEW PACKAGE ONLY, NO-GO.**

## Baseline identity (do not confuse Vercel with Railway)

| Evidence | Verified value |
| --- | --- |
| Railway project / environment | `bliss-bot` / `production` |
| App service / latest deploy | `bliss-bot` / `8dc01af8-61a8-493b-a0c5-0277fab7c478` |
| Deployed GitHub commit | `538086009ffec219126fc2b5d907e2bcaede10be` |
| Latest successful app deploy | 2026-11-06 06:33:30 UTC (Railway reports SUCCESS; later settled online) |
| Current GitHub `main` at comparison | `2ca8b933482e0ce4c0e9b953e043339628d59e6d` |
| Exact compare | `5380860...2be..2ca8b9...e6d`: `main` is **28 commits ahead**, 0 behind; **20 changed files** |
| Railway app + Postgres | Both 1/1 online; no recent failures, warnings or pending staged changes |
| Boot / live health evidence | Runtime logged `GET /health/live 200` |
| Rollback capability | Railway exposes `canRollback=true` for deployed release; **never rehearsed** |
| Database | Existing Railway PostgreSQL 18 volume; **no current nonempty restore/verified managed Auth recovery** |
| Security | Restricted Preview intentionally on; no functional customer login or commercial operation released |

Source compare is available at
`https://github.com/Coxskull/Bliss_Bot_Chapel/compare/538086009ffec219126fc2b5d907e2bcaede10be...2ca8b933482e0ce4c0e9b953e043339628d59e6d`.

## Changes NOT in the deployed app at this checkpoint

- **Public documents:** Stripe website-readiness packet, draft pricing/terms/refund policy. Drafts are **not** merchant approval or accepted client contracts.
- **Public presentation:** new service/contact pages; revisions to privacy/terms; homepage/CTA, forms, mobile styling and role-based login links.
- **Build / assurance scripts:** Vercel portal export, public-form smoke, Stripe public link smoke, creator interest guard tests.
- These changes affect website communication and customer expectations even when server-side Auth remains intentionally locked. **Review all external policy/call-to-action wording before publication.** The 28-commit count is exact for the pinned pair, not a promise that `main` cannot advance later.

## Executable acceptance and failure conditions (owner-controlled, no automatic deployment)

1. **Identity/version gate:** Before any rollout, capture the CURRENT `main` SHA, production Railway source/deployment IDs, Vercel release SHA and protected frontend/worker origin. Compare from the actual deployed SHA; re-review every later commit and reconcile public website vs server routes. A successful GitHub PR CI for a different SHA does not prove the deployed artifact.
2. **Backup and restore gate:** Check actual Railway PostgreSQL backup/snapshot **inventory**, retention and recovery permissions. Confirm an encrypted owner-controlled **nonempty** prechange backup, independent verified offsite copy, custody of age recovery identity, restore into an approved isolated target and before/after row/role/schema/receipt fingerprint checks. **Previous empty-baseline restore and disposable PG16/18 tests are limited PASS, not business-system DR acceptance.** Never claim a volume is backed up solely because it exists.
3. **Pre-deploy safety:** Owner reviews all changes, consumer/privacy/merchant promises and rollback plan; accepts maintenance window, single exact source SHA and intended state. Validate supported proxy HTTPS/host, Auth issuer/audience, public sign-in denial, login CSRF and restore compatibility. Restricted Preview and Factory STOP unchanged; no new database grants, Stripe checkout, paid operations or automatic outreach.
4. **Read-only smoke at intended target:** GET `/health/live` must be **200**; `/health/ready` may intentionally be **503** while Restricted Preview is engaged (record expected reason, do not auto-disable it). GET `/preview-status` must declare `restrictedPreview=true` and `operational=false`. Protected `/api/auth/session`, creator/advertiser dashboards and commercial endpoints must reject anonymous access without data. Any proxy that converts an error document into HTTP 200 must be judged on BOTH response body and actual HTTP status; a rendered error page is **not** a successful auth flow.
5. **Rollback rehearsal:** Only on approved **isolated** environment, intentionally apply a reversible nonempty *synthetic* change and verify the protected baseline is restored; measure restore and health times, row counts, receipt digests, STOP and RLS. For production, identify exact rollback deployment/commit and preserve snapshot before changes. Railway `canRollback` is available UI capability, not an executed rollback, and reverting an app image cannot undo incompatible DB migrations.
6. **GO decision:** Record owner, security, finance, legal and operations reviewers, current explicit approvals, exact SHA and live receipts. Keep public signup and financial operations blocked until all required full-service gates pass. If any gate fails, **NO-GO** and preserve deployed working version.

## Remaining blockers (still OPEN)

- No authenticated new Railway Postgres backup inventory readback, provider retention record or full nonempty isolated restore.
- No full managed customer Auth/MFA recovery or successful role/tenant login with session denial.
- No independent accepted staged rollback of a failed nonempty schema/runtime upgrade and measured RPO/RTO.
- No current approval to lift Restricted Preview, offer commercial paid services or merge this draft PR.
- A direct external web probe attempted from the research environment could not resolve the Railway domain; this is **an inability to recheck from this network**, not evidence that the Railway service is down. Railway deployment/health inventory is separately verified by its connected provider API.

**Actions performed:** only connected read-only project/deployment/GitHub comparison and creation of this review document inside existing **draft PR #316**. No Railway config/variable/persistent volume changes, SQL writes, restore, domain changes, payments, spending, or production deployment. Refer to [Issue #302](https://github.com/Coxskull/Bliss_Bot_Chapel/issues/302) for production acceptance.
