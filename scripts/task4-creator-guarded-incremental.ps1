# ALPHA GLOBAL MEDIA — Task #4, existing Railway Bliss PostgreSQL 18 ONLY.
# One-shot ADDITIVE EF creator migration, held preview, no customer signup,
# no Factory STOP change. Requires a RECENT owner-only GPG backup captured AFTER
# the first real advertiser enrollment, independently SHA-verified on the
# existing DigitalOcean volume and restored on isolated local PostgreSQL 18.
# Never paste the manifest, archive, secrets or raw Railway output into chat.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string] $ReceiptPath)
$ErrorActionPreference = 'Stop'
$project = 'fb33c1b7-03fb-42c8-89b1-081951a738fd'
$envId = 'b67a9836-36bf-4083-93f7-00c867b13f39'
$postgres = '6aa78407-1632-48c9-8ea4-0289a7d36a5b'
$application = '8b22ff53-ccf5-4341-a473-ff146ca497f1'
# Two independent, fail-closed restoration engines are accepted:
# (1) original Docker --network none; or (2) owner's proven WSL2 Unix-socket
# PG18 with a NEW disposable DB destroyed and verified after restore.
# Neither accepts the historic backup that predates the real advertiser.
if (-not (Test-Path -LiteralPath $ReceiptPath -PathType Leaf) -or
    (Split-Path -Leaf $ReceiptPath) -cnotmatch '^bliss-pg18-[a-zA-Z0-9-]+\.recovery-evidence\.json$') {
    throw 'TASK4_CREATOR_SCHEMA=REFUSED_REAL_RECOVERY_EVIDENCE_FILE_REQUIRED'
}
try { $m = Get-Content -LiteralPath $ReceiptPath -Raw | ConvertFrom-Json }
catch { throw 'TASK4_CREATOR_SCHEMA=REFUSED_BAD_RECOVERY_EVIDENCE' }
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne $project -or $m.sourceEnvironmentId -cne $envId -or
    $m.sourceServiceId -cne $postgres -or $m.sourceDatabase -cne 'bliss' -or
    [int]$m.sourcePostgresMajor -ne 18 -or [int]$m.sourcePublicTableCount -lt 90 -or
    $m.encryption -cne 'GPG_RECIPIENT' -or
    $m.offhostProvider -cne 'DigitalOcean' -or $m.offhostDropletId -cne '571399692' -or
    $m.offhostSha256Verified -ne $true -or
    $m.isolatedRestoreVerified -ne $true -or
    $m.isolatedRestoreDatabase -ceq 'bliss' -or
    [int]$m.isolatedRestorePublicTableCount -ne [int]$m.sourcePublicTableCount -or
    $m.archiveSha256 -cnotmatch '^[0-9a-f]{64}$' -or
    $m.offhostRemoteChecksum -cne $m.archiveSha256 -or
    $m.archiveFileName -cnotmatch '^bliss-pg18-[a-zA-Z0-9-]+\.gpg$') {
    throw 'TASK4_CREATOR_SCHEMA=REFUSED_OFFHOST_OR_ISOLATED_RESTORE_UNVERIFIED'
}
$dockerRecovery = $m.isolatedRestoreEngine -ceq 'Docker PostgreSQL 18 --network none' -and
    $m.isolatedRestoreContainerRemoved -eq $true
$wslRecovery = $m.isolatedRestoreEngine -ceq 'WSL2 Ubuntu PostgreSQL 18 Unix socket temporary database' -and
    $m.isolatedRestoreTransport -ceq 'UNIX_SOCKET_ONLY' -and
    $m.isolatedRestoreDatabaseRemoved -eq $true -and
    ([string]$m.isolatedRestoreDatabase) -cmatch '^alpha_task4_wsl_restore_'
