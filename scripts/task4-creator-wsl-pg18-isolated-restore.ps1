# ALPHA GLOBAL MEDIA Task #4 — owner-only WSL2 PG18 recovery, NO Docker.
# Makes one temporary LOCAL PG18 Unix-socket database, decrypts a fresh
# existing Railway backup via Windows GPG pipe and destroys the test DB
# before returning any production-eligible recovery receipt.
# Not a production database migration. Existing Nov21 99-table restore is
# already accepted and is NOT re-run by this script.
# Supports approved DigitalOcean or private Google Drive custody evidence.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string] $OffhostManifestPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'task4-offhost-custody.ps1')
$project = 'fb33c1b7-03fb-42c8-89b1-081951a738fd'
$environment = 'b67a9836-36bf-4083-93f7-00c867b13f39'
$service = '6aa78407-1632-48c9-8ea4-0289a7d36a5b'
if (-not (Test-Path -LiteralPath $OffhostManifestPath -PathType Leaf) -or
    (Split-Path -Leaf $OffhostManifestPath) -cnotmatch '^bliss-pg18-[a-zA-Z0-9-]+\.offhost\.json$') {
    throw 'TASK4_WSL18=REFUSED_TRUSTED_OFFHOST_MANIFEST_REQUIRED'
}
try { $m = Get-Content -LiteralPath $OffhostManifestPath -Raw | ConvertFrom-Json }
catch { throw 'TASK4_WSL18=REFUSED_INVALID_OFFHOST_MANIFEST' }
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne $project -or
    $m.sourceEnvironmentId -cne $environment -or
    $m.sourceServiceId -cne $service -or
    $m.sourceDatabase -cne 'bliss' -or
    [int]$m.sourcePostgresMajor -ne 18 -or
    [int]$m.sourcePublicTableCount -lt 90 -or
    $m.encryption -cne 'GPG_RECIPIENT' -or
    ( -not (Test-Task4OffhostCustody $m) ) -or
    $m.offhostSha256Verified -ne $true -or
    $m.isolatedRestoreVerified -eq $true -or
    $m.archiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or
    $m.offhostRemoteChecksum -cne $m.archiveSha256 -or
    $m.archiveFileName -cnotmatch '^bliss-pg18-[a-zA-Z0-9-]+\.gpg$') {
    throw 'TASK4_WSL18=REFUSED_SOURCE_OR_APPROVED_OFFHOST_EVIDENCE'
}
try {
    $captured = [DateTimeOffset]::Parse([string]$m.capturedAtUtc)
    $offhost = [DateTimeOffset]::Parse([string]$m.offhostVerifiedAtUtc)
} catch { throw 'TASK4_WSL18=REFUSED_EVIDENCE_TIME' }
$now = [DateTimeOffset]::UtcNow
if ($captured -le [DateTimeOffset]::Parse('2026-11-21T07:07:25Z') -or
    $captured -lt $now.AddHours(-6) -or $captured -gt $now.AddMinutes(5) -or
    $offhost -lt $captured -or $offhost -gt $now.AddMinutes(5)) {
    throw 'TASK4_WSL18=REFUSED_STALE_BACKUP_OR_OUT_OF_ORDER_EVIDENCE'
}
$archive = Join-Path (Split-Path -Parent $OffhostManifestPath) $m.archiveFileName
if (-not (Test-Path -LiteralPath $archive -PathType Leaf) -or
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.archiveSha256) {
    throw 'TASK4_WSL18=REFUSED_CIPHERTEXT_SHA256'
}
$wsl = Get-Command wsl.exe -ErrorAction SilentlyContinue
$gpg = Get-Command gpg.exe -ErrorAction SilentlyContinue
if ($null -eq $wsl -or $null -eq $gpg) {
    throw 'TASK4_WSL18=REFUSED_WSL2_UBUNTU_OR_WINDOWS_GPG_MISSING'
}
$helper = Join-Path $PSScriptRoot 'task4-creator-wsl-pg18-restore.sh'
if (-not (Test-Path -LiteralPath $helper -PathType Leaf)) {
    throw 'TASK4_WSL18=REFUSED_REVIEWED_WSL_RESTORE_HELPER_MISSING'
}
# Windows PowerShell 5.1 cannot reliably transport arbitrary binary pipeline
# bytes. Decode the REVIEWED Bash code inside Ubuntu and use the pre-existing
# Windows GPG keyring from WSL; no dump ever passes through PowerShell text.
$helperSource = Get-Content -LiteralPath $helper -Raw
$helper64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($helperSource))
$archive64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($archive))
$gpg64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($gpg.Source))
$expected = [int]$m.sourcePublicTableCount
# All command arguments are ASCII base64 or verified decimal; no shell
# interpolation of owner paths and no key or decrypted material in arguments.
$command = 'set -o pipefail; printf %s ' + $helper64 +
    ' | base64 -d | bash -s -- ' + $archive64 + ' ' + $expected + ' ' + $gpg64
$raw = @()
$exitCode = 1
$priorPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $raw = @(& $wsl.Source -d Ubuntu -u root -- bash -lc $command 2>&1)
    $exitCode = $LASTEXITCODE
} catch {
    $exitCode = 1
} finally {
    $ErrorActionPreference = $priorPreference
}
$passed = @($raw | Where-Object { ([string]$_).Trim() -ceq 'TASK4_WSL_PG18_RESTORE=PASS' }).Count
# Do not print raw WSL, pg_restore or GPG logs; those can reveal local paths.
if ($exitCode -ne 0 -or $passed -ne 1) {
    throw 'TASK4_WSL18=RESTORE_NOT_VERIFIED_OR_TEMPORARY_DATABASE_NOT_REMOVED'
}
$receiptPath = $OffhostManifestPath -replace '\.offhost\.json$', '.recovery-evidence.json'
if ($receiptPath -ceq $OffhostManifestPath -or (Test-Path -LiteralPath $receiptPath)) {
    throw 'TASK4_WSL18=REFUSED_EVIDENCE_PATH_COLLISION'
}
$m.isolatedRestoreVerified = $true
$m | Add-Member -NotePropertyName isolatedRestoreAtUtc -NotePropertyValue ([DateTimeOffset]::UtcNow.ToString('o')) -Force
$m | Add-Member -NotePropertyName isolatedRestoreEngine -NotePropertyValue 'WSL2 Ubuntu PostgreSQL 18 Unix socket temporary database' -Force
$m | Add-Member -NotePropertyName isolatedRestoreTransport -NotePropertyValue 'UNIX_SOCKET_ONLY' -Force
$m | Add-Member -NotePropertyName isolatedRestoreDatabase -NotePropertyValue 'alpha_task4_wsl_restore_disposable' -Force
$m | Add-Member -NotePropertyName isolatedRestorePublicTableCount -NotePropertyValue $expected -Force
$m | Add-Member -NotePropertyName isolatedRestoreDatabaseRemoved -NotePropertyValue $true -Force
$m | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding UTF8 -NoNewline
Write-Output 'TASK4_WSL18=ISOLATED_RESTORE_VERIFIED_AND_DISPOSABLE_DB_REMOVED'
Write-Output 'TASK4_WSL18=RAILWAY_DATABASE_UNCHANGED'
Write-Output ('TASK3B_PRIVATE_RECOVERY_EVIDENCE=' + $receiptPath)
