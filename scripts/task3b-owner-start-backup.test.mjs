import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// Contract-only: Never run a real Railway CLI or GPG in CI.
const wrapper = readFileSync("scripts/task3b-owner-start-backup.ps1", "utf8");
const capture = readFileSync("scripts/task3b-bliss-pg18-capture.ps1", "utf8");
const runbook = readFileSync("docs/engineering/task3b-bliss-pg18-offhost-backup-and-restore.md", "utf8");

assert.match(wrapper, /Get-Command railway\.cmd/);
assert.doesNotMatch(wrapper, /Get-Command railway(?:\s|-ErrorAction)/,
  "Windows owner launcher must not fall back to execution-policy-blocked npm .ps1 shim");
assert.match(wrapper, /& \$railway\.Source whoami \*> \$null/);
assert.match(wrapper, /RAILWAY_OWNER_LOGIN_REQUIRED/);
assert.match(wrapper, /--list-secret-keys/);
assert.match(wrapper, /GPG_PRIVATE_KEY_UNAVAILABLE/);
assert.match(wrapper, /GPG_PUBLIC_FINGERPRINT=/);
assert.match(wrapper, /\^\[A-F0-9\]\{40\}\$/);
assert.match(wrapper, /Read-Host/);
assert.match(wrapper, /\$found\.Contains\(\$selected\)/);
assert.match(wrapper, /task3b-bliss-pg18-capture\.ps1/);
assert.match(wrapper, /-GpgRecipientFingerprint \$selected -OutputDirectory \$OutputDirectory/);
assert.match(wrapper, /-ExecutionPolicy Bypass -File/);
assert.doesNotMatch(wrapper, /Set-ExecutionPolicy|RAILWAY_TOKEN=|PGPASSWORD=|GPG_PASSPHRASE|skip[-_]backup/i);
assert.ok(wrapper.indexOf("RAILWAY_OWNER_LOGIN_REQUIRED") <
          wrapper.indexOf("& $backupScript -GpgRecipientFingerprint"),
  "Railway authentication must precede the only invocation that can query production DB");
assert.ok(wrapper.indexOf("RECIPIENT_NOT_AN_OWNER_HELD_GPG_KEY") <
          wrapper.indexOf("& $backupScript -GpgRecipientFingerprint"),
  "GPG secret-key custody and explicit owner choice precede production read");
assert.match(capture, /pg_dump --dbname="\$db" --format=custom --no-owner --no-acl/);
assert.match(capture, /--encrypt --recipient/);
assert.match(capture, /TASK3B_OFFHOST_DIGITALOCEAN_COPY=NOT_VERIFIED/);
assert.match(capture, /TASK3B_ISOLATED_PG18_RESTORE=NOT_VERIFIED/);
assert.match(runbook, /task3b-owner-start-backup\.ps1/);
assert.match(runbook, /GPG.*private key/i);
console.log("TASK3B_OWNER_BACKUP_WINDOWS_CMD_GPG_CUSTODY_AND_NO_IMPLICIT_PRODUCTION_ACTION=PASS");
