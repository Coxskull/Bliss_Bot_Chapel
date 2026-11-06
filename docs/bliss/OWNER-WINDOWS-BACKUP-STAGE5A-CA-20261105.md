# Bliss Bot — Stage 5A: Read-only PostgreSQL TLS root certificate preflight

**Date:** 2026-11-05. **Owner checkpoint:** `OFFDEVICE_ENCRYPTED_BYTES_MATCH` and `RECOVERY_KEY_OFFDEVICE_COPY_VERIFIED` both visible in the owner's Ubuntu screenshot after re-downloading the encrypted identity backup from OneDrive Personal Vault. The owner should retain the recovery passphrase **separately**.

**This is read-only certificate retrieval.** No SQL client connection, production dump, data export, database write, public proxy, deployment, subscription or Cloudflare change.

## Verified infrastructure and why this step matters

Railway's connected metadata for `bliss-bot` production `Postgres` confirms:
- Project `fb33c1b7-03fb-42c8-89b1-081951a738fd`
- Service `6aa78407-1632-48c9-8ea4-0289a7d36a5b`
- Deployed `ghcr.io/railwayapp-templates/postgres-ssl:18`, deployment success, 5GB attached volume, **no public TCP proxy** and no pending staged change.

The stock image's source (https://github.com/railwayapp-templates/postgres-ssl/blob/main/init-ssl.sh) explicitly creates a self-signed root CA at `/var/lib/postgresql/data/certs/root.crt` and a server certificate for **DNS:localhost**. The intended operator connection is therefore:

- Authenticated Railway CLI SSH tunnel open in Terminal 1
- `PGHOST=localhost` for certificate hostname verification
- `PGHOSTADDR=127.0.0.1` for network routing to the SSH tunnel
- `PGPORT=<actual Railway tunnel local port>`
- `PGSSLMODE=verify-full`, `PGSSLROOTCERT=<public root.crt obtained below>`

Do **not** use `sslmode=disable` or `sslmode=require` as a workaround for trust failure. In this image, `verify-full` against the *Railway internal DNS name* would fail because the certificate SAN is `localhost`; correcting the client configuration is a security fix, not a TLS relaxation.

## Owner actions: extract the PUBLIC root certificate only

Keep your **first Ubuntu terminal with `railway connect ... --tunnel-only` running**. In your second Ubuntu terminal, enter these commands **one per line** at a normal `$` prompt:

```bash
umask 077
install -d -m 700 "$HOME/bliss-backup-operator/trust"
export RAILWAY_CA="$HOME/bliss-backup-operator/trust/railway-postgres-root.crt"
railway ssh --project fb33c1b7-03fb-42c8-89b1-081951a738fd --service 6aa78407-1632-48c9-8ea4-0289a7d36a5b --environment production -- cat /var/lib/postgresql/data/certs/root.crt > "$RAILWAY_CA"
test -s "$RAILWAY_CA" && chmod 600 "$RAILWAY_CA"
openssl x509 -in "$RAILWAY_CA" -noout >/dev/null && echo RAILWAY_PUBLIC_CA_FILE_VALID
```

Only the **public `root.crt`** is retrieved. **Never** retrieve `root.key`, `server.key`, `PGPASSWORD`, `DATABASE_URL` or plaintext customer data. Do not paste the full Railway tunnel connection output, any password or OAuth code.

If `railway ssh` reports no SSH key in the second terminal, initialize the agent and load your **existing** key, without replacing it:

```bash
eval "$(ssh-agent -s)"
ssh-add "$HOME/.ssh/id_ed25519"
```

Retry the exact `railway ssh ... cat` command above only after authenticated SSH is restored. If the CA is missing, the service identity looks wrong, or `openssl x509` fails, **stop**; do not create a self-signed substitute locally or bypass SSL verification.

On success you may privately inspect the public certificate fingerprint and expiry with:

```bash
openssl x509 -in "$RAILWAY_CA" -noout -subject -dates
sha256sum "$RAILWAY_CA"
```

SHA-256 is of the **public CA file** and is not a password or database secret, but sharing the full terminal scrollback is still discouraged. The reviewed backup operator script will require an explicit SHA-256 pin of this particular CA file and the exact Railway project, service, CLI tunnel port and PostgreSQL cluster identity. A changed CA requires a new authenticated inspection, **not** a TLS downgrade.

## What to report

Tell the engineering assistant **only**:

```
RAILWAY_PUBLIC_CA_FILE_VALID
```

If unsuccessful, give just the redacted error category: `SSH_UNAVAILABLE`, `CA_PATH_MISSING`, `INVALID_CERTIFICATE`, or `OTHER`, without secrets or whole-terminal screenshots.

After this checkpoint, separately verify the CLI-reported tunnel port, TLS `verify-full` with authorized credentials, real database name/user/system identifier, 0600 libpq passfile and a future isolated restore target. No `pg_dump` is permitted by this Stage 5A runbook. The production backup and public customer registration remain blocked pending real encrypted backup, independently verified remote SHA-256 and nonproduction restore.

Sources: [Railway SSH CLI docs](https://docs.railway.com/cli/ssh), [Railway PostgreSQL SSL image code](https://github.com/railwayapp-templates/postgres-ssl/blob/main/init-ssl.sh), [PostgreSQL 18 SSL client verification](https://www.postgresql.org/docs/18/libpq-ssl.html).
