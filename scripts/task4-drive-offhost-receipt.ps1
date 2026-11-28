# Owner-only Google Drive alternative for BLISS PG18. No credential export,
# upload, network call, plaintext dump or DB writes. The operator must first
# upload BOTH the fresh GPG archive and pending manifest to the approved private
# folder, obtain independent connected Drive readback of both object IDs,
# owner-only permissions, and SHA256, then download the encrypted archive again.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$ManifestPath,
 [Parameter(Mandatory=$true)][string]$DownloadedArchivePath,
 [Parameter(Mandatory=$true)][string]$DriveArchiveId,
 [Parameter(Mandatory=$true)][string]$DriveManifestId,
 [Parameter(Mandatory=$true)][string]$VerifiedRemoteSha256
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'task4-offhost-custody.ps1')
if ((Split-Path -Leaf $ManifestPath) -cnotmatch '^bliss-pg18-[A-Za-z0-9-]+\.pending\.json$' -or
    -not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw 'TASK4_DRIVE=REFUSED_PENDING_MANIFEST' }
$m=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -or
    $m.sourceEnvironmentId -cne 'b67a9836-36bf-4083-93f7-00c867b13f39' -or
    $m.sourceServiceId -cne '6aa78407-1632-48c9-8ea4-0289a7d36a5b' -or
    $m.sourceDatabase -cne 'bliss' -or [int]$m.sourcePostgresMajor -ne 18 -or
    [int]$m.sourcePublicTableCount -lt 90 -or $m.encryption -cne 'GPG_RECIPIENT' -or
    $m.archiveFileName -cnotmatch '^bliss-pg18-[A-Za-z0-9-]+\.gpg$' -or
    $m.archiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or
    $m.offhostSha256Verified -eq $true -or $m.isolatedRestoreVerified -eq $true) { throw 'TASK4_DRIVE=REFUSED_SOURCE_MANIFEST' }
$now=[DateTimeOffset]::UtcNow
$captured=[DateTimeOffset]::Parse([string]$m.capturedAtUtc)
if ($captured -le [DateTimeOffset]::Parse('2026-11-21T07:07:25Z') -or $captured -lt $now.AddHours(-6) -or $captured -gt $now.AddMinutes(5)) { throw 'TASK4_DRIVE=REFUSED_STALE_CAPTURE' }
$original=Join-Path (Split-Path -Parent $ManifestPath) $m.archiveFileName
if (-not (Test-Path -LiteralPath $original -PathType Leaf) -or -not (Test-Path -LiteralPath $DownloadedArchivePath -PathType Leaf)) { throw 'TASK4_DRIVE=REFUSED_MISSING_CIPHERTEXT' }
if ((Get-Item -LiteralPath $original).FullName -ieq (Get-Item -LiteralPath $DownloadedArchivePath).FullName) { throw 'TASK4_DRIVE=REFUSED_SAME_LOCAL_FILE' }
if ((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.archiveSha256 -or
    (Get-FileHash -LiteralPath $DownloadedArchivePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.archiveSha256 -or
    $VerifiedRemoteSha256 -cne $m.archiveSha256) { throw 'TASK4_DRIVE=REFUSED_SHA256_MISMATCH' }
if ($DriveArchiveId -cnotmatch '^[A-Za-z0-9_-]{10,200}$' -or $DriveManifestId -cnotmatch '^[A-Za-z0-9_-]{10,200}$' -or $DriveArchiveId -ceq $DriveManifestId) { throw 'TASK4_DRIVE=REFUSED_OBJECT_IDENTITIES' }
$ack=(Read-Host 'Only after independent Drive verification of both exact objects, owner-only permissions, remote ciphertext SHA256, and a separate download, type BLISS-DRIVE-CUSTODY-VERIFIED').Trim()
if ($ack -cne 'BLISS-DRIVE-CUSTODY-VERIFIED') { throw 'TASK4_DRIVE=REFUSED_OWNER_ATTESTATION' }
$m | Add-Member -NotePropertyName offhostProvider -NotePropertyValue 'GoogleDrive' -Force
$m | Add-Member -NotePropertyName offhostDriveFolderId -NotePropertyValue '1i3IQ1hFB8H6FazZXtgstZQq9OX-TgMyM' -Force
$m | Add-Member -NotePropertyName offhostDriveArchiveId -NotePropertyValue $DriveArchiveId -Force
$m | Add-Member -NotePropertyName offhostDriveManifestId -NotePropertyValue $DriveManifestId -Force
foreach($key in @('offhostDriveOwnerOnlyAttested','offhostDriveIndependentReadbackAttested','offhostDriveDownloadedCiphertextVerified','offhostSha256Verified')) { $m | Add-Member -NotePropertyName $key -NotePropertyValue $true -Force }
$m | Add-Member -NotePropertyName offhostRemoteChecksum -NotePropertyValue $VerifiedRemoteSha256 -Force
$m | Add-Member -NotePropertyName offhostVerifiedAtUtc -NotePropertyValue ([DateTimeOffset]::UtcNow.ToString('o')) -Force
if (-not (Test-Task4OffhostCustody $m)) { throw 'TASK4_DRIVE=REFUSED_CUSTODY' }
$out=$ManifestPath -replace '\.pending\.json$', '.offhost.json'
if ($out -ceq $ManifestPath -or (Test-Path -LiteralPath $out)) { throw 'TASK4_DRIVE=REFUSED_RECEIPT_COLLISION' }
$m | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $out -Encoding UTF8 -NoNewline
Write-Output 'TASK4_DRIVE=OWNER_ATTESTED_CUSTODY_AND_DOWNLOADED_CIPHERTEXT_SHA256_VERIFIED'
Write-Output 'TASK4_CREATOR_RESTORE=HELD'
Write-Output ('TASK3B_OFFHOST_MANIFEST=' + $out)
