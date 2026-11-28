# ALPHA Issue #330 — separate Bliss Supabase Auth + intake encrypted recovery

**Status: Source-only owner-workstation capture tool prepared. NOT executed against production. NOT restore-attested. No reviewer role or queue release.**

This is the **Bliss Supabase** PostgreSQL **17** project `bkutbglzivfdnoerigyb`, not the Bliss Railway PostgreSQL 18 service, not the DigitalOcean DR362 Factory volume and not the protected Factory staging project. The November 22 Railway/Drive recovery evidence is accepted for that other database but cannot establish rollback of this project's `auth.users`, `auth.sessions`, `auth.mfa_factors`, public inquiries or private support operator grants.

## Capture procedure — existing owner Windows computer, local WSL Ubuntu only

Reviewed script: `scripts/task330-bliss-supabase-auth-intake-capture.sh`. It performs **no DML/DDL**, requires the exact project identity, a PostgreSQL 17 client, certificate-validated SSL, a private `.pgpass` file, a locally installed **GPG public encryption recipient**, and explicit assertion that the matching *private* decryption key is recoverable **separately**. It refuses all other hosts and databases, and never writes plaintext backup bytes. GitHub Actions can only use a fictional loopback PostgreSQL fixture.

In the owner's existing WSL Ubuntu shell, use credentials **only on that machine**. Never paste a password, `.pgpass`, PostgreSQL URL, Auth user UUID, GPG private key, JWT or decrypted dump into this chat, GitHub or public logs.

1. Make a private Linux filesystem directory (not `/mnt/c` and not the DR362 volume): `mkdir -m 700 -p "$HOME/alpha330-auth-private"`. Keep the encrypted output off public storage.
2. Confirm `pg_dump --version` reports **17.x** and `gpg --list-keys --fingerprint` shows an existing trusted, 40-hex public recipient. Independently confirm its decryption private key and passphrase are recoverable offline.
3. In the Supabase dashboard, use **Bliss Bot project** `bkutbglzivfdnoerigyb` → **Connect** to obtain the **direct** or **session pooler** connection details. Configure local WSL libpq `PGHOST`, `PGPORT=5432`, `PGDATABASE=postgres`, and `PGUSER`. The script only accepts direct `db.bkutbglzivfdnoerigyb.supabase.co` with `postgres` user, or the `aws-N-ap-southeast-1.pooler.supabase.com` session pooler with `postgres.bkutbglzivfdnoerigyb` user. It refuses unknown source instances.
4. Set `PGSSLMODE=verify-full` and `PGSSLROOTCERT` to the locally verified Supabase CA certificate, and `PGPASSFILE` to a private local password file with `chmod 600`. Do **not** place a password in command arguments, `PGPASSWORD`, a committed file or this chat.
5. Set the owner-only environment flags locally: `TASK330_CAPTURE_MODE=owner`, `TASK330_OWNER_RECOVERY_AUTHORIZATION=AUTH_INTAKE_ENCRYPTED_CAPTURE`, `TASK330_PRIVATE_DECRYPTION_KEY_HELD=RECOVERY_KEY_HELD_SEPARATELY`, `TASK330_PRIVATE_OUTPUT_DIR="$HOME/alpha330-auth-private"`, and `TASK330_GPG_PUBLIC_FINGERPRINT` to the owner's confirmed 40-hex public encryption key fingerprint.
6. Run **one bounded source operation** from the existing checked-out repository: `bash scripts/task330-bliss-supabase-auth-intake-capture.sh`. If any preflight fails, it prints one generic `TASK330_AUTH_CAPTURE=HELD_...` marker and **does not** retry or weaken TLS, database identity checks or encryption. Never publish raw stderr/SQL errors. Do not write archive bytes into Windows Downloads/Drive without a separately approved encrypted-storage custody route.

The tool captures the complete `auth`, `public`, and `alpha_support_private` schemas as a consistent custom-format `pg_dump` piped directly into GPG. **PostgreSQL's `-n` and `-t` options must not be mixed** in this design: table selection would exclude schema objects. The wider `public` and private-support scopes contain sensitive information; only an encrypted archive may be retained.

After **actual** success it prints only:
```text
TASK330_AUTH_CAPTURE=ENCRYPTED_LOCAL_ONLY
TASK330_AUTH_CAPTURE_CIPHERTEXT_SHA256=AVAILABLE_IN_PRIVATE_RECEIPT
TASK330_AUTH_ISOLATED_RESTORE=NOT_VERIFIED
TASK330_REVIEWER_RELEASE=HELD
```
The private local `.receipt.txt` contains only fixed scope, capture UTC, ciphertext byte count and ciphertext SHA-256; never operator emails, credentials, source rows or session values. The encrypted file does not itself prove decryption, archive integrity or recoverability.

## Mandatory isolated restore acceptance — still NOT COMPLETE

**Do not** feed this dump to production Supabase, any Factory/DR362 database, Railway PostgreSQL 18, or the user's only production database.

A separately reviewed **disposable, isolated, compatible Supabase/PostgreSQL 17** recovery target must be obtained or verified **at $0 additional cost** before a real restore is attempted. Verify correct extensions, reserved auth roles and ownership compatibility, and reject an unsafe/plain PostgreSQL-only substitute if it fails. Decrypt **in a protected memory-only pipe** where possible, restore without logging source data, compare Auth users/identities/sessions/MFA and intake/receipt/CRM/support table metadata counts to a contemporaneous sanitized source receipt, then destroy the disposable target after cleanup acceptance. Source `auth` privileges and grants must never be reconstructed on live Supabase as a speculative experiment. Operator reviewer permission requires a fresh recovery receipt specific to project `bkutbglzivfdnoerigyb`.

After independent ciphertext custody and actual isolated restoration succeed, separate authorization is still required to:
- set **only** the owner-confirmed existing Auth user server-owned `app_metadata.alpha_intake_operator` through the **Supabase Auth Admin API**, not by writing `auth.users` SQL;
- restrict the server-side exact operator UUID allowlist and trusted Origin, apply the security-reviewed service-role-only live session RPC on the correct project, and validate AAL1, revoked, expired, wrong-origin and withdrawn-support-grant denial;
- approve a **new** source change to `PRODUCTION_INTAKE_REVIEW_RELEASE_APPROVED=false`; until then the deployed reviewer queue is hard-closed.

The unrelated public advertiser registration and fictional ad preview remain available. Creator registration, private inquiry review, real customer outreach, paid image generation and Factory STOP release remain separate gated workstreams.

## CI acceptance is deliberately synthetic only

GitHub workflow `.github/workflows/task330-supabase-auth-encrypted-snapshot-ci.yml` uses a disposable PostgreSQL 17 service with **fictional** Auth/intake/support rows and an ephemeral GPG fixture key, proves wrong-host denial, SHA-256 receipt integrity, GPG decryption, isolated local synthetic pg_restore and seven restored record-count checks. It has **no real Supabase credentials, production host access, private decryption key or real customer data**.

**Do not mark live backup, offhost custody, restore, reviewer permissions or operational queue COMPLETE from this CI alone.**
