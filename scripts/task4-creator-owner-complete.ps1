# ALPHA GLOBAL MEDIA — Task #4 ONE-PC OWNER HANDOFF.
# Reuses existing reviewed capture -> DigitalOcean ciphertext -> NEW disposable
# WSL2 PG18 restore (default, NO Docker) -> guarded creator-only migration. It is NOT an unattended agent.
# No new Railway/DO resources, public database port, plaintext backup file,
# OAuth secret, Factory STOP or live-payment activation.
# Existing NEW fresh verified evidence can be reused with -ReceiptPath.
# Prior Nov21 99-table WSL restore remains accepted, never re-run here.
[CmdletBinding()]
param(
    [string] $ReceiptPath = '',
    [string] $DropletIPv4 = '',
    [string] $SshUser = '',
    [string] $RemoteDirectory = '',
    [string] $OutputDirectory = (Join-Path $HOME 'Downloads'),
    # WSL is the owner's already verified 99-table recovery environment.
    # Docker remains supported for operators who actually have it installed.
    [ValidateSet('WSL','Docker')][string] $RestoreEngine = 'WSL'
)
$ErrorActionPreference = 'Stop'
$migration = Join-Path $PSScriptRoot 'task4-creator-guarded-incremental.ps1'
if (-not (Test-Path -LiteralPath $migration -PathType Leaf)) {
    throw 'TASK4_OWNER_COMPLETE=GUARDED_SCRIPT_MISSING'
}
if (-not [string]::IsNullOrWhiteSpace($ReceiptPath)) {
    # Fresh full recovery-evidence receipts only; owner migration script
    # independently verifies exact target, age, SHA and post-applicant date.
    & $migration -ReceiptPath $ReceiptPath
    if (-not $?) { throw 'TASK4_OWNER_COMPLETE=EXISTING_EVIDENCE_NOT_VERIFIED' }
    return
}
$backup = Join-Path $PSScriptRoot 'task3b-owner-start-backup.ps1'
$offhost = Join-Path $PSScriptRoot 'task3b-bliss-pg18-offhost.ps1'
$restore = if ($RestoreEngine -ceq 'WSL') {
    Join-Path $PSScriptRoot 'task4-creator-wsl-pg18-isolated-restore.ps1'
} else {
    Join-Path $PSScriptRoot 'task3b-bliss-pg18-isolated-restore.ps1'
}
foreach ($path in @($backup, $offhost, $restore)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'TASK4_OWNER_COMPLETE=EXISTING_RECOVERY_HELPER_MISSING'
    }
}
if (-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)) {
    throw 'TASK4_OWNER_COMPLETE=PRIVATE_DOWNLOADS_DIRECTORY_MISSING'
}
# These values are not passwords; use the existing DigitalOcean Droplet 571399692
# and its Bliss-only mounted volume path, NEVER DR362/Supabase Factory storage.
if ([string]::IsNullOrWhiteSpace($DropletIPv4)) {
    $DropletIPv4 = (Read-Host 'Existing DigitalOcean recovery droplet IPv4').Trim()
}
if ([string]::IsNullOrWhiteSpace($SshUser)) {
    $SshUser = (Read-Host 'Existing limited SSH user').Trim()
}
if ([string]::IsNullOrWhiteSpace($RemoteDirectory)) {
    $RemoteDirectory = (Read-Host 'Existing Bliss-only encrypted archive directory on attached volume').Trim()
}
Write-Output 'TASK4_OWNER_COMPLETE=CAPTURE_FRESH_CURRENT_BLISS_PG18'
# Tee to the local interactive console is needed: the existing reviewed
# backup helper displays the public GPG fingerprints before Read-Host.
& $backup -OutputDirectory $OutputDirectory | Tee-Object -Variable captured | Out-Host
if (-not $?) { throw 'TASK4_OWNER_COMPLETE=ENCRYPTED_CAPTURE_FAILED' }
$manifest = @($captured | Where-Object { $_ -cmatch '^TASK3B_MANIFEST=(.+)$' })
if ($manifest.Count -ne 1) { throw 'TASK4_OWNER_COMPLETE=CAPTURE_RECEIPT_MISSING' }
$pendingPath = ([string]$manifest[0]).Substring('TASK3B_MANIFEST='.Length)
if (-not (Test-Path -LiteralPath $pendingPath -PathType Leaf)) {
    throw 'TASK4_OWNER_COMPLETE=CAPTURE_FILE_MISSING'
}
Write-Output 'TASK4_OWNER_COMPLETE=VERIFY_ENCRYPTED_COPY_ON_EXISTING_DIGITALOCEAN_VOLUME'
& $offhost -ManifestPath $pendingPath -DropletIPv4 $DropletIPv4 -SshUser $SshUser -RemoteDirectory $RemoteDirectory |
    Tee-Object -Variable copied | Out-Host
if (-not $?) { throw 'TASK4_OWNER_COMPLETE=OFFHOST_COPY_FAILED' }
$offhostLine = @($copied | Where-Object { $_ -cmatch '^TASK3B_OFFHOST_MANIFEST=(.+)$' })
if ($offhostLine.Count -ne 1) { throw 'TASK4_OWNER_COMPLETE=OFFHOST_RECEIPT_MISSING' }
$offhostPath = ([string]$offhostLine[0]).Substring('TASK3B_OFFHOST_MANIFEST='.Length)
Write-Output ('TASK4_OWNER_COMPLETE=VERIFY_DISPOSABLE_' + $RestoreEngine + '_PG18_RESTORE')
& $restore -OffhostManifestPath $offhostPath | Tee-Object -Variable restored | Out-Host
if (-not $?) { throw 'TASK4_OWNER_COMPLETE=ISOLATED_RESTORE_FAILED' }
$restoredLine = @($restored | Where-Object { $_ -cmatch '^TASK3B_PRIVATE_RECOVERY_EVIDENCE=(.+)$' })
if ($restoredLine.Count -ne 1) { throw 'TASK4_OWNER_COMPLETE=ISOLATED_RECEIPT_MISSING' }
$recoveryPath = ([string]$restoredLine[0]).Substring('TASK3B_PRIVATE_RECOVERY_EVIDENCE='.Length)
if (-not (Test-Path -LiteralPath $recoveryPath -PathType Leaf)) {
    throw 'TASK4_OWNER_COMPLETE=RECOVERY_FILE_MISSING'
}
Write-Output 'TASK4_OWNER_COMPLETE=GUARDED_CREATOR_INCREMENTAL_MIGRATION'
& $migration -ReceiptPath $recoveryPath
if (-not $?) { throw 'TASK4_OWNER_COMPLETE=MIGRATION_NOT_VERIFIED' }
Write-Output 'TASK4_OWNER_COMPLETE=SCHEMA_PASS_CREATOR_PUBLIC_FLAG_STILL_OFF'
