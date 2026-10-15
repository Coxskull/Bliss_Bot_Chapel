# Hosted PostgreSQL TLS acceptance probe

This runbook performs a **read-only, explicitly opted-in** connection to the real hosted PostgreSQL endpoint. It uses libpq `sslmode=verify-full` and the provider-supplied CA file, rejects local/loopback targets, and reports only TLS metadata and a SHA-256 digest of the CA file. It never prints the database password or raw connection errors.

## Required environment

Set these in the deployment environment or secret manager; do not commit them or paste secrets into CI logs:

- `BLISS_HOSTED_TLS_ACCEPTANCE=1`
- `BLISS_HOSTED_TLS_TARGET=hosted`
- `BLISS_HOSTED_DB_HOST` — provider hostname matching the certificate SAN
- `BLISS_HOSTED_DB_PORT`
- `BLISS_HOSTED_DB_NAME`
- `BLISS_HOSTED_DB_USER` — a least-privilege account allowed to connect and read `pg_stat_ssl`
- `BLISS_HOSTED_DB_PASSWORD`
- `BLISS_PROVIDER_CA_FILE` — path to the provider's trusted CA bundle mounted into the execution environment

Install `psql` and `sha256sum` in that environment. Then run:

```bash
bash scripts/postgres-hosted-verifyfull-acceptance.sh
```

The script clears inherited libpq connection settings, sets `PGSSLMODE=verify-full`, sets `PGSSLROOTCERT` to the supplied CA bundle, applies a 10-second connection timeout and read-only transaction defaults, and issues only a query against `pg_stat_ssl`. A successful query is evidence that this client reached the configured endpoint, negotiated TLS, and passed libpq certificate-chain and hostname verification with the supplied CA.

## Interpretation and limits

- Run this from the same network/runtime context that will host the application, using the real provider endpoint and CA.
- Keep the generated output with the deployment change record; it includes a UTC timestamp, TLS version/cipher, and CA-file digest but no endpoint, username, or secret.
- A failed connection emits a generic failure message. Diagnose privately in the hosting environment without publishing credentials or raw connection strings.
- This is **not** a backup/restore drill, identity-provider test, application migration test, or full hosted-production acceptance. It must not change `CampaignReady=false` or `Delivery=NOT_SENT` by itself.
- No hosted run is claimed until the script is actually executed against the provider endpoint and the resulting evidence is reviewed.
