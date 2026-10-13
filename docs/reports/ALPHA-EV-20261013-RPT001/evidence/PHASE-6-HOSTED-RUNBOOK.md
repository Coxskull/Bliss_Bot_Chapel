# Phase 6 — Recommended hosted deployment and acceptance

The user requested our recommended platform. Target Azure App Service for Linux
containers, Microsoft Entra External ID for OIDC, and Azure Database for
PostgreSQL Flexible Server. Use Azure Key Vault for deployment secrets and Azure
Monitor/Log Analytics for durable operational logs and alerts. Choose a region
close to the intended users after confirming residency requirements and pricing.
No subscription, resources, domains or paid capacity have been provisioned.

Initial recovery objectives: **RPO ≤ 1 hour**, **RTO ≤ 4 hours**, **14-day backup
retention**. RPO bounds lost committed data; RTO measures time until the restored
service passes its functional checks. These are targets requiring a hosted drill,
not a provider guarantee or a result inferred from the small local fixture.

## Deployment inputs

The environment check found no Azure subscription/tenant/application bindings,
hosted OIDC authority/client credentials, hosted database secret-file binding,
data-protection certificate path or backup-provider declaration in the current
process. The retained local activation script selects only local development
PostgreSQL. Supply actual service bindings securely in environment settings or
the deployment secret store; do not put their values in chat or appsettings.

