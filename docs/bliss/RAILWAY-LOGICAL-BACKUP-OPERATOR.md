# Bliss Bot: owner-approved logical recovery without Railway Pro

**Status (2026-11-05): candidate only; not executed against hosted data.** Parent: [Bliss Bot #302](https://github.com/Coxskull/Bliss_Bot_Chapel/issues/302). The Railway `bliss-bot` production Postgres Backups tab explicitly shows **No Backups**, no schedules and PITR disabled under the Hobby plan. This is a hard production-recovery blocker. The earlier Phase 4E GitHub Actions encrypted dump/restore is *synthetic only*.

This separate operator script can produce a **client-side-encrypted custom-format logical PostgreSQL 18 backup** once approved network access and private storage exist. It does **not** set up a schedule, offsite replication, retention, monitoring or PITR. Saving a local `.age` file does not complete the recovery gate; a real independent destination and isolated restore must be verified.

## Non-negotiable prerequisites

1. Owner approves this **exact** source (`bliss-bot` / production / `Postgres`), the identity of the person executing it, the approved encrypted destination, a **distinct** offsite copy location, security roles, retention/deletion policy and measured restore criteria. Do not copy customer data into GitHub, Sheets, chat, or CI artifacts. Do not purchase Railway Pro or expose a public Postgres TCP proxy simply to run a backup.
2. Use an operator-controlled runtime with legitimate network access to **the existing private Railway Postgres**. The connected Railway chat tools do not provide a SQL shell or a backup-snapshot API. This repository cannot make a live backup without an authorized network path. Do not bypass Railway authentication or abuse `PGHOST`/proxy settings. A controlled one-off job is only acceptable if it can use existing paid-for capacity, has a reviewed source and no unwanted service/billing changes.
3. Install PostgreSQL **18** client binaries (`psql`, `pg_dump`), `age`, and `sha256sum`/`flock`/`mktemp`. Use verify-full TLS with the Railway database CA file for production. Configure a read-only/export-capable database role; do not grant excessive roles merely to make an export work. Existing credentials are never copied into this repository.
4. Place libpq credentials in an owner-owned **0600 passfile** outside the working repository. Do not set `PGPASSWORD` or put a password in an argument, log or config committed to Git. Use a separate **age public recipient**. Keep its **private decryption identity** separately protected, backed up and tested by the approved recovery custodians.
5. Pre-create an owner-controlled **0700 destination directory** outside temporary directories, live database volumes, and Git repositories. The script writes encrypted `.age` and `.sha256` only, never an unencrypted dump. This does not automatically copy to an offsite destination.
6. Read the expected PostgreSQL server system identifier from a separate, authorized, read-only session; pin it with expected host, DB name and user before allowing execution. Obtain separate approval for a **real** backup and separate approval before a **real backup restore**.

## Private Railway CLI tunnel — no public database TCP proxy

Railway's currently documented `railway connect --tunnel-only` opens an authenticated **local SSH tunnel** to the existing service, even when Postgres has no public TCP proxy. This is a **candidate owner-run network path**; the connected ChatGPT Railway tool does not run the local CLI or provide your machine's filesystem. Validate network access and costs before any real export. Documentation: https://docs.railway.com/cli/connect and https://docs.railway.com/cli/ssh.

On an approved operator workstation, with Railway CLI already installed and the correct owner signed in (do not paste a token or password into chat):

```bash
railway link --project fb33c1b7-03fb-42c8-89b1-081951a738fd --environment production
railway connect Postgres --environment production --tunnel-only --port 55432
```

Leave the tunnel process open. It does not change Railway's public networking or the PostgreSQL service configuration. In a **separate** private shell, libpq can dial `PGHOSTADDR=127.0.0.1` and `PGPORT=55432` while keeping `PGHOST` as the **actual TLS certificate hostname** approved from the source's connection settings. PostgreSQL `verify-full` uses `PGHOST` for hostname verification when `PGHOSTADDR` supplies the network address; do not substitute `localhost` for an unmatched certificate, disable TLS, or set an untrusted CA. Reference: https://www.postgresql.org/docs/current/libpq-connect.html.

This connection path still requires a genuine, correctly scoped 0600 passfile, the owner-pinned database name/user/cluster ID, verified CA, an `age` recipient and an owner-approved 0700 encrypted output directory. Use the **local tunnel port** in the passfile. Stop if the correct certificate/host identity cannot be verified. Do not create a Railway public TCP proxy as a workaround; any network egress or CLI usage charges require owner cost review.

## Preflight (no network access)

Set the variables in a private interactive shell (example placeholders are intentionally non-operational):

```bash
export BLISS_BACKUP_ENVIRONMENT=production
export BLISS_BACKUP_EXECUTION_APPROVED=YES                 # only after explicit owner signoff
export PGHOST='<approved-private-Railway-Postgres-host>'
export PGPORT=5432                              # or 55432 for an approved private SSH tunnel
# For the approved SSH tunnel only: export PGHOSTADDR=127.0.0.1
export PGDATABASE='<approved-db-name>'
export PGUSER='<approved-readonly-backup-role>'
export PGSSLMODE=verify-full
export PGSSLROOTCERT='/path/to/approved/railway-postgres-ca.pem'
export PGPASSFILE='/path/to/private/pgpass'               # chmod 600; NOT in repo/backup dir
export BLISS_BACKUP_EXPECTED_HOST="$PGHOST"
export BLISS_BACKUP_EXPECTED_DATABASE="$PGDATABASE"
export BLISS_BACKUP_EXPECTED_USER="$PGUSER"
export BLISS_BACKUP_EXPECTED_SYSTEM_ID='<pinned-numeric-pg-system-id>'
export BLISS_BACKUP_ENCRYPTION_RECIPIENT='<owner-age1-public-recipient>'
export BLISS_BACKUP_DESTINATION='/owner-controlled/secure-output-directory'  # chmod 700
unset PGPASSWORD
bash scripts/bliss-logical-backup-operator.sh --preflight
```

`--preflight` checks local tools/configuration and destination permissions **without connecting to PostgreSQL** or writing an archive. It is not proof that the source is reachable. If the operator environment cannot reach the existing private service, **stop** and document that blocker; do not open the database publicly.

## Explicit execution (approval and private access already verified)

```bash
bash scripts/bliss-logical-backup-operator.sh --execute
```

The script verifies the live PostgreSQL cluster ID, database name, role and major version using metadata-only queries before export, requires matching PostgreSQL 18 client tools, locks the private destination against concurrent runs, streams `pg_dump -Fc` directly to `age` (no plaintext dump on disk), checks SHA-256 and file permissions, and deletes partial artifacts on failures. Success prints `LOCAL_ENCRYPTED_BACKUP_CREATED` and **`OFFSITE_COPY_NOT_VERIFIED`**; never interpret that as a completed backup policy or restoration test.

## Acceptance after export (separate authorized procedure)

1. Independently verify encrypted artifact and checksum against the approved destination; copy encrypted bytes **and** manifest to a physically/logically independent, access-controlled location. Do not store the private decryption identity together with backups. Record size, timestamps and media locations in a restricted operations record, without database rows or credentials.
2. Restore a **copy** into a distinct, isolated PostgreSQL 18 instance confirmed to be nonproduction and empty. Before `pg_restore`, verify both the target environment identity and source backup checksum. Decrypt only into protected ephemeral storage; remove plaintext after the test. Do not run `pg_restore` against the live Railway database or use `--clean` on a production connection.
3. Record schema/object and row-count checks, unique keys, application migration history, consent receipt counts, checksum/content checks and measured restore time (RTO). Track the time between source snapshot and restore point (RPO). Use sanitized aggregates only in shared evidence.
4. Configure the actual recurring export schedule, alerts for failures/missed runs, least-privilege rotation, backup retention/expiry, independent copy and periodic restores. **No native point-in-time recovery:** a daily logical dump gives roughly a 24-hour-or-worse RPO depending on failures and scheduling.
5. Do not mark the public site/advertiser/creator intake **GO** until all recovery, auth, ingress, migrations, privacy, consent and commercial gates are independently accepted.

## Synthetic CI proof (not live data)

The existing Phase 4E workflow exercises a disposable PostgreSQL 18 cluster with an encrypted dump, a distinct restoration cluster, integrity checks and negative security tests. `tests/bliss-logical-backup-operator-ci.sh` additionally runs this opt-in operator script **only** in a pinned local CI fixture (no real Railway credentials or host). Production-mode execution from GitHub Actions is explicitly rejected. This does not test hosted Railway credentials, offsite retention, real network reachability, or recovery of real records.
