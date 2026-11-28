#!/usr/bin/env bash
# Owner-only GPG-to-PG18 archive-format check: no database or file output.
# Source is delivered as base64 Bash stdin; gpg.exe must not consume it.
set -euo pipefail
fail() {
  case "${1:-PREFLIGHT_REFUSED}" in
    GPG_ONLY_REFUSED|PG_FORMAT_ONLY_REFUSED|BOTH_PROCESSES_REFUSED|PIPELINE_UNCLASSIFIED|PREFLIGHT_REFUSED) ;;
    *) set -- PREFLIGHT_REFUSED ;;
  esac
  printf 'TASK3B_WSL_PG_ARCHIVE=%s\n' "$1"
  exit 2
}
[[ $# -eq 2 ]] || fail PREFLIGHT_REFUSED
[[ -x /usr/lib/postgresql/18/bin/pg_restore ]] || fail PREFLIGHT_REFUSED
[[ "$(/usr/lib/postgresql/18/bin/pg_restore --version)" == *"PostgreSQL) 18."* ]] || fail PREFLIGHT_REFUSED
command -v cat >/dev/null 2>&1 || fail PREFLIGHT_REFUSED
command -v wslpath >/dev/null 2>&1 || fail PREFLIGHT_REFUSED
archive="$(printf '%s' "$1" | base64 -d)" || fail PREFLIGHT_REFUSED
[[ "$archive" == [A-Za-z]:\\* && "$archive" == *.gpg ]] || fail PREFLIGHT_REFUSED
gpg_windows="$(printf '%s' "$2" | base64 -d)" || fail PREFLIGHT_REFUSED
gpg_binary="$(wslpath -u "$gpg_windows")" || fail PREFLIGHT_REFUSED
[[ "$gpg_binary" == /mnt/* && "$gpg_binary" == *.exe && -x "$gpg_binary" ]] || fail PREFLIGHT_REFUSED
# Any authenticated plaintext is sent directly to the Linux null device.
# Suppress raw GPG stderr, paths, customer rows and binary stream.
# The PostgreSQL 18 archive TOC is checked with --list, WITHOUT any DB.
# The reader may stop after TOC. Drain the rest to /dev/null so GPG can
# complete its AEAD authentication without an artificial broken pipe.
set +e
"$gpg_binary" --batch --no-tty --decrypt --output - "$archive" </dev/null 2>/dev/null |
(
  /usr/lib/postgresql/18/bin/pg_restore --list >/dev/null 2>/dev/null
  pg_code=$?
  cat >/dev/null 2>/dev/null
  drain_code=$?
  [[ "$pg_code" == "0" && "$drain_code" == "0" ]]
)
pipeline=( "${PIPESTATUS[@]}" )
set -e
[[ "${#pipeline[@]}" -eq 2 ]] || fail PIPELINE_UNCLASSIFIED
if [[ "${pipeline[0]}" != "0" && "${pipeline[1]}" != "0" ]]; then fail BOTH_PROCESSES_REFUSED; fi
[[ "${pipeline[0]}" == "0" ]] || fail GPG_ONLY_REFUSED
[[ "${pipeline[1]}" == "0" ]] || fail PG_FORMAT_ONLY_REFUSED
printf '%s\n' 'TASK3B_WSL_PG_ARCHIVE=PASS_AUTHENTICATED_FORMAT'