| Setting | Deployment source / requirement |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:8080` inside the TLS-terminating deployment |
| `WEBSITES_PORT` | `8080` for the existing Linux container image |
| `Authentication__Enabled` | `true` |
| `Authentication__Authority` | Actual Entra External ID HTTPS OIDC authority |
| `Authentication__ClientId` | Registered application ID |
| `Authentication__ClientSecret` or `Authentication__ClientSecret_FILE` | Key Vault reference or mounted secret, exactly one mechanism |
| `Authentication__RoleClaimType` | Actual configured role claim; default `roles` |
| `Authentication__AdvertiserIdClaimType` | Trusted server-managed advertiser UUID claim; default `advertiser_id` |
| `ConnectionStrings__DefaultConnection` or `ConnectionStrings__DefaultConnection_FILE` | Hosted DB secret with real FQDN, database, user, password and `SSL Mode=VerifyFull`; exactly one mechanism |
| `Runtime__DataProtectionKeysPath` | Persistent writable shared key-ring directory, e.g. `/home/bliss/data-protection` after verifying mount permissions |
| `Runtime__DataProtectionCertificatePath` | Mounted readable PKCS#12 certificate outside the application directory |
| `Runtime__DataProtectionCertificatePassword` or corresponding `_FILE` | Deployment secret, exactly one mechanism |
| `Runtime__KnownProxies__0` (and subsequent indexes) | Actual trusted ingress proxy IPs validated for this deployment |
| `Runtime__Backup__Provider` | `Azure PostgreSQL Flexible Server` after the managed policy is configured |
| `Runtime__Backup__Schedule` | `Managed backups with point-in-time recovery` after verifying the service policy |
| `Runtime__Backup__RetentionDays` | `14` after configuring and inspecting retention on the service |
| `Runtime__ControlledAi__Enabled` | `false` until Phase 4 live activation is separately completed |

Mounted text secrets must be absolute paths outside the application directory;
the secret loader refuses duplicate environment/file bindings. The existing
container runs as `$APP_UID`, so verify that its account can read the certificate
and write the persistent key ring. Do not assume that an App Service mount has
the required ownership. Verify shared persistence before using multiple replicas.

Configure the five distinct roles `bliss.viewer`, `bliss.operator`,
`bliss.reviewer`, `bliss.admin`, and `bliss.advertiser`. Bind advertiser IDs from
an administrator-controlled identity registry/claim source. A user-editable
signup field must never supply the authorization claim. Validate the actual token
claim names emitted by the selected Entra tenant and user flow. Reviewers also
need authorized workspace access; the reviewer role alone does not grant global
workspace access. Admin/staff access and bound-advertiser access retain their
existing semantics.

Register the HTTPS OIDC callback `/signin-oidc` and sign-out callback
`/signout-callback-oidc` on the final application origin. Verify these against the
actual OpenID Connect handler configuration before registration. A successful
login must not let an advertiser choose a different advertiser claim through
request fields. Advertiser and Alpha QA subjects must differ, and research
reviewers must differ from proposal authors.

Use PostgreSQL's actual FQDN with hostname and CA verification. Install only the
provider's trusted CA chain if the system store does not already trust it. Do not
substitute `Require`, `VerifyCA`, or `Trust Server Certificate=true`: current
production startup requires **VerifyFull**. Older Phase 20 example text predates
this stricter requirement; current code and this runbook take precedence.

App Service ingress addresses and forwarded-header behavior must be verified on
the chosen topology. The current application accepts explicit known proxy IPs,
not an arbitrary trusted network range. Inventory the actual trusted hops; if the
platform cannot provide a maintainable explicit allowlist, resolve the ingress
design or make a reviewed proxy-policy change before hosted acceptance. Never
populate this setting with an invented address merely to pass startup.

## Release and migration

1. Build the existing Dockerfile from the reviewed checkout and tag the image
   with the source revision. The .NET Release build is locally validated; a
   hosted registry push/container rollout has not been executed by Phase 6.
2. Bind the deployment configuration, persistent storage and certificates to a
   staging slot. Ensure database network restrictions permit only the intended
   application and migration paths. Production startup fails closed on missing
   identity, TLS, key protection, proxy and backup declarations.
3. Take/verify a recoverable backup and record its restore point. Run
   `dotnet Bliss.Api.dll --migrate` once as a deployment job using the appropriate
   migration account, then start the app with its normal restricted account.
   Migration execution needs the same valid production configuration.
4. Check `/health/live`, `/health/ready`, authenticated `/health/posture`, and
   staff-only `/api/operations/campaign-readiness`. `UP_TO_DATE` means the schema
   is current; a configuration declaration is not hosted acceptance.
5. Run the acceptance checks below on isolated deployment test data. Record
   evidence, resolve failures, and obtain release authorization for the concrete
   deployment before publishing it to users.

## Hosted acceptance record

Keep the record in deployment-owned access-controlled storage. Record the UTC
test timestamps (display Asia/Taipei times for user schedules), image/source
revision, environment, operator/reviewer identities, results, artifact references
and unresolved failures. Do not record tokens, passwords or private image bodies
in operational logs.

| Area | Required live evidence | Current status |
| --- | --- | --- |
| OIDC and authority | Real login/logout; anonymous 401; forbidden 403; cross-advertiser 404; CSRF refusal; independent approvals and research reviewer | Pending service bindings |
| Database transport | Current connection reports TLS; VerifyFull configured; wrong CA/hostname fails in an isolated connection test | Pending hosted database |
| Session durability | Protected session/key round-trip across restart and intended replicas; certificate renewal procedure | Pending deployment storage |
| Proxy | HTTPS redirects, secure cookies and client-origin handling through actual trusted ingress | Pending ingress topology |
| Observability | Request ID joins response/logs; controlled 401/403/429/5xx/readiness events reach durable logs; test alert reaches designated operator | Pending log sink and operator route |
| Backups | Actual 14-day retention and backup/PITR policy inspected; restore point selected from managed backup | Pending managed database |
| Recovery | Isolated restored server passes lineage manifests and application flow; measure lost commit interval ≤ 1h and elapsed recovery ≤ 4h | Local fixture passed only when recorded in Phase 6 evidence |

The in-process event store is bounded and resets on restart. Configure and test
external log retention and access restrictions; it cannot serve as a durable
audit archive. Database audit/observation/proposal/review history is persistent
and included in database backups. Keep alert recipients and escalation ownership
in the deployment runbook; sending a test alert needs that explicit route.

## Recovery procedure

Restore into a new isolated PostgreSQL server/database. Record the incident time,
last known committed marker, chosen recovery point, start time and functional
readiness time. Restore encrypted session-key material through its separate
approved backup procedure. Validate counts and record digests for Brand DNA,
briefs, candidate bytes/hashes, assessments, approvals, handoffs, placement runs,
accepted quote lines, observations, proposals, reviews and audit records. Check
foreign keys and migrations, then sign in with separate test principals and
resume the measurement history. Verify matching/pricing authority and NOT_SENT
history remain intact. Do not route live writes to the recovery target until the
measured result is reviewed. Record failures and repeat the drill after fixes.

The repository's `scripts/phase6-recovery-check.sh` exercises this lineage and
application procedure on a small fictional local database. Its dump is private
and removed afterward; the script creates and cleans up only its own databases.
It does not establish managed PITR, actual backup retention, session-key recovery,
production volume, regional disaster recovery, or the hosted RPO/RTO targets.
