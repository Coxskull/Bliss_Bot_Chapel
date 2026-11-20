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
assert.match(script, /Runtime__RestrictedPreview:-/);
assert.match(script, /BLISS_INITIAL_INSTALL_EXPECTED_DB_HOST:-/);
assert.match(script, /BLISS_INITIAL_INSTALL_EXPECTED_DB_NAME:-/);
assert.match(script, /-- sh -se 2>&1/);
assert.match(script, /\$remoteExit = 1/);
assert.match(script, /\} catch \{/);
assert.match(script, /TASK3B_PUBLIC_REGISTRATION=HELD/);
assert.match(script, /Initial schema installed\. Restricted preview still required\. Delivery remains NOT_SENT\./);
assert.match(script, /\$remoteExit -ne 0 -or \$successCount -ne 1/);
assert.ok(script.indexOf("Read-Host") < script.indexOf('-- sh -se'),
  'owner-specific restore attestation must precede remote execution');
assert.ok(!/Runtime__RestrictedPreview\s*=\s*false|Authentication__AdvertiserRegistrationRequested\s*=\s*true/.test(script),
  'must never unlock the public signup or production preview');
assert.ok(!/Write-Output\s+\$raw|Write-Host\s+\$raw|Start-Transcript/.test(script),
  'must not disclose unexpected remote output or enable transcripts');
console.log('Task3B guarded Railway installation source-contract: PASS (not hosted migration)');
