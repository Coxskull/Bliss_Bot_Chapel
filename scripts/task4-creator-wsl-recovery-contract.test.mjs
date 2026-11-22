import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = path => readFileSync(path, 'utf8');
const wsl = read('scripts/task4-creator-wsl-pg18-restore.sh');
const receipt = read('scripts/task4-creator-wsl-pg18-isolated-restore.ps1');
const owner = read('scripts/task4-creator-owner-complete.ps1');
const guarded = read('scripts/task4-creator-guarded-incremental.ps1');

for (const token of [
  'set -euo pipefail', 'umask 077', '-u root',
  'pg_restore --version', 'createdb', 'dropdb', 'trap cleanup EXIT',
  '/var/run/postgresql', '--exit-on-error --single-transaction',
  '--no-owner --no-acl', 'TASK4_WSL_PG18_RESTORE=PASS',
  'pg_catalog.pg_tables', '__EFMigrationsHistory',
  'alpha_task4_wsl_restore_', '[[ "$left" == "0" ]]'
]) {
  if (token === '-u root') continue; // root is chosen by PowerShell wrapper
  assert.ok(wsl.includes(token), 'WSL restore must retain ' + token);
}
assert.match(wsl, /runuser -u postgres -- dropdb -h \/var\/run\/postgresql/);
assert.match(wsl, /created=0[\s\S]*trap cleanup EXIT[\s\S]*created=1/);
assert.doesNotMatch(wsl, /\/tmp\/.*\.dump|pg_restore.*\/mnt\/|psql.*-h (?:localhost|127\.0\.0\.1)|pg_restore.*-h (?:localhost|127\.0\.0\.1)/);
assert.doesNotMatch(wsl, /(?:^|\n)\s*(?:curl|wget|docker|railway)\b/i);
assert.ok(wsl.indexOf('dropdb -h /var/run/postgresql "$db"') < wsl.lastIndexOf('TASK4_WSL_PG18_RESTORE=PASS'),
  'verified local DB teardown before success');

for (const token of [
  'WSL2 Ubuntu', 'GPG', 'DigitalOcean', 'offhostSha256Verified',
  '2026-11-21T07:07:25Z', '$now.AddHours(-6)',
  'Get-FileHash -LiteralPath $archive -Algorithm SHA256',
  'isolatedRestoreDatabaseRemoved', 'isolatedRestoreTransport',
  'UNIX_SOCKET_ONLY', 'isolatedRestoreEngine', 
  'TASK4_WSL_PG18_RESTORE=PASS',
  'TASK4_WSL18=ISOLATED_RESTORE_VERIFIED_AND_DISPOSABLE_DB_REMOVED',
  'Get-Command wsl.exe', 'Get-Command gpg.exe',
  'Get-Content -LiteralPath $helper -Raw',
  '-d Ubuntu -u root -- bash -lc',
  'TASK3B_PRIVATE_RECOVERY_EVIDENCE='
]) assert.ok(receipt.includes(token), 'receipt must enforce ' + token);
assert.doesNotMatch(receipt, /Write-(?:Output|Host)\s+\$raw|Start-Transcript|docker\.exe|--network host/i);
assert.match(owner, /\[ValidateSet\('WSL','Docker'\)\]\[string\] \$RestoreEngine = 'WSL'/);
assert.match(owner, /task4-creator-wsl-pg18-isolated-restore\.ps1/);
assert.match(owner, /if \(\$RestoreEngine -ceq 'WSL'\)/);
assert.match(guarded, /\$wslRecovery = [\s\S]*isolatedRestoreDatabaseRemoved -eq \$true/);
assert.match(guarded, /-not \(\$dockerRecovery -or \$wslRecovery\)/);
assert.match(guarded, /2026-11-21T07:07:25Z/);
assert.match(guarded, /\$captured -lt \$now.AddHours\(-6\)/);
assert.doesNotMatch(guarded, /Runtime__RestrictedPreview\s*=\s*false|Authentication__CreatorRegistrationRequested\s*=\s*true/);

console.log('TASK4_WSL18_RECOVERY_CONTRACT=PASS (offline only; no restore or production migration)');
