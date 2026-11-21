#!/usr/bin/env bash
set -Eeuo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
script="$root/scripts/phase4c-nonprod-backup.sh"
test -f "$script"
bash -n "$script"
set +e
output="$(BACKUP_ENVIRONMENT=production BACKUP_DATABASE_URL=ignored BACKUP_OUTPUT_DIR=/tmp BACKUP_ENCRYPTION_RECIPIENT=ignored bash "$script" 2>&1)"
code=$?
set -e
if [[ "$code" -eq 0 || "$output" != *"BACKUP FAILED: Disposable GitHub Actions mode required"* ]]; then
  echo "FAIL: production mode did not fail closed with the expected safety error" >&2
  exit 1
fi
echo "PASS: shell syntax and production-environment guard"
