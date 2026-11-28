# Offline Task4 owner handoff test: fake all helpers; NEVER connect to Railway, GPG, WSL or DigitalOcean.
$ErrorActionPreference = 'Stop'
$directory = Join-Path ([IO.Path]::GetTempPath()) ('alpha-task4-fixture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$env:TASK4_RECOVERY_TEST_DIR = $directory
try {
    Copy-Item (Join-Path $PSScriptRoot 'task4-creator-owner-complete.ps1') (Join-Path $directory 'task4-creator-owner-complete.ps1')
    @'
param([string]$OutputDirectory)
$pending = Join-Path $OutputDirectory 'bliss-pg18-20261124T000000Z-00000000.pending.json'
'{}' | Set-Content -LiteralPath $pending
Write-Output ('TASK3B_MANIFEST=' + $pending)
'@ | Set-Content -LiteralPath (Join-Path $directory 'task3b-owner-start-backup.ps1')
    @'
param([string]$ManifestPath,[string]$DropletIPv4,[string]$SshUser,[string]$RemoteDirectory,[switch]$PreflightOnly)
if ($DropletIPv4 -cne '159.89.238.136' -or $SshUser -cne 'fixture' -or $RemoteDirectory -cne '/mnt/bliss-only') { throw 'MISMATCH' }
if ($PreflightOnly) {
    if ($env:TASK4_REFUSE_PREFLIGHT -eq 'YES') { throw 'MOCK_OFFHOST_UNAVAILABLE' }
    Write-Output 'TASK3B_OFFHOST_READONLY_PREFLIGHT_READY'
    return
}
$receipt = $ManifestPath -replace '\.pending\.json$', '.offhost.json'
'{}' | Set-Content -LiteralPath $receipt
Write-Output ('TASK3B_OFFHOST_MANIFEST=' + $receipt)
'@ | Set-Content -LiteralPath (Join-Path $directory 'task3b-bliss-pg18-offhost.ps1')
    @'
param([string]$OffhostManifestPath)
$receipt = $OffhostManifestPath -replace '\.offhost\.json$', '.recovery-evidence.json'
'{}' | Set-Content -LiteralPath $receipt
Write-Output ('TASK3B_PRIVATE_RECOVERY_EVIDENCE=' + $receipt)
'@ | Set-Content -LiteralPath (Join-Path $directory 'task4-creator-wsl-pg18-isolated-restore.ps1')
    @'
param([string]$ReceiptPath)
Set-Content -LiteralPath (Join-Path $env:TASK4_RECOVERY_TEST_DIR 'UNSAFE_MIGRATION_INVOKED') -Value $ReceiptPath
'@ | Set-Content -LiteralPath (Join-Path $directory 'task4-creator-guarded-incremental.ps1')
    $launcher = Join-Path $directory 'task4-creator-owner-complete.ps1'
    $env:TASK4_REFUSE_PREFLIGHT = 'YES'
    $blocked = $false
    try { & $launcher -RecoveryOnly -DropletIPv4 '159.89.238.136' -SshUser fixture -RemoteDirectory '/mnt/bliss-only' -OutputDirectory $directory | Out-Null }
    catch { if ([string]$_ -match 'MOCK_OFFHOST_UNAVAILABLE') { $blocked = $true } }
    finally { Remove-Item Env:TASK4_REFUSE_PREFLIGHT -ErrorAction SilentlyContinue }
    if (-not $blocked -or (Test-Path (Join-Path $directory 'bliss-pg18-20261124T000000Z-00000000.pending.json'))) { throw 'CAPTURE_OCCURRED_BEFORE_VALIDATED_DESTINATION' }
    $result = @(& $launcher -RecoveryOnly -DropletIPv4 '159.89.238.136' -SshUser fixture -RemoteDirectory '/mnt/bliss-only' -OutputDirectory $directory)
    if ($result -cnotcontains 'TASK4_OWNER_COMPLETE=OFFHOST_AND_ISOLATED_RECOVERY_VERIFIED' -or
        $result -cnotcontains 'TASK4_OWNER_COMPLETE=PRODUCTION_CREATOR_MIGRATION_HELD' -or
        $result -cnotcontains 'TASK4_OWNER_COMPLETE=CREATOR_PUBLIC_FLAG_STILL_OFF') {
        throw 'RECOVERY_ONLY_RESULT_MISSING'
    }
    if (Test-Path (Join-Path $directory 'UNSAFE_MIGRATION_INVOKED')) { throw 'PRODUCTION_MIGRATION_CALLED' }
    if (-not (Test-Path (Join-Path $directory 'bliss-pg18-20261124T000000Z-00000000.recovery-evidence.json'))) {
        throw 'MOCK_RECOVERY_RECEIPT_MISSING'
    }
    $denied = $false
    try {
        & $launcher -RecoveryOnly -ReceiptPath (Join-Path $directory 'existing.recovery-evidence.json') | Out-Null
    } catch {
        if ([string]$_ -match 'RECOVERY_ONLY_RECEIPT_PATH_REFUSED') { $denied = $true }
    }
    if (-not $denied -or (Test-Path (Join-Path $directory 'UNSAFE_MIGRATION_INVOKED'))) {
        throw 'RECOVERY_ONLY_SHORTCUT_NOT_REFUSED'
    }
    Write-Output 'TASK4_RECOVERY_ONLY_OWNER_HANDOFF=PASS_SYNTHETIC_NO_PRODUCTION'
} finally {
    Remove-Item Env:TASK4_RECOVERY_TEST_DIR -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $directory -Recurse -Force -ErrorAction SilentlyContinue
}
