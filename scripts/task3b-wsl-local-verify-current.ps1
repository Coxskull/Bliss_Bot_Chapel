# ALPHA Task3B: owner-only LOCAL verification of a current encrypted Bliss PG18
# backup in a newly created WSL Ubuntu PG18 socket-only DB, then deletion.
# Does not attest offhost custody, execute migration or change production.
[CmdletBinding()]
param(
  [string]$ManifestPath = (Join-Path (Join-Path $HOME 'Downloads') 'bliss-pg18-20261122T055153Z-b34126b0.pending.json')
)
$ErrorActionPreference='Stop'
$project='fb33c1b7-03fb-42c8-89b1-081951a738fd'
$envId='b67a9836-36bf-4083-93f7-00c867b13f39'
$postgres='6aa78407-1632-48c9-8ea4-0289a7d36a5b'
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf) -or
    (Split-Path -Leaf $ManifestPath) -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.pending\.json$') {
  throw 'TASK3B_WSL_LOCAL=REFUSED_OWNER_PENDING_MANIFEST_MISSING'
}
try { $m=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json }
catch { throw 'TASK3B_WSL_LOCAL=REFUSED_PENDING_MANIFEST_INVALID' }
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne $project -or $m.sourceEnvironmentId -cne $envId -or
    $m.sourceServiceId -cne $postgres -or $m.sourceDatabase -cne 'bliss' -or
    [int]$m.sourcePostgresMajor -ne 18 -or [int]$m.sourcePublicTableCount -lt 90 -or
    $m.encryption -cne 'GPG_RECIPIENT' -or
    $m.offhostProvider -cne 'NOT_VERIFIED' -or $m.offhostSha256Verified -ne $false -or
    $m.isolatedRestoreVerified -ne $false -or
    $m.archiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or
    $m.gpgRecipientFingerprint -cnotmatch '^[A-F0-9]{40}$' -or
    $m.archiveFileName -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.gpg$') {
  throw 'TASK3B_WSL_LOCAL=REFUSED_TARGET_OR_CUSTODY_FIELDS'
}
if ($m.archiveFileName -cne ((Split-Path -Leaf $ManifestPath) -replace '\.pending\.json$', '.gpg')) {
  throw 'TASK3B_WSL_LOCAL=REFUSED_ARCHIVE_MANIFEST_PAIR_MISMATCH'
}
try { $captured=[DateTimeOffset]::Parse([string]$m.capturedAtUtc) }
catch { throw 'TASK3B_WSL_LOCAL=REFUSED_CAPTURE_DATE' }
if ($captured -le [DateTimeOffset]::Parse('2026-11-21T07:07:25Z') -or
    $captured -gt [DateTimeOffset]::UtcNow.AddMinutes(5)) {
  throw 'TASK3B_WSL_LOCAL=REFUSED_HISTORIC_OR_FUTURE_ARCHIVE'
}
$archive=Join-Path (Split-Path -Parent $ManifestPath) ([string]$m.archiveFileName)
if (-not (Test-Path -LiteralPath $archive -PathType Leaf) -or
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.archiveSha256) {
  throw 'TASK3B_WSL_LOCAL=REFUSED_CIPHERTEXT_SHA256'
}
$wsl=Get-Command wsl.exe -ErrorAction SilentlyContinue
$gpg=Get-Command gpg.exe -ErrorAction SilentlyContinue
$helper=Join-Path $PSScriptRoot 'task4-creator-wsl-pg18-restore.sh'
if ($null -eq $wsl -or $null -eq $gpg -or -not (Test-Path -LiteralPath $helper -PathType Leaf)) {
  throw 'TASK3B_WSL_LOCAL=REFUSED_EXISTING_WSL_PG18_GPG_OR_HELPER_NOT_READY'
}
$receipt=$ManifestPath -replace '\.pending\.json$', '.local-wsl-restore-evidence.json'
if ($receipt -ceq $ManifestPath -or (Test-Path -LiteralPath $receipt)) {
  throw 'TASK3B_WSL_LOCAL=REFUSED_LOCAL_RECEIPT_ALREADY_EXISTS'
}
# Normalize ONLY the reviewed Bash helper source. Never modify ciphertext.
$source=(Get-Content -LiteralPath $helper -Raw).Replace("`r`n", "`n").Replace("`r", "")
$helper64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($source))
$archive64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($archive))
$gpg64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($gpg.Source))
$count=[int]$m.sourcePublicTableCount
$cmd='set -o pipefail; printf %s '+$helper64+' | base64 -d | bash -s -- '+$archive64+' '+$count+' '+$gpg64
$raw=@()
$exitCode=1
$prior=$ErrorActionPreference
try {
  $ErrorActionPreference='Continue'
  $raw=@(& $wsl.Source -d Ubuntu -u root -- bash -lc $cmd 2>&1)
  $exitCode=$LASTEXITCODE
} catch { $exitCode=1 } finally { $ErrorActionPreference=$prior }
$passCount=@($raw | Where-Object { ([string]$_).Trim() -ceq 'TASK4_WSL_PG18_RESTORE=PASS' }).Count
if ($exitCode -ne 0 -or $passCount -ne 1) {
  # Only a fixed allowlisted stage from the reviewed helper is permitted.
  # NEVER print raw WSL, GPG, pg_restore errors, local paths or archive rows.
  $allowedStages=@('ARGUMENTS','NOT_ROOT','COUNT_INVALID','TOOLS_MISSING',
    'PG18_BINARY_MISSING','PG18_VERSION_MISMATCH','PG18_CLUSTER_NOT_READY',
    'PG18_SOCKET_MISSING','PG18_QUERY_REFUSED','PG18_MAJOR_MISMATCH',
    'ARCHIVE_PATH_INVALID','GPG_EXE_MISSING','TEMP_DB_NAME_INVALID',
    'TEMP_DB_CHECK_REFUSED','TEMP_DB_EXISTS','TEMP_DB_CREATE_REFUSED',
    'GPG_DECRYPT_REFUSED','PG_RESTORE_REFUSED','TABLE_COUNT_QUERY_REFUSED',
    'TABLE_COUNT_MISMATCH','EF_HISTORY_QUERY_REFUSED','EF_HISTORY_MISSING',
    'TEMP_DB_REMOVE_REFUSED','TEMP_DB_CLEANUP_UNVERIFIED','UNKNOWN')
  $stages=@($raw | ForEach-Object {
    $line=([string]$_).Trim()
    if ($line -cmatch '^TASK4_WSL_PG18_STAGE=([A-Z_]+)$' -and
        $allowedStages -ccontains $Matches[1]) { $Matches[1] }
  } | Select-Object -Unique)
  if ($stages.Count -eq 1) {
    Write-Output ('TASK3B_WSL_LOCAL_STAGE=' + $stages[0])
  } else {
    Write-Output 'TASK3B_WSL_LOCAL_STAGE=TRANSPORT_OR_UNCLASSIFIED'
  }
  throw 'TASK3B_WSL_LOCAL=ISOLATED_RESTORE_NOT_VERIFIED'
}
# Independently verified Drive storage is NOT attested by this local script.
# Preserve the original .pending.json unchanged and mark this receipt local-only.
$report=[ordered]@{
  schema='alpha-bliss-pg18-local-wsl-verification/v1'
  sourceProjectId=$project; sourceEnvironmentId=$envId
  sourceServiceId=$postgres; sourceDatabase='bliss'; sourcePostgresMajor=18
  sourcePublicTableCount=$count; archiveFileName=[string]$m.archiveFileName
  archiveSha256=[string]$m.archiveSha256; capturedAtUtc=[string]$m.capturedAtUtc
  localRestoreVerifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
  isolatedRestoreEngine='WSL2 Ubuntu PostgreSQL 18 Unix socket temporary database'
  isolatedRestoreVerified=$true; isolatedRestoreDatabaseRemoved=$true
  localOnly=$true; offhostVerifiedByThisScript=$false; productionMigrationAuthorized=$false
}
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $receipt -Encoding UTF8 -NoNewline
Write-Output 'TASK3B_WSL_LOCAL=PASS_TEMPORARY_PG18_RESTORE_AND_CLEANUP'
Write-Output 'TASK3B_WSL_LOCAL=PRODUCTION_DB_UNCHANGED'
Write-Output 'TASK3B_WSL_LOCAL=OFFHOST_NOT_ATTESTED_BY_THIS_SCRIPT'
Write-Output 'TASK4_CREATOR_SCHEMA=HELD'