if (-not ($dockerRecovery -or $wslRecovery)) {
    throw 'TASK4_CREATOR_SCHEMA=REFUSED_DOCKER_OR_WSL18_RECOVERY_NOT_VERIFIED'
}
try {
    $captured = [DateTimeOffset]::Parse([string]$m.capturedAtUtc)
    $offhost = [DateTimeOffset]::Parse([string]$m.offhostVerifiedAtUtc)
    $restored = [DateTimeOffset]::Parse([string]$m.isolatedRestoreAtUtc)
} catch { throw 'TASK4_CREATOR_SCHEMA=REFUSED_BAD_EVIDENCE_TIMESTAMPS' }
$now = [DateTimeOffset]::UtcNow
# Known approved real advertiser record at 2026-11-21 07:07:25 UTC.
# The old 05:07 archive is genuine but cannot cover that applicant.
if ($captured -le [DateTimeOffset]::Parse('2026-11-21T07:07:25Z') -or
    $captured -lt $now.AddHours(-6) -or $captured -gt $now.AddMinutes(5) -or
    $offhost -lt $captured -or $restored -lt $offhost -or
    $restored -gt $now.AddMinutes(5)) {
    throw 'TASK4_CREATOR_SCHEMA=REFUSED_STALE_OR_OUT_OF_ORDER_BACKUP'
}
$archivePath = Join-Path (Split-Path -Parent $ReceiptPath) $m.archiveFileName
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf) -or
    (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.archiveSha256) {
    throw 'TASK4_CREATOR_SCHEMA=REFUSED_ARCHIVE_SHA256_MISMATCH'
}
$railway = Get-Command railway.cmd -ErrorAction SilentlyContinue
if ($null -eq $railway) { throw 'TASK4_CREATOR_SCHEMA=REFUSED_RAILWAY_CLI_REQUIRED' }
& $railway.Source whoami *> $null
if ($LASTEXITCODE -ne 0) { throw 'TASK4_CREATOR_SCHEMA=REFUSED_RAILWAY_OWNER_LOGIN_REQUIRED' }
# Before any database DDL, require the authorized owner to attest that the
# backup is current and recoverable; the private document never leaves the PC.
Write-Output 'TASK4_CREATOR_BACKUP=RECENT_DIGITALOCEAN_AND_ISOLATED_RESTORE_METADATA_VERIFIED'
Write-Output 'TASK4_CREATOR_TARGET=EXISTING_BLISS_PRODUCTION_PG18'
Write-Output 'TASK4_CREATOR_ADVERTISER_AND_GLOBAL_STOP=UNCHANGED'
$ack = (Read-Host 'Type BLISS-CREATOR-CURRENT-RECOVERY-VERIFIED if the backup still covers current customer data and your decryption key is recoverable').Trim()
if ($ack -cne 'BLISS-CREATOR-CURRENT-RECOVERY-VERIFIED') {
    throw 'TASK4_CREATOR_SCHEMA=REFUSED_OWNER_CURRENT_RECOVERY_ATTESTATION'
}
$receiptHash = (Get-FileHash -LiteralPath $ReceiptPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($receiptHash -cnotmatch '^[0-9a-f]{64}$') {
    throw 'TASK4_CREATOR_SCHEMA=REFUSED_EVIDENCE_DIGEST'
}
$remote = @'
set -euo pipefail
if [ "$RAILWAY_PROJECT_ID" != "fb33c1b7-03fb-42c8-89b1-081951a738fd" ] ||
   [ "$RAILWAY_ENVIRONMENT_ID" != "b67a9836-36bf-4083-93f7-00c867b13f39" ] ||
   [ "$RAILWAY_SERVICE_ID" != "8b22ff53-ccf5-4341-a473-ff146ca497f1" ] ||
   [ "$ASPNETCORE_ENVIRONMENT" != "Production" ] ||
   [ "$Runtime__RestrictedPreview" != "true" ] ||
   [ "$Authentication__AdvertiserRegistrationRequested" != "true" ] ||
   [ "${Authentication__CreatorRegistrationRequested:-false}" = "true" ]; then
    echo "TASK4_CREATOR_SCHEMA=REFUSED_REMOTE_IDENTITY_OR_FLAGS"
    exit 2
fi
[ -n "$BLISS_INITIAL_INSTALL_EXPECTED_DB_HOST" ] &&
[ "$BLISS_INITIAL_INSTALL_EXPECTED_DB_NAME" = "bliss" ] || exit 2
export BLISS_TASK4_CREATOR_RECOVERY_MANIFEST_SHA256=__RECOVERY_DIGEST__
exec /usr/local/bin/bliss-railway-entrypoint --migrate --task4-creator-incremental
'@
$remote = $remote.Replace('__RECOVERY_DIGEST__', $receiptHash)
# Railway's verified Windows PowerShell 5.1 SSH bridge does not forward
# piped local stdin reliably. Encode only the REVIEWED source (no DB rows or
# credentials), decode on the already running service and run in remote bash.
$remote64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($remote))
$runner = 'set -o pipefail; printf %s ' + $remote64 + ' | base64 -d | bash'
$raw = @()
$exit = 1
$previousPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $raw = @(& $railway.Source ssh -p $project -s bliss-bot -e production -- bash -c $runner 2>&1)
    $exit = $LASTEXITCODE
} catch {
    $exit = 1
} finally {
    $ErrorActionPreference = $previousPreference
}
$passed = @($raw | Where-Object {
    ([string]$_).Trim() -ceq 'TASK4_CREATOR_INCREMENTAL_SCHEMA=PASS_PREVIEW_AND_ADVERTISER_UNCHANGED' -or
    ([string]$_).Trim() -ceq 'TASK4_CREATOR_INCREMENTAL_SCHEMA=ALREADY_PRESENT_VERIFIED'
}).Count
# Raw SSH/provider errors could contain private host/DB context: no printing.
if ($exit -ne 0 -or $passed -ne 1) {
    throw 'TASK4_CREATOR_SCHEMA=NOT_VERIFIED; CREATOR_SIGNUP_REMAINS_OFF'
}
Write-Output 'TASK4_CREATOR_SCHEMA_MIGRATION=PASS'
Write-Output 'TASK4_CREATOR_PRODUCTION_SIGNUP=STILL_OFF_PENDING_HOSTED_ACCEPTANCE'
Write-Output 'TASK4_CREATOR_ADVERTISER_AND_GLOBAL_STOP=UNCHANGED'
