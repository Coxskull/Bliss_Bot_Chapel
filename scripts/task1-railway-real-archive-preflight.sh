#!/usr/bin/env bash
# ALPHA Task #1: READ-ONLY preflight of an OWNER-APPROVED, ALREADY DECRYPTED
# REAL PostgreSQL custom-format backup. Not a restore command.
# No networking, secrets, database connection, SQL execution, or production writes.
set -Eeuo pipefail
umask 077
export LC_ALL=C
fail() { printf 'TASK1_PREFLIGHT_BLOCKED: %s\n' "$1" >&2; exit 2; }

if [[ "${1:-}" != "--archive" || $# != 3 || "${3:-}" != "--local-only" ]]; then
  fail 'usage: bash scripts/task1-railway-real-archive-preflight.sh --archive /private/path/actual.dump --local-only'
fi
[[ "${TASK1_REAL_ARCHIVE_INSPECTION_APPROVED:-}" == "yes" ]] || fail 'separate authorization required even for inspecting a real archive'
# These values must come from the separately authenticated, owner-approved
# BACKUP MANIFEST, not merely by hashing the candidate file on demand.
[[ "${TASK1_REAL_SOURCE_PROJECT_REF:-}" =~ ^[a-z0-9]{20}$ ]] || fail 'verified source Supabase project ref from restricted manifest required'
expected_sha="${TASK1_REAL_ARCHIVE_SHA256_EXPECTED:-}"
[[ "$expected_sha" =~ ^[a-f0-9]{64}$ ]] || fail 'verified 64-digit archive SHA-256 from restricted manifest required'
archive="$2"
[[ -f "$archive" && ! -L "$archive" && -r "$archive" && -s "$archive" ]] || fail 'archive must be an existing private non-symlink regular file'
[[ "${archive##*/}" != *$'\n'* ]] || fail 'invalid filename'
for binary in dd stat pg_restore sha256sum mktemp grep cut chmod rm; do
  command -v "$binary" >/dev/null 2>&1 || fail "missing prerequisite: $binary"
done
# Never inspect a plaintext real backup that other users can read.
mode="$(stat -c %a -- "$archive")"
[[ "$mode" =~ ^[0-7]{3,4}$ ]] || fail 'could not establish archive permissions'
(( (8#$mode & 8#077) == 0 )) || fail 'archive has group/other access; restrict to owner first'
actual_sha="$(sha256sum -- "$archive" | cut -d ' ' -f 1)"
[[ "$actual_sha" == "$expected_sha" ]] || fail 'archive checksum does not match verified backup manifest'

# A Cloudflare D1 SQL/gzip export, .7z, or .age ciphertext is NOT a PGDMP.
# Do not decrypt an owner archive on Railway; owner keys stay on owner devices.
header="$(dd if="$archive" bs=5 count=1 status=none)"
[[ "$header" == "PGDMP" ]] || fail 'not a PostgreSQL custom archive; never run this through pg_restore'
bytes="$(stat -c %s -- "$archive")"
[[ "$bytes" =~ ^[0-9]+$ && "$bytes" -ge 100 ]] || fail 'implausibly small PostgreSQL archive'
toc="$(mktemp)" || fail 'could not allocate private TOC'
err="$(mktemp)" || { rm -f -- "$toc"; fail 'could not allocate private error file'; }
chmod 600 "$toc" "$err"
trap 'rm -f -- "$toc" "$err"' EXIT
if ! pg_restore --list "$archive" >"$toc" 2>"$err"; then
  fail 'pg_restore cannot read archive TOC; check format/version and corruption in the restricted workspace'
fi
[[ -s "$toc" ]] || fail 'archive TOC is empty'
objects="$(grep -c '^ *[0-9][0-9]*;' "$toc" || true)"
sensitive="$(grep -Eic '^[[:space:]]*[0-9]+;.*(EVENT TRIGGER|EXTENSION|FOREIGN SERVER|USER MAPPING|SUBSCRIPTION|PUBLICATION|PROCEDURAL LANGUAGE|ACL|DATABASE|FUNCTION|TRIGGER)' "$toc" || true)"
printf 'TASK1_READONLY_REAL_ARCHIVE_PREFLIGHT_PASS\n'
printf 'archive_size_bytes=%s\n' "$bytes"
printf 'archive_sha256=%s\n' "$actual_sha"
printf 'source_ref_attested=%s\n' "$TASK1_REAL_SOURCE_PROJECT_REF"
printf 'trusted_manifest_provenance=operator_must_verify_separately\n'
printf 'toc_object_count=%s\n' "$objects"
printf 'objects_requiring_manual_security_review=%s\n' "$sensitive"
printf 'owner_key_uploaded=no\ndatabase_connected=no\nrestore_executed=no\nfactory_stop_changed=no\n'
printf 'REAL_RESTORE_AUTHORIZED=no\n'
printf 'NEXT: verify source project and backup timestamp, approved isolated target, private-only ingress, egress restrictions, exact zero-new-spend quote, independent owner authorization, Supabase PG17 role/extension compatibility and encrypted evidence retention.\n'
