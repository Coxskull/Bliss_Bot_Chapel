# Engineering Contract: Hosted dock

**Status:** open. Hosted acceptance is not claimed.
**Classification target:** the evidence file records the result after the four phases.
**Delivery:** `NOT_SENT`. Green does not mean send.

This contract prepares the dock. It does not authorize live outreach.

## Objective

Move from local proof toward hosted production readiness without inventing a
hosted environment. Work that can be proved on this machine is proved. Work
that needs an account, a purchase, DNS, or an identity provider stops and is
named.

## Phases

| Phase | Work | Done when |
| --- | --- | --- |
| 1 | Chart the dock. Separate what this run can finish from what the owner must supply. | The chart, the owner list, a PDF, and a recording of the unclaimed hosted page. |
| 2 | Require `VerifyFull`. Read secrets from mounted files. Publish an image contract that contains no secret. Production startup fails closed. | Tests, the refusal record, a PDF, and a recording. |
| 3 | Rehearse `VerifyFull`, apply migrations, back up, and restore on loopback. | The rehearsal record says the marker matched and hosted acceptance was not claimed. A PDF and a recording. |
| 4 | Run the full regression. Classify the milestone. | The test summary, the yellow or green classification, a PDF, and a recording. |

A blocked phase does not cancel the others.

## A. This run can complete

- The phase chart and the owner list.
- The production gate rejects `SSL Mode=Require`, `SSL Mode=VerifyCA`, and `Trust Server Certificate=true`.
- `SSL Mode=VerifyFull` with `Trust Server Certificate=false` remains the accepted declaration.
- Three secrets can be read from absolute files outside the application directory.
- The same secret cannot be set inline and in a file.
- `dotnet Bliss.Api.dll --migrate` applies migrations and exits. It does not start the site and does not claim hosted acceptance.
- The design-time factory uses `ConnectionStrings__DefaultConnection` when that variable is set.
- The Dockerfile builds a Production image and does not contain a secret.
- The publish scanner rejects an embedded connection string, client secret, certificate password, or private key.
- A loopback PostgreSQL rehearsal can prove a real `verify-full` handshake, a wrong-authority refusal, a wrong-hostname refusal, a migration, a backup, and a restore.
- The existing regression suite can be run.

## B. Owner input required

No price is stated. No service was purchased. A missing cost stays unrecorded.

1. **Application host.** Choose where the ASP.NET Core process runs. This run does not have that account.
2. **Hosted PostgreSQL.** Choose a host whose certificate can be checked with `SSL Mode=VerifyFull`. Supply the host, database name, username, password, and the provider CA when it is not already trusted. A prior live test named Supabase project `bkutbglzivfdnoerigyb`. This run has no password for it and did not connect.
3. **Organizational OIDC.** Supply an HTTPS issuer that is not loopback, a client id, and a client secret. Put these role values in the ID token or the userinfo response under the `roles` claim, unless a different claim name is configured:
   - `bliss.viewer`
   - `bliss.operator`
   - `bliss.reviewer`
   - `bliss.admin`
   - `bliss.advertiser`
4. **Secret store.** The process reads environment variables or mounted files. Binding those to a named vendor store is the owner's platform. Do not commit the values.
5. **Proxy and name.** Supply the public HTTPS name, the DNS record, and at least one non-loopback reverse-proxy address for `Runtime__KnownProxies__0`.
6. **Hosted backup and restore.** After the hosted database exists, authorize one backup and one restore of that database. The loopback rehearsal does not certify the hosted backup.
7. **Outreach stays locked.** Hosting the process does not authorize email, WhatsApp, SMS, social messaging, crawlers, paid enrichment, a new model, settlement, a creator payout, or an advertiser charge.

## Secret names

Supply each secret once, either as the value or as the `_FILE` path.

| Setting | File alternative |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings__DefaultConnection_FILE` |
| `Authentication__ClientSecret` | `Authentication__ClientSecret_FILE` |
| `Runtime__DataProtectionCertificatePassword` | `Runtime__DataProtectionCertificatePassword_FILE` |

The connection string used outside Development must include a non-loopback host, a username, a password of at least 12 characters, a database name, `SSL Mode=VerifyFull`, and `Trust Server Certificate=false`. Add `Root Certificate` when the provider CA is not in the machine trust store.

Also set `Authentication__Enabled=true`, `Authentication__Authority`, `Authentication__ClientId`, `Runtime__DataProtectionKeysPath`, `Runtime__DataProtectionCertificatePath`, `Runtime__KnownProxies__0`, `Runtime__Backup__Provider`, `Runtime__Backup__Schedule`, and `Runtime__Backup__RetentionDays`.

## What this contract will not do

- It will not mark hosted acceptance passed because a loopback database accepted `VerifyFull`.
- It will not stand up a substitute identity provider and call it organizational OIDC.
- It will not put a secret in source, in a report, or in a recording.
- It will not send a message. Delivery remains `NOT_SENT`.

## Acceptance rule

🟢 **HOSTED ACCEPTANCE — PASSED** requires the hosted process, the hosted database, a real `VerifyFull` connection to that database, migrations on that database, organizational HTTPS identity with the role behavior, secrets outside source, the operations console on that host, a backup, a restore of that backup, and the regression suite.

🟡 **HOSTED ACCEPTANCE — PARTIAL / BLOCKED** is the result when the dock work is proved and one of the owner inputs is still missing.

🔴 **HOSTED ACCEPTANCE — FAILED** is the result when a required proof was attempted and failed, and the failure is not an absent owner input.
