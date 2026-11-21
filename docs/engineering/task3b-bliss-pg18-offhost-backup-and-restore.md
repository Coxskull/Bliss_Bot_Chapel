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

**Windows PowerShell 5.1, shortest supported route.** From your trusted local checkout of this existing repository, use this single command (the process-local execution policy does not persist or change the machine policy):

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\task3b-owner-start-backup.ps1
~~~

It checks the existing `railway.cmd` npm launcher (avoids the Windows `railway.ps1` execution-policy failure already observed on the owner PC), confirms authenticated `railway.cmd whoami` locally, and reads **only public fingerprints** of GPG encryption keys installed locally. The matching private decryption key may remain **offline in separate owner custody** and does not need to be on the active PC. The owner must type one printed **40-character public fingerprint** and explicitly confirm `RECOVERY-KEY-HELD` only if that matching private decryption key is independently recoverable; **never enter a GPG passphrase, API key or database password in this chat**. The wrapper refuses missing login, untrusted recipient or missing private-key custody attestation before any production database read.

It invokes the existing `task3b-bliss-pg18-capture.ps1` unchanged. If Railway's SSH host key is not registered yet, follow the local **Railway CLI's** SSH-key prompts using the trusted owner account; do not disable host-key verification or publish SSH keys. **PowerShell 5.1 stderr compatibility follow-up (2026-11-21):** After the direct-command repair, the owner's `task3b-bliss-pg18-capture.ps1` passed its local source check but still reported `BLISS_PG18_BACKUP=REMOTE_COMMAND_FAILED`. The helper was using `$ErrorActionPreference='Stop'` across a native `railway.cmd ssh ... 2>&1` call. On Windows PowerShell 5.1, benign Railway SSH notices written to stderr can become terminating `NativeCommandError` records *before* the helper reads the true remote exit code. The updated helper temporarily applies `Continue` **only around that one native SSH invocation**, captures both streams privately, reads `$LASTEXITCODE`, and restores the original `Stop` policy in `finally`. It still refuses a nonzero exit, unknown message, invalid database identity/PGDMP format, or missing encryption; never print raw stdout or stderr because they may contain confidential output. This is a source repair, NOT a real successful encrypted backup; owner must replace the downloaded capture script and independently run it to verify the receipt.

**November 21 owner Windows acceptance:** direct Railway SSH commands succeed on this actual CLI and PostgreSQL 18.6 server: `pg_dump --version` returned exit 0 and `/bin/sh -c 'echo TASK3B_DIRECT_SHELL_OK'` returned exit 0. However, **PowerShell-piped scripts into `railway.cmd ssh -- bash -se` failed with exit 127** (`echo`/`printf` not found, because this CLI did not forward the intended command source correctly). The capture helper was corrected to pass the fixed reviewed shell source as an **ASCII base64 argument** to direct `bash -c`; the script is decoded and piped entirely inside the already-authenticated Railway container. Only the shell program, not the database output, is base64-encoded into command arguments. The backup output remains memory-only before encrypted GPG writing and is **never displayed or logged**. Script source accepted in offline synthetic CI; actual production archive remains **NOT VERIFIED** until the owner receives a `TASK3B_ENCRYPTED_SOURCE_BACKUP=CAPTURED` receipt.

If you downloaded only two owner-side scripts into `$HOME\Downloads`, replace **only** your older `task3b-bliss-pg18-capture.ps1` with the current verified file from the merged `main` GitHub repository; keep the existing `task3b-owner-start-backup.ps1` alongside it. Never paste backup stdout, archive contents, GPG private keys or passwords into GitHub/chat.

Alternatively, an experienced operator who already knows the recoverable secret-key fingerprint may invoke the underlying helper directly:

~~~powershell
.\scripts\task3b-bliss-pg18-capture.ps1 -GpgRecipientFingerprint "YOUR_EXISTING_TRUSTED_40_HEX_GPG_FINGERPRINT"
~~~

