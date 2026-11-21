# Bliss Bot — Stage 4B: Verify encrypted recovery-key copy retrieved from Personal Vault

**Date:** 2026-11-05. **Scope:** key recovery verification only; NO production database access, export, restore, deployment, billing, or public-site changes.

Owner reports that `age-identity-recovery-*.age` (an **age-passphrase-encrypted** copy of the original private age identity) was uploaded to Microsoft OneDrive **Personal Vault**. Upload is **owner-reported**, not independently accessible to the connected tools. The previous local `RECOVERY_KEY_LOCAL_ENCRYPTED_COPY_PASS` screenshot confirmed the encrypted copy could be decrypted and matched the private key locally.

**Still unverified:** the encrypted bytes retrieved back from OneDrive match the locally encrypted file and decrypt correctly using a recovery passphrase. Do not mark off-device key custody complete until that check succeeds.

## Step 1 — Download only the encrypted recovery copy

On [OneDrive](https://onedrive.live.com/) open **Personal Vault** with your normal Microsoft verification and **download** the `age-identity-recovery-*.age` file to Windows Downloads. Do not download or upload the plaintext `age-identity.txt` and do not upload passphrases, Railway credentials, SSH private keys, or rclone OAuth configuration.

In a *second Ubuntu/WSL terminal* (at a normal `$` prompt), prepare a private target and open it in Windows File Explorer:

```bash
umask 077
install -d -m 700 "$HOME/bliss-backup-operator/offdevice-verify"
explorer.exe "$(wslpath -w "$HOME/bliss-backup-operator/offdevice-verify")"
```

In Windows Explorer, **move only the downloaded encrypted `.age` file** from Windows Downloads into the private `offdevice-verify` folder that just opened. Do not move the original key, and do not alter the downloaded file's contents or name. This step confirms an independent **retrieve from cloud** path; the cloud copy remains in Personal Vault.

## Step 2 — Verify the exact ciphertext bytes and recovery passphrase

If you're still in the same Ubuntu terminal where `RECOVERY` was exported for Stage 4A, these checks should succeed:

```bash
test -n "${RECOVERY:-}" && test -s "$RECOVERY" && echo ORIGINAL_ENCRYPTED_RECOVERY_PRESENT
```

If not, select the **exact original** local encrypted archive from the previous `$HOME/bliss-backup-operator/key-escrow/` directory first. For this stage there should normally be one file. Use this command to select it only when there is exactly one:

```bash
shopt -s nullglob
files=("$HOME"/bliss-backup-operator/key-escrow/age-identity-recovery-*.age)
test "${#files[@]}" -eq 1 && RECOVERY="${files[0]}" && echo ORIGINAL_ENCRYPTED_RECOVERY_PRESENT
```

**STOP** if the expected marker does not appear (there may be zero or multiple archives; choose the correct one privately and do not guess).

Now run, **one line at a time**:

```bash
KEY="$HOME/.config/alpha-bliss-recovery/age-identity.txt"
DOWNLOADED="$HOME/bliss-backup-operator/offdevice-verify/$(basename "$RECOVERY")"
test -s "$DOWNLOADED" && echo ENCRYPTED_VAULT_DOWNLOAD_PRESENT
cmp -s "$RECOVERY" "$DOWNLOADED" && echo OFFDEVICE_ENCRYPTED_BYTES_MATCH
age --decrypt "$DOWNLOADED" | cmp -s "$KEY" - && echo RECOVERY_KEY_OFFDEVICE_COPY_VERIFIED
```

The final command prompts for the recovery passphrase **locally** and compares restored bytes against the original key without printing the key. Since the pipeline uses a private `age` identity, never run with `set -x`, screenshot its secret entry prompts, paste passphrases into the chat, or pipe the decoded private key to a web tool. If any earlier marker is missing, stop; do not claim the final marker.

After this succeeds, a suitable owner report is just:

```text
OFFDEVICE_ENCRYPTED_BYTES_MATCH
RECOVERY_KEY_OFFDEVICE_COPY_VERIFIED
```

This verifies retrieval and decryption on the **same workstation** that holds the original identity; it does not test a second machine or the durability of the separate passphrase record. Owner must ensure the recovery passphrase can be accessed if the workstation is lost. For higher assurance, repeat recovery into a separately controlled clean device later without copying the plaintext key to another shared location.

## After recovery-key escrow validation

Engineering next verifies the existing Railway `bliss-bot / production / Postgres` source identity, exact private SSH tunnel port, PostgreSQL SSL server certificate chain and `verify-full`, approved database/user/cluster identity and read/export role, and owner-owned 0600 libpq passfile. No public proxy or TLS downgrade. Only after those preflight checks: owner-approved `pg_dump` streaming into `age`, encrypted archive+SHA-256 manifest upload to the **separate** [ALPHA EVIDENCE encrypted database backup folder](https://drive.google.com/drive/folders/1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo), and separately accepted isolated real PostgreSQL 18 restore.

This private encrypted key escrow **must not be placed in the shared ALPHA EVIDENCE database backup folder**. Cloudflare and additional subscriptions are unnecessary. Public registration and production recovery acceptance remain **NO-GO / BLOCKED** until real restore and other launch gates pass.
