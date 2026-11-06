# Bliss Bot — Stage 5B: Verify live PostgreSQL TLS over the private Railway tunnel

**Date:** 2026-11-05. **Scope:** TLS-only handshake; no database password, SQL query, `pg_dump`, restore, network exposure, app restart, or plan change.

### Confirmed status before this step

- Owner Ubuntu screenshot shows `RAILWAY_PUBLIC_CA_FILE_VALID` after authenticated `railway ssh` retrieved only the PUBLIC `/var/lib/postgresql/data/certs/root.crt` from the existing `bliss-bot / production / Postgres` service.
- Stage 4B encrypted recovery-key archive was downloaded from owner-controlled OneDrive Personal Vault. The owner screenshot shows `ENCRYPTED_VAULT_DOWNLOAD_PRESENT`, `OFFDEVICE_ENCRYPTED_BYTES_MATCH` and `RECOVERY_KEY_OFFDEVICE_COPY_VERIFIED` on the existing workstation.
- The database source is `ghcr.io/railwayapp-templates/postgres-ssl:18`, with its certificate named `localhost`. SSL client certificate chain *and hostname* are not yet tested against the live forwarded service.
- Read-only Railway connector confirmed no public TCP proxy; continue via the already-authenticated Railway CLI SSH tunnel in the FIRST Ubuntu terminal.

### On your SECOND Ubuntu terminal

Do not show the Railway first terminal output; it can include database credentials. **Read only the local TCP port number** selected by the private tunnel. If the tunnel has closed, restart it from your linked project (`railway connect --environment production --tunnel-only Postgres`) and leave it open. Do not add the unsupported `--port` option.

Run **one complete line at a time**, with the normal `$` prompt, not the `>` continuation prompt:

```bash
umask 077
RAILWAY_CA="$HOME/bliss-backup-operator/trust/railway-postgres-root.crt"
test -s "$RAILWAY_CA" && openssl x509 -in "$RAILWAY_CA" -noout && echo RAILWAY_PUBLIC_CA_FILE_VALID
read -r -p 'Enter the local SSH tunnel PORT number (digits only): ' TUNNEL_PORT
[[ "$TUNNEL_PORT" =~ ^[0-9]+$ ]] && (( TUNNEL_PORT >= 1 && TUNNEL_PORT <= 65535 )) && echo LOCAL_TUNNEL_PORT_FORMAT_OK
```

If the port check does not print its pass marker, **STOP** before connecting. The local port is not a secret, but do not share the tunnel's printed password or database connection URL.

Now check the *actual TLS server certificate* using OpenSSL's PostgreSQL-specific startup handshake. This sends **no PostgreSQL username, password or SQL query**:

```bash
TLS_LOG="$HOME/bliss-backup-operator/trust/tls-handshake-check.log"
timeout 15s openssl s_client -starttls postgres -connect "127.0.0.1:$TUNNEL_PORT" -servername localhost -CAfile "$RAILWAY_CA" -verify_hostname localhost -verify_return_error -brief </dev/null > "$TLS_LOG" 2>&1
grep -E 'CONNECTION ESTABLISHED|Verification: OK|verify error|error:' "$TLS_LOG"
```

The OpenSSL test should report **`CONNECTION ESTABLISHED`** and **`Verification: OK`**. Only then evaluate:

```bash
if grep -Fq 'CONNECTION ESTABLISHED' "$TLS_LOG" && grep -Fq 'Verification: OK' "$TLS_LOG"; then echo RAILWAY_TLS_HOSTNAME_CA_VERIFIED; else echo RAILWAY_TLS_NOT_VERIFIED; fi
```

An OpenSSL client may subsequently report an EOF when the unauthenticated TLS probe closes without a PostgreSQL startup packet. Do not confuse the TLS certificate verification markers with a fully authenticated PostgreSQL session. This check proves the TLS certificate chain/hostname of the endpoint reachable through the trusted tunnel; it **does not** prove a database identity, database role, or backup privileges.

**Stop if `Verification: OK` is absent**, if the certificate expired or does not match `localhost`, or if the connection cannot be made. Do not retry with `-verify_return_error` omitted, `sslmode=require`, `sslmode=disable`, another CA, a public TCP proxy, or an unverified host.

**Operator reply (no secrets):** `RAILWAY_TLS_HOSTNAME_CA_VERIFIED`, or `RAILWAY_TLS_NOT_VERIFIED` plus a redacted error category. Do not paste `railway connect` connection details, private keys, passwords, connection URLs, cookies, or OAuth tokens.

### Subsequent Stage 5C, not covered by this probe

After Stage 5B, prepare a protected `0600` PostgreSQL passfile and connect with libpq `PGHOST=localhost`, `PGHOSTADDR=127.0.0.1`, real CLI tunnel `PGPORT`, `PGSSLMODE=verify-full`, `PGSSLROOTCERT=$RAILWAY_CA`; verify approved database name, SQL server cluster/system identifier, export-capable role, and least privilege via **read-only SQL** with no plaintext output. The existing draft operator script must independently check these before the first authorized `pg_dump`→`age` encrypted export. Real database backup, checksum and isolated restoration remain separate release gates.

Official references: [OpenSSL s_client STARTTLS and hostname verification](https://docs.openssl.org/3.5/man1/openssl-s_client/), [PostgreSQL 18 SSL client verification](https://www.postgresql.org/docs/18/libpq-ssl.html), [Railway SSL Postgres template](https://github.com/railwayapp-templates/postgres-ssl).