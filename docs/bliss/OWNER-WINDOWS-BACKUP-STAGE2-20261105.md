# Bliss Bot — Stage 2: operator workstation encryption + Google Drive authorization
_Date: 2026-11-05 | **local-only preparation** | Existing Railway private tunnel already open_

The owner confirmed that the existing `bliss-bot / production / Postgres` private Railway tunnel is open in their **first Ubuntu terminal**. **Keep that first terminal open**, but run all preparation below in a **new Ubuntu terminal**. Do not paste the tunnel output: it can contain database secrets.

**No production SQL is run here.** These steps do not create a database export, run `pg_dump`, add a subscription, configure Cloudflare or open a public DB proxy.

## Step A — Install non-database operator utilities

On the owner's Ubuntu/WSL machine, open a **second** Ubuntu terminal:

```bash
sudo apt-get update
sudo apt-get install -y age rclone ca-certificates postgresql-client-18
age --version
rclone version | head -n 1
pg_dump --version
```

On Ubuntu 26.04 (`resolute`), `postgresql-client-18` is available in the normal Ubuntu repositories. **Do not install only `postgresql-client-common`: it supplies a wrapper but not a `pg_dump` client binary.** Check `pg_dump --version` for **18.x** before any real backup. If installation fails or the version is not 18, stop and report that error; do not install a PostgreSQL server or create a live database. Source: https://packages.ubuntu.com/resolute/postgresql-client-18.

## Step B — Create and verify a private *age* encryption key, offline

If Ubuntu shows a leading `>` continuation prompt, **press Ctrl+C once**. Do not continue entering commands until the normal shell `$` prompt returns. The `>` prompt means your previous command has an unmatched quote or unfinished statement. **Paste each command below as a separate complete line.**

```bash
umask 077
install -d -m 700 "$HOME/.config/alpha-bliss-recovery"
key="$HOME/.config/alpha-bliss-recovery/age-identity.txt"
if [ -e "$key" ]; then echo 'Keeping existing age key'; else age-keygen -o "$key"; fi
chmod 600 "$key"
recipient="$(age-keygen -y "$key")"
printf 'BLISS test\n' > "$HOME/bliss-test.txt"
age -r "$recipient" -o "$HOME/bliss-test.age" "$HOME/bliss-test.txt"
age -d -i "$key" -o "$HOME/bliss-test-restored.txt" "$HOME/bliss-test.age"
cmp "$HOME/bliss-test.txt" "$HOME/bliss-test-restored.txt" && echo 'LOCAL_AGE_ENCRYPTION_TEST_PASS'
rm -f "$HOME/bliss-test.txt" "$HOME/bliss-test.age" "$HOME/bliss-test-restored.txt"
```

These files contain only synthetic test text, never a real SQL dump. If the key already exists it is **preserved**, not regenerated. Check the success marker before cleanup. Failure or another `>` prompt means this step remains incomplete; return to the normal shell prompt before troubleshooting.

**Key custody:** Backup archives cannot be recovered if the private age identity is lost. Keep `$HOME/.config/alpha-bliss-recovery/age-identity.txt` on the owner-controlled workstation, mode `0600`. Make a separately protected recovery copy under owner control and test its ability to decrypt a synthetic archive. Never upload the private key to the shared ALPHA EVIDENCE Drive folder, GitHub, Railway config or chat. Public recipient is not secret, but it need not be posted.

## Step C — Connect rclone to existing Google Drive (no Cloudflare)

The owner-approved directory is [ALPHA EVIDENCE → BLISS BOT CHAPEL - ENCRYPTED POSTGRES BACKUPS](https://drive.google.com/drive/folders/1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo). Exact folder ID: `1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo`.

```bash
rclone config
```

In the interactive configuration:
- Create a new remote named **`drive`**, selecting the **Google Drive** storage backend (`drive`).
- Authenticate in the owner's browser with the Google account that has access to the approved Drive folder. Do not use a service account or give rclone any private `age` key.
- Keep the Drive remote's root folder setting blank; the backup uploader pins the **specific child folder ID** each time.
- Do not paste the rclone config, OAuth token, authorization code, or screenshots containing these values into chat or tickets. Note: rclone's shared Google OAuth client may be retired in 2026; if prompted for your own OAuth client and setup cannot proceed, stop and report *only the stage failed*.

When complete:

```bash
export RCLONE_CONFIG="$HOME/.config/rclone/rclone.conf"
chmod 600 "$RCLONE_CONFIG"
rclone --config "$RCLONE_CONFIG" --drive-root-folder-id 1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo lsf drive: --files-only
echo 'DRIVE_READ_ONLY_LIST_COMPLETE'
```

A blank listing is **expected** while the backup subfolder is empty. This final command only lists filenames in the pinned Drive child; it never uploads, deletes, or modifies files. If rclone cannot access the folder, do not grant broad public access to fix it.

### Owner reply (non-secret only)

Reply with:
```
LOCAL_AGE_ENCRYPTION_TEST_PASS
DRIVE_READ_ONLY_LIST_COMPLETE
pg_dump version: <18.x / other / not installed>
```

If Stage B or C fails, report just the stage and error **with credentials and OAuth tokens removed**.

## Engineering follow-up — not part of Stage 2

After safe local preparation, engineering must separately verify PostgreSQL certificate/CA trust and `verify-full` behavior over the live SSH tunnel, source Postgres 18 identity and least-privileged backup role, and private `0600` passfile. All approval checks in `scripts/bliss-logical-backup-operator.sh` remain enforced. The owner-selected Google Drive folder stores **encrypted archive + checksum only**, while the private `age` key is separately escrowed. Then an explicitly authorized **real** encrypted export, readback and **independent isolated** restoration must be verified; synthetic CI alone is not production acceptance. Existing `RestrictedPreview` stays active until recovery, OIDC/ingress, privacy and consent gates close.

Official references: [age](https://github.com/FiloSottile/age), [rclone Google Drive configuration](https://rclone.org/drive/), [Railway private CLI tunnel](https://docs.railway.com/cli/connect), [PostgreSQL Ubuntu client](https://www.postgresql.org/download/linux/ubuntu/).
