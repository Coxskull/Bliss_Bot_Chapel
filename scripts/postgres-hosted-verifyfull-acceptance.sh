#!/usr/bin/env bash
# Read-only hosted PostgreSQL TLS acceptance probe.
# This validates the configured endpoint only; it is not full hosted acceptance.
set -euo pipefail
set +x

if [[ "${BLISS_HOSTED_TLS_ACCEPTANCE:-}" != "1" || "${BLISS_HOSTED_TLS_TARGET:-}" != "hosted" ]]; then
  echo "Hosted TLS acceptance refused: explicit hosted opt-in is required." >&2
  exit 2
fi

for name in BLISS_HOSTED_DB_HOST BLISS_HOSTED_DB_PORT BLISS_HOSTED_DB_NAME BLISS_HOSTED_DB_USER BLISS_HOSTED_DB_PASSWORD BLISS_PROVIDER_CA_FILE; do
  if [[ -z "${!name:-}" ]]; then
    echo "Hosted TLS acceptance refused: required setting ${name} is missing." >&2
    exit 2
  fi
done

for tool in psql sha256sum; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "Hosted TLS acceptance refused: ${tool} is not installed." >&2
    exit 2
  fi
done

host="$BLISS_HOSTED_DB_HOST"
port="$BLISS_HOSTED_DB_PORT"
case "${host,,}" in
  localhost|127.*|::1|0.0.0.0|host.docker.internal)
    echo "Hosted TLS acceptance refused: loopback/local hosts are not hosted targets." >&2
    exit 2
    ;;
esac
if [[ "$host" =~ [[:space:]] || ! "$port" =~ ^[0-9]{1,5}$ ]] || (( port < 1 || port > 65535 )); then
  echo "Hosted TLS acceptance refused: host or port format is invalid." >&2
  exit 2
fi
if [[ ! -s "$BLISS_PROVIDER_CA_FILE" || ! -r "$BLISS_PROVIDER_CA_FILE" ]]; then
  echo "Hosted TLS acceptance refused: provider CA file is missing, empty, or unreadable." >&2
  exit 2
fi

# Ignore ambient libpq settings so this probe uses only the explicit inputs below.
unset PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD PGSSLMODE PGSSLROOTCERT PGOPTIONS PGCONNECT_TIMEOUT || true
export PGHOST="$host"
export PGPORT="$port"
export PGDATABASE="$BLISS_HOSTED_DB_NAME"
export PGUSER="$BLISS_HOSTED_DB_USER"
export PGPASSWORD="$BLISS_HOSTED_DB_PASSWORD"
export PGSSLMODE=verify-full
export PGSSLROOTCERT="$BLISS_PROVIDER_CA_FILE"
export PGCONNECT_TIMEOUT=10
export PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=10000'

tmp="$(mktemp)"
trap 'rm -f "$tmp"; unset PGPASSWORD || true' EXIT

# libpq verify-full must validate both the chain against the supplied CA and the hostname.
# The only SQL executed is a read-only inspection of this connection's TLS session.
if ! psql --no-psqlrc --set=ON_ERROR_STOP=1 --quiet --tuples-only --no-align \
  --command "SELECT ssl::text || '|' || COALESCE(version, '') || '|' || COALESCE(cipher, '') FROM pg_stat_ssl WHERE pid = pg_backend_pid();" \
  >"$tmp" 2>/dev/null; then
  echo "Hosted TLS acceptance failed: connection or certificate/hostname verification was refused." >&2
  exit 1
fi

result="$(cat "$tmp")"
IFS='|' read -r ssl tls_version cipher <<< "$result"
if [[ "$ssl" != "t" || -z "$tls_version" || -z "$cipher" ]]; then
  echo "Hosted TLS acceptance failed: the server did not report an active TLS session." >&2
  exit 1
fi

ca_sha256="$(sha256sum "$BLISS_PROVIDER_CA_FILE" | awk '{print $1}')"
printf '%s\n' \
  "timestamp_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  "target=hosted" \
  "database_tls_connection=passed" \
  "server_hostname_verification=passed" \
  "ssl=true" \
  "tls_version=$tls_version" \
  "cipher=$cipher" \
  "provider_ca_file_sha256=$ca_sha256" \
  "query_mode=read-only" \
  "identity_provider_contacted=no" \
  "backup_drill=not_run" \
  "full_hosted_acceptance=not_claimed" \
  "delivery=NOT_SENT"
