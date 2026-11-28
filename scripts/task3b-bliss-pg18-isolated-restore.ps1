# Task #3B: independent, disposable PostgreSQL 18 restore on the owner's
# Windows Docker Desktop, *not* on live Railway or DigitalOcean DR362.
# Reads existing GPG-encrypted archive, decrypts only into a binary pipe,
# restores into an isolated --network none container, checks table count, and
# destroys that ephemeral container BEFORE writing a success receipt.
# Default: require independent DigitalOcean off-host SHA evidence.
# -LocalVerificationOnly: allow verified original .pending.json for a strictly
# local-only receipt; NEVER imply DigitalOcean verified or unlock production.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $OffhostManifestPath,
    # Strict local-only isolated restore from the existing .pending.json.
    # NOT a substitute for the DigitalOcean off-host verification receipt.
    [switch] $LocalVerificationOnly
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $OffhostManifestPath -PathType Leaf)) {
    throw 'TASK3B_RESTORE=OFFHOST_MANIFEST_MISSING'
}
$m = Get-Content -LiteralPath $OffhostManifestPath -Raw | ConvertFrom-Json
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -or
    $m.sourceEnvironmentId -cne 'b67a9836-36bf-4083-93f7-00c867b13f39' -or
    $m.sourceServiceId -cne '6aa78407-1632-48c9-8ea4-0289a7d36a5b' -or
    $m.sourceDatabase -cne 'bliss' -or $m.sourcePostgresMajor -ne 18 -or
    $m.encryption -cne 'GPG_RECIPIENT' -or
    $m.isolatedRestoreVerified -eq $true -or
    $m.archiveFileName -cnotmatch '^bliss-pg18-[a-zA-Z0-9-]+\.gpg$' -or
    $m.archiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or
    $null -eq $m.sourcePublicTableCount -or
    ([string]$m.sourcePublicTableCount) -cnotmatch '^[0-9]{1,8}$') {
    throw 'TASK3B_RESTORE=EVIDENCE_TARGET_OR_SOURCE_INVALID'
}
# Keep DigitalOcean proof strict in the original production-oriented path.
# This alternate mode is an isolated local-only test, never production evidence.
if ($LocalVerificationOnly) {
    if ($OffhostManifestPath -cnotmatch '\.pending\.json$' -or
        $m.offhostProvider -cne 'NOT_VERIFIED' -or
        $m.offhostSha256Verified -ne $false -or
        $null -ne $m.offhostDropletId) {
        throw 'TASK3B_LOCAL_RESTORE=REQUIRES_ORIGINAL_PENDING_MANIFEST'
    }
} else {
    if ($OffhostManifestPath -cnotmatch '\.offhost\.json$' -or
        $m.offhostProvider -cne 'DigitalOcean' -or
        $m.offhostSha256Verified -ne $true -or
        $m.offhostDropletId -cne '571399692' -or
        $m.offhostRemoteChecksum -cne $m.archiveSha256) {
        throw 'TASK3B_RESTORE=EVIDENCE_TARGET_OR_OFFHOST_NOT_VERIFIED'
    }
}
$archive = Join-Path (Split-Path -Parent $OffhostManifestPath) $m.archiveFileName
if (-not (Test-Path -LiteralPath $archive -PathType Leaf) -or
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.archiveSha256) {
    throw 'TASK3B_RESTORE=ARCHIVE_CHECKSUM_MISMATCH_OR_MISSING'
}
$docker = Get-Command docker.exe -ErrorAction SilentlyContinue
if ($null -eq $docker) { $docker = Get-Command docker -ErrorAction SilentlyContinue }
$gpg = Get-Command gpg.exe -ErrorAction SilentlyContinue
if ($null -eq $gpg) { $gpg = Get-Command gpg -ErrorAction SilentlyContinue }
if ($null -eq $docker -or $null -eq $gpg) {
    throw 'TASK3B_RESTORE=DOCKER18_AND_GPG_REQUIRED'
}
$container = 'bliss-pg18-restore-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$testDatabase = 'bliss_task3b_isolated_restore'
$started = $false
$verified = $false
$cleanupComplete = $false
$gpgProc = $null
$restoreProc = $null
try {
    # This creates a disposable LOCAL test container only, with no published
    # ports, no network, a memory limit, and no link to any production service.
    $created = @(& $docker.Source run --rm -d --name $container --network none --memory 512m -e POSTGRES_HOST_AUTH_METHOD=trust postgres:18 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw 'TASK3B_RESTORE=ISOLATED_DOCKER_START_FAILED'
    }
    $started = $true
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        & $docker.Source exec -u postgres $container pg_isready -q *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'TASK3B_RESTORE=ISOLATED_POSTGRES_NOT_READY' }
    & $docker.Source exec -u postgres $container createdb $testDatabase *> $null
    if ($LASTEXITCODE -ne 0) { throw 'TASK3B_RESTORE=ISOLATED_TEST_DB_CREATE_FAILED' }

    $gpgPsi = New-Object Diagnostics.ProcessStartInfo
    $gpgPsi.FileName = $gpg.Source
    $gpgPsi.Arguments = '--batch --no-tty --decrypt "' + $archive + '"'
    $gpgPsi.UseShellExecute = $false
    $gpgPsi.CreateNoWindow = $true
    $gpgPsi.RedirectStandardOutput = $true
    $gpgPsi.RedirectStandardError = $true
    $restorePsi = New-Object Diagnostics.ProcessStartInfo
    $restorePsi.FileName = $docker.Source
    $restorePsi.Arguments = 'exec -i -u postgres ' + $container + ' pg_restore --no-owner --no-acl --exit-on-error -d ' + $testDatabase
    $restorePsi.UseShellExecute = $false
    $restorePsi.CreateNoWindow = $true
    $restorePsi.RedirectStandardInput = $true
    $restorePsi.RedirectStandardError = $true

    $restoreProc = New-Object Diagnostics.Process
    $restoreProc.StartInfo = $restorePsi
    $gpgProc = New-Object Diagnostics.Process
    $gpgProc.StartInfo = $gpgPsi
    if (-not $restoreProc.Start()) { throw 'TASK3B_RESTORE=RESTORE_PIPE_START_FAILED' }
    if (-not $gpgProc.Start()) { throw 'TASK3B_RESTORE=DECRYPT_PIPE_START_FAILED' }
    # Stream bytes from decryption directly into pg_restore: no plaintext file.
    $gpgProc.StandardOutput.BaseStream.CopyTo($restoreProc.StandardInput.BaseStream)
    $restoreProc.StandardInput.Close()
    $gpgProc.WaitForExit()
    $restoreProc.WaitForExit()
    if ($gpgProc.ExitCode -ne 0 -or $restoreProc.ExitCode -ne 0) {
        throw 'TASK3B_RESTORE=GPG_OR_PG_RESTORE_FAILED'
    }

    $major = @(& $docker.Source exec -u postgres $container psql -XAt -v ON_ERROR_STOP=1 -d $testDatabase -c "SHOW server_version" 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($major -join ' ') -cnotmatch '^18\.') {
        throw 'TASK3B_RESTORE=SERVER_NOT_PG18'
    }
    $tables = @(& $docker.Source exec -u postgres $container psql -XAt -v ON_ERROR_STOP=1 -d $testDatabase -c "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public'" 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($tables -join ' ').Trim() -cnotmatch '^[0-9]+$' -or
        [int]($tables -join ' ').Trim() -ne [int]$m.sourcePublicTableCount) {
        throw 'TASK3B_RESTORE=RESTORED_TABLE_COUNT_MISMATCH'
    }
    $verified = $true
} finally {
    if ($null -ne $gpgProc) { $gpgProc.Dispose() }
    if ($null -ne $restoreProc) { $restoreProc.Dispose() }
    if ($started) {
        & $docker.Source rm -f $container *> $null
        $cleanupComplete = ($LASTEXITCODE -eq 0)
    }
}
if (-not $verified -or -not $cleanupComplete) {
    throw 'TASK3B_RESTORE=NOT_ACCEPTED_OR_LOCAL_TEST_CONTAINER_NOT_REMOVED'
}

