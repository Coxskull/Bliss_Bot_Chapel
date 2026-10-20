#!/usr/bin/env bash
set -Eeuo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
script="$root/scripts/phase4c-nonprod-backup.sh"
test -f "$script"
bash -n "$script"
set +e
BACKUP_ENVIRONMENT=production BACKUP_DATABASE_URL=ignored BACKUP_OUTPUT_DIR=/tmp BACKUP_ENCRYPTION_RECIPIENT=ignored bash "$script" >/dev/null 2>&1
code=$?
set -e
test "$code" -eq 64 || { echo "FAIL: production environment not refused"; exit 1; }
echo "PASS: shell syntax and production-environment guard"
