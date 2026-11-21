import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const script = readFileSync('scripts/task3b-guarded-railway-install.ps1', 'utf8');

assert.match(script, /\[Parameter\(Mandatory = \$true\)\]/);
assert.match(script, /Get-Item -LiteralPath \$ReceiptPath/);
assert.match(script, /Get-FileHash -LiteralPath \$receipt\.FullName -Algorithm SHA256/);
assert.match(script, /BLISS-PG18-RESTORE-VERIFIED/);
assert.match(script, /\$acknowledgement -cne 'BLISS-PG18-RESTORE-VERIFIED'/);
assert.match(script, /\$remote = \$remote\.Replace\('__EVIDENCE_DIGEST__', \$hash\)/);
assert.match(script, /BLISS_INITIAL_INSTALL_BACKUP_EVIDENCE_ID=__EVIDENCE_DIGEST__/);
assert.match(script, /--migrate --initial-install-empty-hosted-db/);
assert.match(script, /fb33c1b7-03fb-42c8-89b1-081951a738fd/);
assert.ok(script.includes('RAILWAY_ENVIRONMENT_ID:-'), "exact hosted environment required");
assert.ok(script.includes('b67a9836-36bf-4083-93f7-00c867b13f39'), "production environment pinned");
assert.ok(script.includes('RAILWAY_SERVICE_ID:-'), "actual service identity required");
assert.ok(script.includes('8b22ff53-ccf5-4341-a473-ff146ca497f1'), "Bliss API service pinned");
assert.ok(script.includes('TASK3B_INSTALL=REFUSED_SERVICE_IDENTITY'), "unsafe target must refuse");
assert.match(script, /Runtime__RestrictedPreview:-/);
assert.match(script, /BLISS_INITIAL_INSTALL_EXPECTED_DB_HOST:-/);
assert.match(script, /BLISS_INITIAL_INSTALL_EXPECTED_DB_NAME:-/);
assert.ok(script.includes('-- bash -c $runner 2>&1'), "Windows direct Railway SSH path");
assert.ok(script.includes('ToBase64String([Text.Encoding]::UTF8.GetBytes($remote))'),
  "encode only reviewed remote script source");
assert.ok(script.includes('set -o pipefail; printf %s '), "remote streaming runner fails closed");
assert.ok(script.includes("$ErrorActionPreference = 'Continue'"), "native status banner handled under PS5.1");
assert.ok(script.includes('$ErrorActionPreference = $previousErrorActionPreference'), "restore strict errors");
assert.ok(!script.includes('$remote | & $railway.Source ssh'), "reject broken CLI stdin passthrough");
assert.match(script, /\$remoteExit = 1/);
assert.match(script, /\} catch \{/);
assert.match(script, /TASK3B_PUBLIC_REGISTRATION=HELD/);
assert.match(script, /Initial schema installed\. Restricted preview still required\. Delivery remains NOT_SENT\./);
assert.match(script, /\$remoteExit -ne 0 -or \$successCount -ne 1/);
assert.ok(script.indexOf("Read-Host") < script.indexOf('-- bash -c $runner'),
  'owner-specific restore attestation must precede remote execution');
assert.ok(!/Runtime__RestrictedPreview\s*=\s*false|Authentication__AdvertiserRegistrationRequested\s*=\s*true/.test(script),
  'must never unlock the public signup or production preview');
assert.ok(!/Write-Output\s+\$raw|Write-Host\s+\$raw|Start-Transcript/.test(script),
  'must not disclose unexpected remote output or enable transcripts');
const drivePrep = readFileSync('scripts/task3b-prepare-drive-wsl-evidence.ps1', 'utf8');
const expectedSha = 'df487fcdeefb99db69956849c4b3ae5a1035456c8823b1b142fee4a0ff43fb3b';
assert.ok(script.includes(expectedSha) && drivePrep.includes(expectedSha),
  'only the independently verified existing encrypted Drive archive can use the alternative');
assert.ok(script.includes('GOOGLE_DRIVE_WSL_ONE_ARCHIVE_OWNER_REVIEW'),
  'alternative receipt must be separately classified');
assert.ok(script.includes('INDEPENDENT_CONNECTED_DRIVE_RETRIEVED_CIPHERTEXT_SHA256_20261121'),
  'exact independent cloud copy readback is required');
assert.ok(script.includes('OWNER_SCREENSHOT_AND_CURRENT_LOCAL_READBACK'),
  'local restore evidence must be explicitly scoped');
assert.ok(script.includes('WSL2 PostgreSQL 18 local Unix socket'),
  'only local PG18 recovery can count for WSL alternative');
assert.ok(script.includes("offhostProvider -cne 'DigitalOcean'"),
  'existing DigitalOcean alternative remains strict');
assert.ok(script.includes("isolatedRestoreEngine -cne 'Docker PostgreSQL 18 --network none'"),
  'existing Docker restore controls remain intact');
assert.ok(script.includes('ownerAttestationRequiredAtProductionInstall -ne $true'),
  'Google Drive/WSL alternative must never waive owner attestation');
assert.ok(drivePrep.includes('Get-FileHash -LiteralPath $archive -Algorithm SHA256'),
  'local source ciphertext must be verified');
assert.ok(drivePrep.includes('RECEIPT_ALREADY_EXISTS_REFUSE_OVERWRITE'),
  'cannot replace existing operator custody receipt');
assert.ok(drivePrep.includes('TASK3B_PRODUCTION_MIGRATION=NOT_EXECUTED'),
  'evidence preparation cannot claim hosted migration');
assert.ok(!drivePrep.includes('railway.cmd ssh') && !drivePrep.includes('dotnet run'),
  'recovery evidence preparation cannot invoke hosting or run migrations');
console.log('Task3B guarded Railway installation source-contract: PASS (not hosted migration)');
