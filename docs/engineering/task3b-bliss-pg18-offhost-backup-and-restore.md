# ALPHA Task #3B: Bliss Railway PostgreSQL 18 off-host backup and isolated restore

**State:** Source procedures prepared. Do NOT count a production backup, DigitalOcean upload, isolated restore or schema installation as complete until the owner produces real execution receipts. Existing Factory/Supabase DR362 remains independent and complete.

## Verified resources, November 20 2026

- Railway project: fb33c1b7-03fb-42c8-89b1-081951a738fd / production environment b67a9836-36bf-4083-93f7-00c867b13f39.
- Existing Postgres service: 6aa78407-1632-48c9-8ea4-0289a7d36a5b, PostgreSQL 18, database name bliss per prior owner-target classifier. Existing app bliss-bot still has registration locked.
- Database has **no public TCP proxy**; preserve that. Railway Postgres service has provider-private PGUSER, PGPASSWORD, PGHOST, PGPORT vars. The owner-only Railway CLI SSH executes the dump inside that service, not on the public network.
- Existing DigitalOcean Droplet: 571399692, the current public IPv4 privately visible in the owner DigitalOcean account, attached volume. Use a separate Bliss-only encrypted-backup directory, not the DR362 database/recovery path.
- Connected OAuth tools do not expose direct shell execution in live Railway or DigitalOcean. These scripts are for the owner's existing authenticated Windows terminal, not a simulated hosted success.

## Prerequisites (existing free local tools, no new cloud subscription)

Railway CLI signed in as authorized owner, GPG with an owner-trusted encryption recipient and recoverable private key held separately, Windows OpenSSH SSH/SCP with verified host key, and local Docker Desktop for isolated PostgreSQL 18 restore. Do not publish private key or dump in GitHub, chat, Drive, ticket or public site.

Read-only prerequisite command:

~~~powershell
Get-Command railway, gpg, ssh, scp, docker -ErrorAction SilentlyContinue |
  Select-Object Name, Source
~~~

If any required tool is missing, do not run the database installer or substitute plaintext archives.

## Stage 1: private read-only pg_dump and GPG-encrypted archive

~~~powershell
.\scripts\task3b-bliss-pg18-capture.ps1 -GpgRecipientFingerprint "YOUR_EXISTING_TRUSTED_40_HEX_GPG_FINGERPRINT"
~~~

Script pins actual project/environment/Postgres service, SQL-checks that source is database bliss / PostgreSQL 18, streams pg_dump custom bytes over private Railway SSH as base64, decodes only in workstation RAM, then GPG-encrypts directly to a unique local .gpg file (maximum 64 MiB unencrypted). No plaintext file. Outputs a .pending.json manifest with source identity, public table count, ciphertext SHA-256 and offhost/restore status explicitly false.

## Stage 2: verified copy of ONLY the encrypted file to existing DigitalOcean droplet

First ensure a suitable existing mounted filesystem has space and the exact SSH user/host key are correct. Remote folder must be Bliss-specific; no changes to the DR362 database location. Example:

~~~powershell
.\scripts\task3b-bliss-pg18-offhost.ps1 -ManifestPath "C:\path\bliss-pg18-xxxx.pending.json" -DropletIPv4 "OWNER_DIGITALOCEAN_IPV4" -SshUser "EXISTING_USER" -RemoteDirectory "/mounted/volume/alpha-bliss-pg18"
~~~

The tool pins the existing DigitalOcean public IP, requires OpenSSH StrictHostKeyChecking=yes, makes a separate private 0700 directory, uploads only the encrypted archive, checks remote SHA-256 against source ciphertext, then creates .offhost.json. Remote shell permissions/space remain owner-controlled.

## Stage 3: actual isolated restore to disposable PostgreSQL 18, without DR362 writes

~~~powershell
.\scripts\task3b-bliss-pg18-isolated-restore.ps1 -OffhostManifestPath "C:\path\bliss-pg18-xxxx.offhost.json"
~~~

Uses existing local Docker Desktop. Creates an ephemeral postgres:18 container with --network none and no published ports, a new isolated restore database, streams GPG decryption directly through an in-memory binary pipe into pg_restore, checks exit status, version and restored public-table count, removes the disposable container, then writes .recovery-evidence.json. No decrypted dump on disk. No live data reimported to DR362 or Railway.

## Stage 4: guarded initial Bliss PG18 schema installer (later, after stages 1-3 verified)

~~~powershell
.\scripts\task3b-guarded-railway-install.ps1 -ReceiptPath "C:\path\bliss-pg18-xxxx.recovery-evidence.json"
~~~

Existing one-shot .NET command remains the authoritative gate: Production locked preview, exact DB identity, TLS VerifyFull and zero public app schema before migration. New Windows guard refuses generic text/DR362 evidence, wrong project/database, stale/partial manifests, mismatched encrypted archive SHA, missing DigitalOcean SHA or local PG18 restore receipt, and leftover test container. Owner must independently attest exact backup using BLISS-PG18-RESTORE-VERIFIED. The manifest is owner-generated evidence, not an independent third-party security audit.

## Release boundaries

Source backup captured != copied to DigitalOcean != isolated PostgreSQL 18 restore passed != schema installed != public advertiser registration ready. Public signup remains OFF until hosted verified-email OIDC + v2 consent + 201/200/409 durable registration receipts, CSRF and cross-tenant denials and PENDING_REVIEW authorization are verified. Leave Global STOP, paid AI, live Stripe and outreach unchanged.

The helper is capped at 64 MiB and is specifically for the presently small, locked pre-registration Bliss database. It is not a production-scale recurring disaster recovery system. Future real customer data requires a streaming, scheduled, monitored encrypted backup design.