Script pins actual project/environment/Postgres service, SQL-checks that source is database bliss / PostgreSQL 18, streams pg_dump custom bytes over private Railway SSH as base64, decodes only in workstation RAM, then GPG-encrypts directly to a unique local .gpg file (maximum 64 MiB unencrypted). No plaintext file. Outputs a .pending.json manifest with source identity, public table count, ciphertext SHA-256 and offhost/restore status explicitly false.

A GPG public key alone is insufficient for recovery; verify the private decryption key is held by the owner, backed up securely, and passphrase recoverable. Never print or transfer the secret key here.

## Stage 2: verified copy of ONLY the encrypted file to existing DigitalOcean droplet

DigitalOcean API confirms that the existing Droplet **571399692** has an attached Block Storage Volume (**67b86c56-dc80-11f1-b677-e23269a37b43**). This confirms attachment, **not** the Linux filesystem mountpoint or available bytes. An incorrect destination could otherwise write ciphertext to the Droplet's root disk and consume its capacity. Identify the actual mounted volume in your existing SSH session (for example, a read-only `findmnt -t ext4,xfs,btrfs` or `lsblk -f`) and choose a **separate Bliss-only directory below that mountpoint**. Do not change its mount settings or the DR362 recovery directory. The owner must know the existing SSH user and verified host key.

The repaired off-host script now checks, **before any mkdir or transfer**, that the nearest existing destination parent is on a non-root, disk-backed ext4/xfs/btrfs mount and has the encrypted archive's size **plus 100 MiB** free. It also rejects canonicalized symlink paths, archive collisions, and destinations on the root filesystem, then checks the remote archive SHA-256 after SCP. If it reports `TASK3B_OFFHOST=NONROOT_ATTACHED_VOLUME_OR_SPACE_NOT_VERIFIED`, leave the source backup local, inspect the existing volume path/space and do **not** substitute the Droplet root disk.

Example (replace placeholders with values verified privately on your workstation):

~~~powershell
.\scripts\task3b-bliss-pg18-offhost.ps1 -ManifestPath "C:\path\bliss-pg18-xxxx.pending.json" -DropletIPv4 "OWNER_DIGITALOCEAN_IPV4" -SshUser "EXISTING_USER" -RemoteDirectory "/ACTUAL_MOUNTED_VOLUME/alpha-bliss-pg18"
~~~

The tool pins the existing DigitalOcean public IP, requires OpenSSH StrictHostKeyChecking=yes, refuses root/unmounted/unsafe destinations **before writing**, creates a separate private 0700 directory only on an existing mounted disk with sufficient free space, uploads only the encrypted archive, checks remote SHA-256 against source ciphertext, then creates .offhost.json. No second Droplet, paid volume, database TCP proxy or account is created. It does not prove source recovery until Stage 3 actually restores and validates the ciphertext.

## Stage 3: actual isolated restore to disposable PostgreSQL 18, without DR362 writes

~~~powershell
.\scripts\task3b-bliss-pg18-isolated-restore.ps1 -OffhostManifestPath "C:\path\bliss-pg18-xxxx.offhost.json"
~~~

Uses existing local Docker Desktop. Creates an ephemeral postgres:18 container with --network none and no published ports, a new isolated restore database, streams GPG decryption directly through an in-memory binary pipe into pg_restore, checks exit status, version and restored public-table count, removes the disposable container, then writes .recovery-evidence.json. No decrypted dump on disk. No live data reimported to DR362 or Railway.

## Alternative Stage 3A: source-only isolated restore from verified private Google Drive copy (no DigitalOcean claim)

**Evidence as of November 21, 2026:** The owner captured a read-only, locally encrypted Bliss Railway PostgreSQL 18 archive. The owner then uploaded the exact `bliss-pg18-20261121T023709Z-cda88608.gpg` (871 bytes) and original `.pending.json` (802 bytes) to a separate, owner-only folder under existing `ALPHA Encrypted Recovery Backups` in Google Drive. Connected Drive readback verified both objects, their sizes, owner-only access, and computed the uploaded **ciphertext SHA-256 matching the pending manifest**. The encrypted object has an OpenPGP public-key-encrypted data packet. These checks **do not prove decryption or database restore**. No DigitalOcean upload occurred; its protected LUKS recovery disk remains locked. Crucially, the source manifest reports **`sourcePublicTableCount: 0`** — a restored zero-table database does not establish a working advertiser-registration schema.

