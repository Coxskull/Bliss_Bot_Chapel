# ALPHA Task #3B — owner-only Google Drive + WSL evidence preparation.
# Read-only metadata verification. NO production database migration or customer writes.
# The exact archived ciphertext digest below was independently recomputed from
# the private Google Drive copy on 2026-11-21. This is a one-archive adapter;
# it does not assert that subsequent unrelated Drive files were verified.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ManifestPath
)
$ErrorActionPreference = 'Stop'
$expectedDigest = 'df487fcdeefb99db69956849c4b3ae5a1035456c8823b1b142fee4a0ff43fb3b'
$expectedArchive = 'bliss-pg18-20261121T023709Z-cda88608.gpg'
$manifestFile = Get-Item -LiteralPath $ManifestPath -ErrorAction Stop
if ($manifestFile.PSIsContainer -or $manifestFile.Name -cne 'bliss-pg18-20261121T023709Z-cda88608.pending.json') {
    throw 'TASK3B_DRIVE_WSL=REFUSED_MANIFEST_IDENTITY'
}
$m = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -or
    $m.sourceEnvironmentId -cne 'b67a9836-36bf-4083-93f7-00c867b13f39' -or
    $m.sourceServiceId -cne '6aa78407-1632-48c9-8ea4-0289a7d36a5b' -or
    $m.sourceDatabase -cne 'bliss' -or $m.sourcePostgresMajor -ne 18 -or
    $m.sourcePublicTableCount -ne 0 -or
    $m.archiveFileName -cne $expectedArchive -or
    $m.archiveSha256 -cne $expectedDigest -or
    $m.encryption -cne 'GPG_RECIPIENT' -or
    $m.offhostProvider -cne 'NOT_VERIFIED' -or
    $m.offhostSha256Verified -ne $false -or
    $m.isolatedRestoreVerified -ne $false) {
    throw 'TASK3B_DRIVE_WSL=REFUSED_SOURCE_PROVENANCE'
}
$archive = Join-Path $manifestFile.DirectoryName $expectedArchive
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
    throw 'TASK3B_DRIVE_WSL=ENCRYPTED_ARCHIVE_MISSING'
}
$actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -cne $expectedDigest) {
    throw 'TASK3B_DRIVE_WSL=LOCAL_ARCHIVE_SHA_MISMATCH'
}
$wsl = Get-Command wsl.exe -ErrorAction SilentlyContinue
if ($null -eq $wsl) { throw 'TASK3B_DRIVE_WSL=WSL_MISSING' }
# Read fixed identity/format metadata from the owner's existing, isolated
# Ubuntu PostgreSQL18 recovery database. No plaintext archive or secret key.
# This verifies current local metadata, not the historical restore transcript.
$sql = "SELECT current_database() || '|' || (current_setting('server_version_num')::int / 10000)::text || '|' || (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public')::text || '|' || (inet_server_addr() IS NULL)::text"
$output = @()
$exitCode = 1
$previousErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $output = @(& $wsl.Source -d Ubuntu -- psql -X -At -v ON_ERROR_STOP=1 -h /var/run/postgresql -d alpha_task3b_restore_202611 -c $sql 2>$null)
    $exitCode = $LASTEXITCODE
} finally {
    $ErrorActionPreference = $previousErrorActionPreference
}
if ($exitCode -ne 0 -or $output.Count -ne 1 -or ([string]$output[0]).Trim() -cne 'alpha_task3b_restore_202611|18|0|true') {
    throw 'TASK3B_DRIVE_WSL=LOCAL_PG18_RESTORE_READBACK_NOT_VERIFIED'
}
$now = [DateTimeOffset]::UtcNow
try { $captured = [DateTimeOffset]::Parse([string]$m.capturedAtUtc) }
catch { throw 'TASK3B_DRIVE_WSL=INVALID_CAPTURE_TIMESTAMP' }
if ($captured -gt $now -or $captured -lt $now.AddDays(-7)) {
    throw 'TASK3B_DRIVE_WSL=REVIEWED_EVIDENCE_WINDOW_EXPIRED'
}
# Do NOT modify the original encrypted archive or pending manifest.
# The pending manifest intentionally still says offhostProvider=NOT_VERIFIED;
# the separately named review receipt records the verified alternative.
$receipt = [ordered]@{
    schema = 'alpha-bliss-pg18-recovery/v1'
    evidenceScope = 'GOOGLE_DRIVE_WSL_ONE_ARCHIVE_OWNER_REVIEW'
    sourceProjectId = $m.sourceProjectId
    sourceEnvironmentId = $m.sourceEnvironmentId
    sourceServiceId = $m.sourceServiceId
    sourceDatabase = $m.sourceDatabase
    sourcePostgresMajor = 18
    sourcePublicTableCount = 0
    capturedAtUtc = $m.capturedAtUtc
    archiveFileName = $expectedArchive
    archiveSha256 = $expectedDigest
    encryption = 'GPG_RECIPIENT'
    offhostProvider = 'GoogleDrive'
    offhostSha256Verified = $true
    offhostVerificationMethod = 'INDEPENDENT_CONNECTED_DRIVE_RETRIEVED_CIPHERTEXT_SHA256_20261121'
    offhostVerifiedDateUtc = '2026-11-21'
    offhostPermissionScope = 'OWNER_ONLY'
    isolatedRestoreVerified = $true
    isolatedRestoreEngine = 'WSL2 PostgreSQL 18 local Unix socket'
    isolatedRestoreDatabase = 'alpha_task3b_restore_202611'
    isolatedRestorePublicTableCount = 0
    isolatedRestoreReadbackAtUtc = $now.ToString('o')
    isolatedRestoreEvidenceScope = 'OWNER_SCREENSHOT_AND_CURRENT_LOCAL_READBACK'
    ownerAttestationRequiredAtProductionInstall = $true
}
$out = Join-Path $manifestFile.DirectoryName 'bliss-pg18-20261121T023709Z-cda88608.drive-wsl-recovery-evidence.json'
if (Test-Path -LiteralPath $out) {
    throw 'TASK3B_DRIVE_WSL=RECEIPT_ALREADY_EXISTS_REFUSE_OVERWRITE'
}
$receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $out -Encoding UTF8 -NoNewline
Write-Output 'TASK3B_DRIVE_WSL_EVIDENCE=PREPARED_OWNER_REVIEW_REQUIRED'
Write-Output 'TASK3B_PRODUCTION_MIGRATION=NOT_EXECUTED'
Write-Output 'TASK3B_PUBLIC_REGISTRATION=HELD'
