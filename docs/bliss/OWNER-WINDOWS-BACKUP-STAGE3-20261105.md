# Bliss Bot — Stage 3: encrypted Google Drive live transport canary (synthetic only)

**Date:** 2026-11-05. **Status:** prepared, NOT yet executed from the owner's Windows/Ubuntu workstation.

Prerequisites confirmed by owner: `LOCAL_AGE_ENCRYPTION_TEST_PASS` and `DRIVE_READ_ONLY_LIST_COMPLETE`. Existing Railway private tunnel has been reported open in a **separate** Ubuntu terminal. PostgreSQL client installed: **18.6**. This stage **does not connect to PostgreSQL**, create a real database export, or test a restore. No Cloudflare or new subscription.

Goal: prove that the specific authenticated rclone Drive remote can **write an age-encrypted fictional payload** into the approved child folder, and that the bytes retrieved from that Drive child have the exact same SHA-256. Filename contains `SYNTHETIC`, so the test object cannot be mistaken for a production backup.

Approved folder: [BLISS BOT CHAPEL - ENCRYPTED POSTGRES BACKUPS](https://drive.google.com/drive/folders/1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo), ID `1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo`.

## In the SECOND Ubuntu terminal only

Run these commands **one line at a time** at a normal `$` prompt. If your terminal shows `>`, press **Ctrl+C** once to cancel unfinished shell input.

```bash
umask 077
set -o pipefail
export RCLONE_CONFIG="$HOME/.config/rclone/rclone.conf"
chmod 600 "$RCLONE_CONFIG"
export BLISS_AGE_IDENTITY="$HOME/.config/alpha-bliss-recovery/age-identity.txt"
test -s "$BLISS_AGE_IDENTITY" && echo AGE_IDENTITY_PRESENT
mkdir -p -m 700 "$HOME/bliss-backup-operator/encrypted-archives"
export CANARY="$HOME/bliss-backup-operator/encrypted-archives/SYNTHETIC-ALPHA-DRIVE-$(date -u +%Y%m%dT%H%M%SZ).age"
printf 'ALPHA synthetic encrypted Drive transport test only\n' | age -r "$(age-keygen -y "$BLISS_AGE_IDENTITY")" -o "$CANARY"
test -s "$CANARY" && echo SYNTHETIC_ENCRYPTED_FILE_CREATED
rclone --config "$RCLONE_CONFIG" --drive-root-folder-id 1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo copyto --immutable "$CANARY" "drive:$(basename "$CANARY")"
LOCAL_SHA="$(sha256sum "$CANARY" | cut -d' ' -f1)"
REMOTE_SHA="$(rclone --config "$RCLONE_CONFIG" --drive-root-folder-id 1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo cat "drive:$(basename "$CANARY")" | sha256sum | cut -d' ' -f1)"
test "$LOCAL_SHA" = "$REMOTE_SHA" && echo DRIVE_ENCRYPTED_CANARY_READBACK_PASS
```

On success, the file name visible in Google Drive begins **SYNTHETIC-ALPHA-DRIVE-** and ends `.age`. This is fictional plaintext encrypted before upload. The exact uploader for real backups additionally handles checksum manifests and separate file-permission/source identity checks.

**Do not paste:** the age private identity, rclone OAuth token/configuration contents, Railway tunnel output/URL/password, Google OAuth authorization redirect URLs, or any database rows. The synthetic canary content and the `DRIVE_ENCRYPTED_CANARY_READBACK_PASS` success marker contain no secrets.

If a command errors, STOP rather than changing Drive permissions or retrying a production dump. A read-only listing can succeed with `drive.readonly` permissions; this stage proves actual **write** permissions separately. Google OAuth consent remaining in External Testing may cause the refresh token to expire in approximately 7 days and is **not** suitable as the long-term unattended backup schedule.

## After the canary passes

Engineering should inspect the dedicated Google Drive folder for a **SYNTHETIC-ALPHA-DRIVE-...age** object to independently confirm the upload. The owner does **not** need to share the full output of `rclone config`. Keep the test archive for evidence until the independent readback is recorded.

Next must be: secure out-of-band key recovery copy; confirm actual owner-approved database host, user, cluster identity, TLS CA / `verify-full` over SSH, libpq passfile mode 0600, and source role privileges; then run only the reviewed streaming `pg_dump`→`age` operator script with source pinned. A real archive must be transferred with the dedicated checksum-manifest uploader and validated by restore into an isolated nonproduction PostgreSQL 18 target before production readiness. The source is **not** writable during a logical `pg_dump` read, but queries still read actual production data and require appropriate approval/handling.

Existing draft PR: https://github.com/Coxskull/Bliss_Bot_Chapel/pull/316. Recovery/launch status remains **BLOCKED** until real backup + isolated restore, and consent/auth/ingress gates, are accepted.
