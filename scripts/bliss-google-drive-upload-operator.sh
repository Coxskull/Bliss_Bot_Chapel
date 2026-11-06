#!/usr/bin/env bash
# Upload only pre-encrypted Bliss Bot PostgreSQL archive + integrity manifest.
# Owner-configured rclone Google Drive OAuth is required on the operator's machine.
set -Eeuo pipefail
umask 077
fail() { printf 'BLISS DRIVE UPLOAD REFUSED: %s\n' "$1" >&2; exit 1; }
[[ "$#" -eq 1 ]] || fail 'specify exactly one encrypted archive (.age)'
[[ "${BLISS_DRIVE_UPLOAD_APPROVED:-}" == YES ]] || fail 'explicit owner upload approval is required'
[[ "${CI:-}" != true && "${GITHUB_ACTIONS:-}" != true ]] || fail 'production upload is prohibited from CI'
folder_id='1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo'
[[ "${BLISS_DRIVE_FOLDER_ID:-}" == "$folder_id" ]] || fail 'target must be the approved Bliss encrypted backup Drive subfolder'
remote="${BLISS_DRIVE_RCLONE_REMOTE:-}"
[[ "$remote" =~ ^[a-zA-Z][a-zA-Z0-9_-]{0,50}$ ]] || fail 'invalid configured rclone remote name'
config="${RCLONE_CONFIG:-}"
[[ -n "$config" && -f "$config" && ! -L "$config" ]] || fail 'private rclone configuration is required'
[[ "$(stat -c '%a' "$config")" == 600 && "$(stat -c '%u' "$config")" == "$(id -u)" ]] || fail 'rclone configuration must belong to the operator and have mode 600'
# Reject a local, alias, or shared remote accidentally configured in place of Google Drive.
awk -v remote="$remote" '
  /^\[/ { active = ($0 == "[" remote "]") }
  active && /^[[:space:]]*type[[:space:]]*=[[:space:]]*drive[[:space:]]*$/ { good=1 }
  END { if (!good) exit 1 }
' "$config" || fail 'rclone remote must be the Google Drive backend'
command -v rclone >/dev/null 2>&1 || fail 'rclone is not installed on the operator workstation'
command -v sha256sum >/dev/null 2>&1 || fail 'sha256sum is missing'
archive="$1"
[[ -f "$archive" && ! -L "$archive" ]] || fail 'encrypted archive must be a regular file (no symlinks)'
archive="$(realpath -e -- "$archive")" || fail 'encrypted archive path does not resolve'
dir="$(dirname -- "$archive")"
[[ "$(stat -c '%a' "$dir")" == 700 && "$(stat -c '%u' "$dir")" == "$(id -u)" ]] || fail 'local encrypted backup directory must be operator owned, mode 700'
[[ "$(stat -c '%a' "$archive")" == 600 && "$(stat -c '%u' "$archive")" == "$(id -u)" ]] || fail 'encrypted archive must be operator owned, mode 600'
file="$(basename -- "$archive")"
[[ "$file" =~ ^bliss-backup-[0-9]{8}T[0-9]{6}Z-[a-zA-Z0-9]{8}\.age$ ]] || fail 'file name does not match Bliss operator backup output'
[[ -s "$archive" ]] || fail 'encrypted archive is empty'
[[ "$(head -c 21 -- "$archive")" == 'age-encryption.org/v1' ]] || fail 'file is not an age-encrypted archive'
manifest="${archive}.sha256"
[[ -f "$manifest" && ! -L "$manifest" ]] || fail 'matching checksum manifest is required'
[[ "$(stat -c '%a' "$manifest")" == 600 && "$(stat -c '%u' "$manifest")" == "$(id -u)" ]] || fail 'checksum manifest must be operator owned, mode 600'
expected="$(sha256sum -- "$archive" | cut -d' ' -f1)"
[[ "$(cat -- "$manifest")" == "$expected  $file" ]] || fail 'manifest does not exactly match this encrypted archive'
(
  cd -- "$dir"
  sha256sum --check -- "$file.sha256" >/dev/null
) || fail 'local archive checksum did not verify'

# All remote operations pin the exact folder ID, avoiding path/name ambiguity.
rclone_drive() { rclone --config "$config" --drive-root-folder-id "$folder_id" "$@"; }
rclone_drive lsf "$remote:" --files-only >/dev/null || fail 'Drive folder not accessible using approved remote'
rclone_drive copyto --immutable -- "$archive" "$remote:$file" >/dev/null || fail 'encrypted backup upload failed'
remote_hash="$(rclone_drive cat "$remote:$file" | sha256sum | cut -d' ' -f1)" || fail 'encrypted remote readback failed'
[[ "$remote_hash" == "$expected" ]] || fail 'uploaded encrypted bytes differ from local archive'
rclone_drive copyto --immutable -- "$manifest" "$remote:$file.sha256" >/dev/null || fail 'checksum manifest upload failed'
local_manifest_hash="$(sha256sum -- "$manifest" | cut -d' ' -f1)"
remote_manifest_hash="$(rclone_drive cat "$remote:$file.sha256" | sha256sum | cut -d' ' -f1)" || fail 'remote checksum manifest readback failed'
[[ "$remote_manifest_hash" == "$local_manifest_hash" ]] || fail 'uploaded checksum manifest differs from local manifest'
printf 'DRIVE_ENCRYPTED_ARCHIVE_READBACK_PASS: %s\n' "$file"
printf 'SOURCE_RESTORE_AND_RETENTION_NOT_VERIFIED; KEEP_PRODUCTION_LOCKED\n'
