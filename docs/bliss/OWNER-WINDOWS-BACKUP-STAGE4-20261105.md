# Bliss Bot — Stage 4: Protect the age recovery key before a real backup

**Date:** 2026-11-05. **Scope:** existing `bliss-bot` production database and Google Drive backup workflow. **This stage is local-only. It does not run SQL, export production records, restore a database, deploy Railway, or upload secrets.**

## Verified starting point

- Owner reported the private Railway CLI tunnel is open, and installed `pg_dump (PostgreSQL) 18.6` on Ubuntu/WSL.
- Owner reported `LOCAL_AGE_ENCRYPTION_TEST_PASS` and `DRIVE_READ_ONLY_LIST_COMPLETE`.
- The 238-byte [synthetic age-encrypted canary](https://drive.google.com/file/d/1vGL4MyROLjoAkImYANfYOA_7YpPF-tNq/view) was downloaded independently from [ALPHA EVIDENCE → Bliss encrypted backups](https://drive.google.com/drive/folders/1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo), and its SHA-1 matched the owner-local screenshot (`24cfcf070a8c5076c54d4d80c742b20ed26a46c5`). This proves a **synthetic transport** round-trip only, not a database backup. Use SHA-256 and isolated real restore for actual backup acceptance.
- Connected Railway read-only metadata: service `Postgres` in `bliss-bot / production`, image `ghcr.io/railwayapp-templates/postgres-ssl:18`, 5GB mounted volume, latest deployment SUCCESS, no pending staged changes and **no public TCP proxy**. No SQL identity, user privileges or trusted TLS CA have yet been independently verified.

## Stage 4A — make a separately passphrase-encrypted recovery copy (local)

**Use your SECOND Ubuntu terminal. Leave your Railway tunnel in the first terminal alone.** The original private `age` identity must remain on your computer and must not be pasted in this chat.

These are complete one-line commands. Run them **one by one** at the normal `$` prompt (press Ctrl+C if stuck at a `>` continuation prompt):

```bash
umask 077
set -o pipefail
export KEY="$HOME/.config/alpha-bliss-recovery/age-identity.txt"
test -f "$KEY" && test ! -L "$KEY" && test "$(stat -c '%a' "$KEY")" = 600 && echo PRIVATE_AGE_KEY_PERMISSIONS_PASS
install -d -m 700 "$HOME/bliss-backup-operator/key-escrow"
export RECOVERY="$HOME/bliss-backup-operator/key-escrow/age-identity-recovery-$(date -u +%Y%m%dT%H%M%SZ).age"
age --passphrase --output "$RECOVERY" "$KEY"
chmod 600 "$RECOVERY"
age --decrypt "$RECOVERY" | cmp -s "$KEY" - && echo RECOVERY_KEY_LOCAL_ENCRYPTED_COPY_PASS
```

**Important:** `age --passphrase` asks for a **new, strong recovery passphrase** in the terminal. Use a unique passphrase kept **outside this computer**, in the owner's trusted password manager or another independent secure record. Do not put the passphrase on the command line, in shell history, in chat, or in the same Drive folder as the encrypted database archive. To verify, the decrypt command asks for it again without printing the key. If verification fails, STOP; never delete the original key. Do not run this code with `set -x` or save the terminal output.

A passing `RECOVERY_KEY_LOCAL_ENCRYPTED_COPY_PASS` proves only that a new **passphrase-encrypted copy of the original age identity** can be decrypted locally. This is **NOT** independent disaster recovery until you also store its `.age` ciphertext securely outside the WSL/Windows machine.

## Stage 4B — independent recovery-key custody (owner-side action)

After 4A passes, use your existing private cloud storage account **separate from ALPHA EVIDENCE's shared Google Drive backup folder**, with owner-only access, to save **only** the passphrase-encrypted `age-identity-recovery-*.age` file. A separate personally controlled cloud account or separately administered secure password-manager attachment is preferable; a different folder under the same shared Drive account is weaker. If using a Windows sync folder, confirm the file is truly synced off-device; copying into another directory on the same hard disk does **not** suffice.

Never upload:
- Original plaintext `age-identity.txt`
- PostgreSQL credentials/URL, `rclone.conf`, SSH private key, live database rows
- The recovery passphrase

Keep the recovery passphrase independently accessible from the escrow file and from WSL. Re-download the encrypted copy **privately** and repeat the decryption/compare test on a trusted device to establish independent recoverability. Do not post any file path containing private account identities, the passphrase, or key data to GitHub/ChatGPT.

**Status to report (non-secret only):** `RECOVERY_KEY_LOCAL_ENCRYPTED_COPY_PASS` and, **only when the file was copied off-device and tested**, `RECOVERY_KEY_OFFDEVICE_COPY_VERIFIED`. Otherwise report `RECOVERY_KEY_OFFDEVICE_PENDING`.

## Stage 4C — production source identity/TLS readiness (engineering check; do NOT export yet)

The existing Railway image and successful deployment **do not** prove SSL hostname verification, the production PostgreSQL cluster ID, read-only export permissions, or an actual recoverable backup. Before performing any real export, the owner/operator and engineering must verify:

1. Correct `bliss-bot / production / Postgres` tunnel, actual CLI-reported dynamic local port, and original TLS hostname. Use `PGHOSTADDR=127.0.0.1` to route via the SSH tunnel while retaining the correct trusted server `PGHOST` hostname. Do not assume port 55432.
2. An independently trusted PostgreSQL server certificate **CA** and `PGSSLMODE=verify-full`, rather than `sslmode=disable` or `verify-ca`. Railway `postgres-ssl:18` image alone does not establish a trusted client certificate chain; the operator must confirm the actual CA and hostname safely. If unavailable, **stop** rather than weakening checks or publishing a DB port.
3. A least-privilege PostgreSQL backup role, approved actual database name, user, hostname and cluster `system_identifier`; the existing operator backup script pins and checks them before exporting. Permissions for `pg_control_system()` must be confirmed, not silently elevated if missing.
4. A 0600 owner-owned libpq passfile outside GitHub, Drive and the encrypted backup directory. Do not put passwords into the chat, command-line arguments, captured screenshots or log files.
5. A nonproduction isolated PostgreSQL 18 restore target and allowed access established **before** first production dump, plus retention, cost, source version and recovery time targets. The `age` key copy and its passphrase remain separately held.

**No real export until all of these are checked.** Once approved, the existing protected `scripts/bliss-logical-backup-operator.sh --execute` streams a custom `pg_dump` directly into `age` without producing plaintext dumps on disk, then the dedicated `scripts/bliss-google-drive-upload-operator.sh` uploads the ciphertext and SHA-256 manifest to the exact approved Drive folder. Verify remote archive integrity and perform a separately approved restore into a **different** PostgreSQL instance, never the live production database.

**Deferred production gates:** This private Google Drive OAuth client is in Google External **Testing**, where refresh tokens for Drive scopes may expire after seven days. A one-off successful rclone session is not unattended backup reliability. The shared folder permits inherited editor modifications/deletion and is not immutable. Public advertiser/creator onboarding stays restricted until these recovery and authentication, ingress, privacy, consent gates are separately accepted.

**References:** [Existing draft PR #316](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/316) · [Railway Postgres in existing project](https://railway.com/project/fb33c1b7-03fb-42c8-89b1-081951a738fd) · [age documentation](https://github.com/FiloSottile/age) · [Google Drive encrypted backups](https://drive.google.com/drive/folders/1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo).