$receiptPath = if ($LocalVerificationOnly) {
    $OffhostManifestPath -replace '\.pending\.json$', '.local-restore-evidence.json'
} else {
    $OffhostManifestPath -replace '\.offhost\.json$', '.recovery-evidence.json'
}
if ($receiptPath -ceq $OffhostManifestPath -or (Test-Path -LiteralPath $receiptPath)) {
    throw 'TASK3B_RESTORE=EVIDENCE_PATH_COLLISION'
}
$m.isolatedRestoreVerified = $true
$m | Add-Member -NotePropertyName isolatedRestoreAtUtc -NotePropertyValue ((Get-Date).ToUniversalTime().ToString('o')) -Force
$m | Add-Member -NotePropertyName isolatedRestoreEngine -NotePropertyValue 'Docker PostgreSQL 18 --network none' -Force
$m | Add-Member -NotePropertyName isolatedRestoreDatabase -NotePropertyValue $testDatabase -Force
$m | Add-Member -NotePropertyName isolatedRestorePublicTableCount -NotePropertyValue ([int]$m.sourcePublicTableCount) -Force
$m | Add-Member -NotePropertyName isolatedRestoreContainerRemoved -NotePropertyValue $true -Force
if ($LocalVerificationOnly) {
    $m | Add-Member -NotePropertyName isolatedRestoreAcceptanceScope -NotePropertyValue 'LOCAL_ONLY_NOT_PRODUCTION' -Force
}
$m | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding UTF8 -NoNewline
Write-Output 'TASK3B_BLISS_PG18_BACKUP_ENCRYPTED=PASS'
if ($LocalVerificationOnly) {
    Write-Output 'TASK3B_DIGITALOCEAN_OFFHOST_SHA256=NOT_VERIFIED'
    Write-Output 'TASK3B_GOOGLE_DRIVE_CLOUD_COPY=NOT_EVALUATED_BY_RESTORE_SCRIPT'
    Write-Output 'TASK3B_PG18_LOCAL_ISOLATED_RESTORE=PASS'
} else {
    Write-Output 'TASK3B_DIGITALOCEAN_OFFHOST_SHA256=PASS'
    Write-Output 'TASK3B_PG18_ISOLATED_RESTORE=PASS'
}
if ([int]$m.sourcePublicTableCount -eq 0) {
    Write-Output 'TASK3B_ADVERTISER_SCHEMA=NOT_ESTABLISHED_ZERO_PUBLIC_TABLES'
}
Write-Output 'TASK3B_HOSTED_CUSTOMER_REGISTRATION=NOT_TESTED'
Write-Output 'TASK3B_PUBLIC_SIGNUP=HELD'
Write-Output ("TASK3B_PRIVATE_RECOVERY_EVIDENCE=" + $receiptPath)
