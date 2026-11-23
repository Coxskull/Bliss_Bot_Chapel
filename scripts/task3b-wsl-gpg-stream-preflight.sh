#!/usr/bin/env bash
# Owner-only WSL/Gpg4win binary stream diagnostic. No PG, DB, disk output or key export.
# Source is delivered as base64 Bash stdin; gpg.exe must not consume it.
set -euo pipefail
fail() { printf '%s\n' 'TASK3B_WSL_GPG_STREAM=REFUSED_OR_INTEROP_UNVERIFIED'; exit 2; }
[[ $# -eq 2 ]] || fail
command -v wslpath >/dev/null 2>&1 || fail
archive="$(printf '%s' "$1" | base64 -d)" || fail
[[ "$archive" == [A-Za-z]:\\* && "$archive" == *.gpg ]] || fail
gpg_windows="$(printf '%s' "$2" | base64 -d)" || fail
gpg_binary="$(wslpath -u "$gpg_windows")" || fail
[[ "$gpg_binary" == /mnt/* && "$gpg_binary" == *.exe && -x "$gpg_binary" ]] || fail
# Any authenticated plaintext is sent directly to the Linux null device.
# Suppress raw GPG stderr, paths, customer rows and binary stream.
"$gpg_binary" --batch --no-tty --decrypt --output - "$archive" </dev/null >/dev/null 2>/dev/null || fail
printf '%s\n' 'TASK3B_WSL_GPG_STREAM=PASS'
