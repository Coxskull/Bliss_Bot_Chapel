# ALPHA / Bliss Bot — Owner Windows setup (Stage 1 only)
**Date:** 2026-11-05. **Scope:** existing Railway `bliss-bot` / production / Postgres, and owner-approved Google Drive storage. **NO database export or restore in this stage.**

## What engineering has already done
- Existing [Railway project](https://railway.com/project/fb33c1b7-03fb-42c8-89b1-081951a738fd) and Postgres remain online. Native Railway Backups tab shows **No Backups**, no schedules or PITR (Hobby plan).
- Google Drive parent is the owner's [ALPHA EVIDENCE](https://drive.google.com/drive/folders/1HnemyTZBgQhmFhzddyIgxG0cPQ5wElT3). The verified empty child is [BLISS BOT CHAPEL - ENCRYPTED POSTGRES BACKUPS](https://drive.google.com/drive/folders/1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo), ID `1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo`. No Cloudflare service or new subscription.
- Engineering draft [PR #316](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/316) holds opt-in encrypted-backup and encrypted-only Google Drive upload utilities. CI passes with synthetic/mocked data. It is **NOT MERGED**, has not been run against Railway production and does not silently enable backups.

## Owner actions: do Stage 1 only, then report the non-secret status

You use a **Windows computer**. Run the following on your own machine, not in ChatGPT, GitHub Actions, a web page, or Railway's production application shell. The CLI session uses your own browser authentication, separate from the Railway login previously saved in ChatGPT's browser.

1. In **Windows PowerShell**, check whether Windows Subsystem for Linux is installed:

```powershell
wsl --status
```

If not installed, and you have Windows administrator authority, open PowerShell as Administrator and run:

```powershell
wsl --install -d Ubuntu
```

Follow the official Windows prompts (a reboot may be required) and open the **Ubuntu** application. If Ubuntu is already installed, skip installation. This changes your local machine only, not Railway.

2. In **Ubuntu / WSL** (not Windows PowerShell), install basic tools and the supported Railway CLI:

```bash
sudo apt-get update
sudo apt-get install -y curl git ca-certificates
bash <(curl -fsSL https://railway.com/install.sh)
export PATH="$HOME/.railway/bin:$PATH"
railway --version
railway login
railway whoami
```

The CLI install command is from the [Railway CLI documentation](https://docs.railway.com/cli). It installs local software, not a new Railway service or plan. Use your own GitHub-based Railway login in the opened browser. **Never send authentication codes, passwords, Railway tokens or screenshots of the connected CLI's credentials.**

3. Work outside your Git repository so Railway CLI link metadata is not accidentally committed:

```bash
mkdir -p -m 700 "$HOME/bliss-backup-operator"
cd "$HOME/bliss-backup-operator"
railway link --project fb33c1b7-03fb-42c8-89b1-081951a738fd --environment production
```

Verify locally that it selects the **existing** `bliss-bot` project and `production` environment. Do not create a new project/environment or choose another database.

4. In the **same Ubuntu window**, open a **read-only transport tunnel**, without exporting any data:

```bash
railway connect Postgres --environment production --tunnel-only --port 55432
```

Leave this terminal open if the command succeeds. The Railway CLI [documented tunnel command](https://docs.railway.com/cli/connect) binds a local port and connects privately to the existing Postgres service. **WARNING: this command prints a database host, user, password and connection URL. Do not paste or screenshot its output into ChatGPT, GitHub, Google Drive, tickets or email.** It does not itself run `pg_dump` or write to the database. To close the tunnel later, press Ctrl+C.

**What to report back here (ONLY this):** say `Ubuntu ready; Railway CLI login verified; project linked; tunnel open`, or tell me **which of the four stages failed**, with credentials/URLs/passwords redacted. Do not send the terminal output.

## Engineering tasks to perform only after owner reports Stage 1 is ready

- Confirm an authorized source role and exact PostgreSQL 18 server identity, TLS certificate/CA needed for verify-full through the private tunnel, and safe 0600 passfile outside GitHub/Drive.
- Establish age public encryption recipient and privately escrow the **decryption identity** outside Google Drive; verify it can actually decrypt a synthetic fixture. Do not ask the owner to paste a private key.
- Configure the user's own `rclone` Google Drive OAuth on the operator workstation, pin exact Drive folder ID, verify 0600 config, and test the encrypted-only upload utility with fictional data. Google Drive interactive OAuth may require an owner-configured OAuth client; never create/paste client secrets or tokens in this chat.
- Only then, under the existing explicit owner authorization, perform the **first real encrypted logical backup** via reviewed script and private tunnel; verify remote SHA-256 by download/readback. **No plaintext or decryption key in the shared Drive folder.**
- Separately authorize and perform an **isolated real restore** into a nonproduction PostgreSQL 18 target, verify schema/data/receipt counts and measured RPO/RTO. Approve and implement recurring schedule/retention/failure alerts and independent deletion-resistant offsite copy. No live destructive restores.
- Keep customer registration `RestrictedPreview` until all backup, identity, HTTPS ingress, migration, privacy and consent release gates are independently accepted.

## Cost and security boundary
No new subscription or Cloudflare. The Google Drive child inherits shared folder access (one owner/three editors on 2026-11-05); inherited editors can potentially delete encrypted backup files, so Drive alone is not immutable retention. This stage requires **local software installation and owner login only**. Stop if an unexpected charge, new service, or credential-disclosure request appears.