The existing DigitalOcean-based restore contract stays unchanged and remains the only path producing its `.recovery-evidence.json` accepted by the guarded production installer. For this alternate backup custody, use **local-only isolated verification** of the original encrypted archive with a **separate `.local-restore-evidence.json` receipt**. It cannot authorize or be accepted by the production schema installer; actual source table count, archive hash and PG18 isolated restore still must pass.

The owner's existing Windows workstation must have Docker running and a usable PostgreSQL 18 image, plus access to the corresponding **GPG private recovery key** (a public key is insufficient). Do **not** upload/import the private key to GitHub, ChatGPT, Google Drive or an unfamiliar computer. If that key is retained offline, arrange a secure approved restore workstation and stop until key custody is ready. Use process-local PowerShell policy, never alter remote service or security settings:

~~~powershell
cd "$HOME\Downloads"
Get-Command docker, gpg -ErrorAction SilentlyContinue | Select-Object Name, Source
docker info --format '{{.ServerVersion}}'
gpg --batch --list-secret-keys --keyid-format LONG 25CAD4D027B8861B7D4565D2EFA626F40F74F134

# After downloading the reviewed current script to this folder:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\task3b-bliss-pg18-isolated-restore.ps1" -OffhostManifestPath ".\bliss-pg18-20261121T023709Z-cda88608.pending.json" -LocalVerificationOnly
~~~

The local-only switch requires the unmodified, pinned Railway PG18 **original pending manifest** with `offhostProvider=NOT_VERIFIED` and `offhostSha256Verified=false`. It checks the archive's actual SHA-256, starts a disposable `postgres:18` Docker container with `--network none`, decrypts the archive into `pg_restore` through a binary pipe without a plaintext dump file, tests PostgreSQL major/version and table inventory, then removes the test container before creating a receipt. Output markers explicitly state `TASK3B_DIGITALOCEAN_OFFHOST_SHA256=NOT_VERIFIED` and `TASK3B_PG18_LOCAL_ISOLATED_RESTORE=PASS` only after success, with `TASK3B_ADVERTISER_SCHEMA=NOT_ESTABLISHED_ZERO_PUBLIC_TABLES` where applicable. A successful **local-only restore is not a production migration, original DigitalOcean offsite acceptance, or real advertiser registration approval**.

If Docker is not installed or the private recovery key is unavailable on the approved workstation, do not run the restore script. These are owner-side prerequisites, not a reason to unlock DR361/DR362 or create another paid cloud service.

## Stage 4: guarded initial Bliss PG18 schema installer (later, after stages 1-3 verified)

~~~powershell
.\scripts\task3b-guarded-railway-install.ps1 -ReceiptPath "C:\path\bliss-pg18-xxxx.recovery-evidence.json"
~~~

Existing one-shot .NET command remains the authoritative gate: Production locked preview, exact DB identity, TLS VerifyFull and zero public app schema before migration. New Windows guard refuses generic text/DR362 evidence, wrong project/database, stale/partial manifests, mismatched encrypted archive SHA, missing DigitalOcean SHA or local PG18 restore receipt, and leftover test container. Owner must independently attest exact backup using BLISS-PG18-RESTORE-VERIFIED. The manifest is owner-generated evidence, not an independent third-party security audit.

## Release boundaries

Source backup captured != copied to DigitalOcean != isolated PostgreSQL 18 restore passed != schema installed != public advertiser registration ready. Public signup remains OFF until hosted verified-email OIDC + v2 consent + 201/200/409 durable registration receipts, CSRF and cross-tenant denials and PENDING_REVIEW authorization are verified. Leave Global STOP, paid AI, live Stripe and outreach unchanged.

The helper is capped at 64 MiB and is specifically for the presently small, locked pre-registration Bliss database. It is not a production-scale recurring disaster recovery system. Future real customer data requires a streaming, scheduled, monitored encrypted backup design.
