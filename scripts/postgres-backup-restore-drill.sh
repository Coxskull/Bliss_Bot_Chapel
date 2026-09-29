#!/usr/bin/env bash
# Local PostgreSQL backup/restore procedure drill.
# Refuses remote hosts. This does not certify a hosted production backup.
set -euo pipefail

if [[ "${BLISS_BACKUP_DRILL:-}" != "1" || "${BLISS_BACKUP_DRILL_TARGET:-}" != "local" ]]; then
  echo "Backup drill refused: set BLISS_BACKUP_DRILL=1 and BLISS_BACKUP_DRILL_TARGET=local." >&2
  exit 2
fi

host="${PGHOST:-localhost}"
case "$host" in
  localhost|127.0.0.1|::1) ;;
  *)
    echo "Backup drill refused: PGHOST must be a loopback host." >&2
    exit 2
    ;;
esac

for tool in psql pg_dump pg_restore createdb dropdb; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "Backup drill refused: ${tool} is not installed." >&2
    exit 2
  fi
done

run_pg() {
  if command -v sudo >/dev/null 2>&1 && sudo -n -u postgres true >/dev/null 2>&1; then
    sudo -n -u postgres "$@"
  else
    "$@"
  fi
}

db="bliss_backup_drill_$$"
dump="/tmp/${db}.dump"
cleanup() {
  run_pg dropdb --if-exists "$db" >/dev/null 2>&1 || true
  if [[ -f "$dump" ]]; then
    if command -v sudo >/dev/null 2>&1 && sudo -n true >/dev/null 2>&1; then
      sudo -n rm -f "$dump" || rm -f "$dump" || true
    else
      rm -f "$dump" || true
    fi
  fi
}
trap cleanup EXIT

marker="marker-$(date -u +%Y%m%dT%H%M%SZ)-$$"
run_pg createdb "$db"
run_pg psql -v ON_ERROR_STOP=1 -d "$db" -c "CREATE TABLE drill_marker (marker text PRIMARY KEY);"
run_pg psql -v ON_ERROR_STOP=1 -d "$db" -c "INSERT INTO drill_marker (marker) VALUES ('${marker}');"
run_pg pg_dump -Fc -d "$db" -f "$dump"
run_pg dropdb "$db"
run_pg createdb "$db"
run_pg pg_restore --no-owner --exit-on-error -d "$db" "$dump"
found="$(run_pg psql -v ON_ERROR_STOP=1 -d "$db" -tA -c "SELECT marker FROM drill_marker;")"
if [[ "$found" != "$marker" ]]; then
  echo "Backup drill failed: restored marker did not match." >&2
  exit 1
fi

echo "Backup drill restored marker ${marker}."
