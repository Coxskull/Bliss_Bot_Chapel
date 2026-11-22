import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = path => readFileSync(path, 'utf8');
const owner = read('scripts/task4-creator-guarded-incremental.ps1');
const migration = read('Bliss.Infrastructure/Migrations/20261107100000_SecureCreatorRegistration.cs');
const app = read('Bliss.Api/Program.cs');
const guarded = read('Bliss.Api/Runtime/MigrationCommand.cs');
const one = read('Bliss.Api/Runtime/CreatorIncrementalMigration.cs');

for (const exact of [
  'BLISS_TASK4_CREATOR_RECOVERY_MANIFEST_SHA256',
  'BLISS_INITIAL_INSTALL_EXPECTED_DB_HOST',
  'BLISS_INITIAL_INSTALL_EXPECTED_DB_NAME',
  'BLISS-CREATOR-CURRENT-RECOVERY-VERIFIED',
  'isolatedRestoreContainerRemoved',
  'isolatedRestorePublicTableCount',
  'offhostRemoteChecksum',
  'sourcePublicTableCount',
  'Get-FileHash -LiteralPath $archivePath -Algorithm SHA256',
  '2026-11-21T07:07:25Z',
  'Get-Command railway.cmd',
  'set -euo pipefail',
  '--migrate --task4-creator-incremental',
  'TASK4_CREATOR_SCHEMA_MIGRATION=PASS',
  'TASK4_CREATOR_PRODUCTION_SIGNUP=STILL_OFF_PENDING_HOSTED_ACCEPTANCE'
]) assert.ok(owner.includes(exact), 'owner requires ' + exact);

assert.match(owner, /\$captured -lt \$now.AddHours\(-6\)/);
assert.match(owner, /\$raw = @\(& \$railway\.Source ssh -p \$project -s bliss-bot -e production -- bash -c \$runner 2>&1\)/);
assert.ok(owner.indexOf('BLISS-CREATOR-CURRENT-RECOVERY-VERIFIED') < owner.indexOf('ssh -p $project'),
  'fresh custody and owner attestation before remote execution');
assert.doesNotMatch(owner, /(?:Runtime__RestrictedPreview|Authentication__CreatorRegistrationRequested)\s*=\s*(?:false|true)/);
assert.doesNotMatch(owner, /Write-(?:Host|Output)\s+\$raw|Start-Transcript|pg_restore|pg_dump|DROP\s+TABLE/i);

for (const text of [
  'CreatorIncrementalRequested(args)',
  'ValidateCreatorIncrementalHostedInstall(',
  'CreatorIncrementalMigration.ApplyOnceAsync',
  'TASK4_CREATOR_INCREMENTAL_SCHEMA=REFUSED_OR_NOT_VERIFIED'
]) assert.ok(app.includes(text), 'production one-shot entrypoint: ' + text);

assert.match(guarded, /actual\.SslMode != SslMode\.VerifyFull/);
assert.match(guarded, /actual\.TrustServerCertificate/);
assert.match(guarded, /!advertiserRegistrationEnabled\s*\|\|\s*creatorRegistrationEnabled/);
assert.match(guarded, /PublicTableCount < 90/);
assert.match(one, /pending\.Length != 1 \|\| pending\[0\] != MigrationCommand\.CreatorMigrationId/);
assert.match(one, /GetPendingMigrationsAsync\(/);
assert.match(one, /await db\.Database\.MigrateAsync\(\)/);
assert.match(one, /PublicTableCount != before\.PublicTableCount \+ 2/);
assert.match(migration, /ENABLE ROW LEVEL SECURITY/);
assert.match(migration, /REVOKE ALL ON TABLE/);

console.log('TASK4_CREATOR_GUARDED_INCREMENTAL_CONTRACT=PASS (source-only, no Railway or database calls)');
