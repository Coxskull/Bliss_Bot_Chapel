#!/usr/bin/env bash
# Owner-only decrypted PGDMP magic check. No database, output file or key export.
# Source is delivered as base64 Bash stdin; gpg.exe must not consume it.
set -euo pipefail
fail() { printf '%s\n' 'TASK3B_WSL_PGDMP=REFUSED_OR_INTEROP_UNVERIFIED'; exit 2; }
[[ $# -eq 2 ]] || fail
command -v wslpath >/dev/null 2>&1 || fail
archive="$(printf '%s' "$1" | base64 -d)" || fail
[[ "$archive" == [A-Za-z]:\\* && "$archive" == *.gpg ]] || fail
gpg_windows="$(printf '%s' "$2" | base64 -d)" || fail
gpg_binary="$(wslpath -u "$gpg_windows")" || fail
[[ "$gpg_binary" == /mnt/* && "$gpg_binary" == *.exe && -x "$gpg_binary" ]] || fail
# Any authenticated plaintext is sent directly to the Linux null device.
# Suppress raw GPG stderr, paths, customer rows and binary stream.
# Stream exact five-byte custom archive signature, then drain remaining binary
# bytes to /dev/null so authenticated GPG completion is independently checked.
# The signature is compared internally; no decrypted data is displayed or saved.
set +e
"$gpg_binary" --batch --no-tty --decrypt --output - "$archive" </dev/null 2>/dev/null |
(
  header="$(head -c 5 2>/dev/null)"
  header_code=$?
  cat >/dev/null 2>/dev/null
  drain_code=$?
  [[ "$header_code" == "0" && "$drain_code" == "0" ]] || exit 23
  [[ "$header" == "PGDMP" ]] || exit 22
)
pipe=( "${PIPESTATUS[@]}" )
set -e
[[ "${#pipe[@]}" -eq 2 ]] || fail
[[ "${pipe[0]}" == 0 ]] || fail
if [[ "${pipe[1]}" == 22 ]]; then
  printf '%s\n' 'TASK3B_WSL_PGDMP=NOT_PGDMP'
  exit 2
fi
[[ "${pipe[1]}" == 0 ]] || fail
printf '%s\n' 'TASK3B_WSL_PGDMP=PASS_PGDMP_AND_AUTHENTICATION'
