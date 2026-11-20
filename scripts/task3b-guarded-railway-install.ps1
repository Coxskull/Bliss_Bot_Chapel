# ALPHA GLOBAL MEDIA — Task #3B: guarded Bliss PG18 initial schema installation.
# OWNER-ONLY, ONE-SHOT. Run on the owner's Windows workstation with Railway CLI authenticated.
# Requires an EXISTING, private evidence file for the *Bliss Railway PostgreSQL 18*
# off-host backup AND independently tested isolated restore. DigitalOcean DR362
# evidence for a different database is NOT sufficient.
#
# Example (only after the exact Bliss PG18 evidence exists):
#   .\scripts\task3b-guarded-railway-install.ps1 -ReceiptPath C:\private\bliss-pg18-restore-evidence.txt
#
# This file is an operator tool, NOT an automatic deployment. It never creates backups,
# invents restore evidence, publishes secrets, enables registrations, or releases STOP.
# The existing .NET guard performs the authoritative TLS, target and empty-schema checks.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ReceiptPath
)

$ErrorActionPreference = 'Stop'
$projectId = 'fb33c1b7-03fb-42c8-89b1-081951a738fd'
$serviceName = 'bliss-bot'
$environmentName = 'production'

$railway = Get-Command railway.cmd -ErrorAction SilentlyContinue
if ($null -eq $railway) { $railway = Get-Command railway -ErrorAction SilentlyContinue }
if ($null -eq $railway) {
    throw 'Task3B: Railway CLI unavailable. No database changes were made.'
}

# Never read a credential, database password, restore archive, or connection string
# into local logs; the receipt is a separate private operator-held evidence document.
$receipt = Get-Item -LiteralPath $ReceiptPath -ErrorAction Stop
if ($receipt.PSIsContainer -or $receipt.Length -le 0) {
    throw 'Task3B: The Bliss PG18 off-host backup + restore evidence file must exist and be nonempty.'
}
$hash = (Get-FileHash -LiteralPath $receipt.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -cnotmatch '^[a-f0-9]{64}$') {
    throw 'Task3B: Failed to derive an evidence manifest digest.'
}

Write-Output 'TASK3B_OPERATION=GUARDED_INITIAL_SCHEMA_INSTALL'
Write-Output 'TASK3B_TARGET=EXISTING_BLISS_RAILWAY_PRODUCTION_PG18'
Write-Output 'TASK3B_EVIDENCE_CONTENT=OWNER_HELD_NOT_READ_OR_VERIFIED_BY_SCRIPT'
Write-Output 'TASK3B_GLOBAL_STOP=UNCHANGED'
Write-Output 'TASK3B_PUBLIC_SIGNUP=UNCHANGED_LOCKED'

# The prompt deliberately does not ask the user to paste confidential
# backup material into a shell, GitHub, chat, or Railway configuration.
$acknowledgement = Read-Host 'Type BLISS-PG18-RESTORE-VERIFIED only if this PRIVATE receipt proves an off-host backup and successful ISOLATED restore of the exact current Bliss Railway PostgreSQL 18 target'
if ($acknowledgement -cne 'BLISS-PG18-RESTORE-VERIFIED') {
    throw 'Task3B: Owner-specific backup/restore attestation not provided. No changes made.'
}

# The lowercase hexadecimal SHA-256 is safe to embed as an environment-only
# evidence reference for this *one process*; it is not a secret or the backup.
# Hash input is validated before substitution, preventing shell injection.
$remote = @'
set -eu
umask 077
if [ "${ASPNETCORE_ENVIRONMENT:-}" != "Production" ] ||
   [ "${Runtime__RestrictedPreview:-}" != "true" ] ||
   [ "${Authentication__AdvertiserRegistrationRequested:-false}" = "true" ]; then
    echo "TASK3B_INSTALL=REFUSED_CONFIGURATION"
    exit 2
fi
if [ -z "${BLISS_INITIAL_INSTALL_EXPECTED_DB_HOST:-}" ] ||
   [ -z "${BLISS_INITIAL_INSTALL_EXPECTED_DB_NAME:-}" ]; then
    echo "TASK3B_INSTALL=REFUSED_TARGET_IDENTITY"
    exit 2
fi
export BLISS_INITIAL_INSTALL_BACKUP_EVIDENCE_ID=__EVIDENCE_DIGEST__
exec /usr/local/bin/bliss-railway-entrypoint --migrate --initial-install-empty-hosted-db
'@
$remote = $remote.Replace('__EVIDENCE_DIGEST__', $hash)
if ($remote.Contains('__EVIDENCE_DIGEST__')) {
    throw 'Task3B: Evidence substitution failed. No database changes made.'
}

# Execute in the EXISTING running application via the owner's Railway CLI SSH.
# No public DB proxy, second service, GitHub secret, or persistent flag change.
# Piping the shell script avoids a long Windows SSH command and keeps the
# digest out of process arguments. Do not enable PowerShell transcripts.
# Capture BOTH streams; failure diagnostics can contain driver connection details.
# Never print them into CI, chat, command history, or normal operator output.
$raw = @()
$remoteExit = 1
try {
    $raw = @($remote | & $railway.Source ssh -p $projectId -s $serviceName -e $environmentName -- sh -se 2>&1)
    $remoteExit = $LASTEXITCODE
} catch {
    $raw = @()
    $remoteExit = 1
}

# Do not echo raw application output: an unexpected runtime could emit secrets.
# Only the already-reviewed, fixed-vocabulary .NET completion line is trusted.
$expected = 'Initial schema installed. Restricted preview still required. Delivery remains NOT_SENT.'
$successCount = @($raw | Where-Object { ([string]$_).Trim() -ceq $expected }).Count
if ($remoteExit -ne 0 -or $successCount -ne 1) {
    Write-Output 'TASK3B_INSTALL=NOT_VERIFIED'
    Write-Output 'TASK3B_PUBLIC_REGISTRATION=HELD'
    throw 'Task3B: Guarded install did not produce the exact completion receipt. Check private Railway logs. Do not flip registration flags.'
}

Write-Output 'TASK3B_INITIAL_SCHEMA_INSTALL=REMOTE_COMPLETION_RECEIPT_PASS'
Write-Output 'TASK3B_BACKUP_RESTORE=OWNER_ATTESTED_NOT_INDEPENDENTLY_VERIFIED_BY_SCRIPT'
Write-Output 'TASK3B_PUBLIC_REGISTRATION=HELD_PENDING_HOSTED_OIDC_CONSENT_SECURITY_TESTS'
Write-Output 'TASK3B_LIVE_PAYMENTS=HELD'
