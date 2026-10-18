#!/usr/bin/env bash
# Disposable PostgreSQL 17 only. Never point this script at a live project.
set -euo pipefail
: "${PGHOST:?set isolated PGHOST}"
: "${PGPORT:?set isolated PGPORT}"
: "${PGDATABASE:?set isolated PGDATABASE}"
: "${PGUSER:?set isolated PGUSER}"
: "${PGPASSWORD:?set isolated PGPASSWORD}"
if [[ "${PGHOST}" != "127.0.0.1" || "${PGDATABASE}" != "postgres" || "${PGUSER}" != "postgres" ]]; then
  echo "REFUSED: real event-trigger test requires localhost disposable postgres service" >&2
  exit 1
fi
if ! command -v psql >/dev/null; then
  echo "psql missing on runner" >&2
  exit 1
fi
# PostgreSQL service is only visible on this runner. Never log credentials.
result="$(psql -X -A -t -v ON_ERROR_STOP=1 -f tests/lead-containment/real-event-trigger-driver.sql)"
printf '%s\n' "$result" | grep -F "PASS: PostgreSQL 17 event trigger auto-RLS"
count() { printf '%s\n' "$result" | grep -Fxc "$1" || true; }
if [[ "$(count 'rls_auto_enable|f')" -ne 1 || "$(count 'set_updated_at|f')" -ne 1 ]]; then
  echo "FAILED: baseline did not flag both function risks" >&2; exit 1
fi
if [[ "$(count 'rls_auto_enable|t')" -ne 2 || "$(count 'set_updated_at|t')" -ne 2 ]]; then
  echo "FAILED: exact post-change catalog acceptance not 2/2 twice" >&2; exit 1
fi
echo "PASS: before 0/2, after 2/2, reapply 2/2 real PostgreSQL catalog acceptance"
echo "NOT TESTED: production database, production RLS migrations, customer integrations, incident attribution"
