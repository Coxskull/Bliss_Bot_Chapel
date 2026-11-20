# Task #3B: transfer ONLY the already GPG-encrypted Bliss PG18 dump to
# the existing owner-controlled DigitalOcean droplet. Never upload plaintext.
# Requires OpenSSH client, the established SSH host key/owner identity, and
# explicit separate storage directory (not the DR362 database directory).
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ManifestPath,
    [Parameter(Mandatory = $true)][ValidatePattern('^([0-9]{1,3}\.){3}[0-9]{1,3}$')][string] $DropletIPv4,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z_][a-z0-9_-]*$')][string] $SshUser,
    [Parameter(Mandatory = $true)][string] $RemoteDirectory
)
$ErrorActionPreference = 'Stop'
# Pin the actual existing DigitalOcean Droplet 571399692, verified through
# the connected owner account on 2026-11-20. Refuse an unrelated SSH target.
# This public-source commitment prevents accidentally targeting a different
# host without publishing the current infrastructure IP in repository code.
# It is NOT a secret or substitute for verified OpenSSH host-key checking.
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $hostDigest = ([BitConverter]::ToString(
        $sha.ComputeHash([Text.Encoding]::ASCII.GetBytes($DropletIPv4))
    )).Replace('-', '').ToLowerInvariant()
} finally { $sha.Dispose() }
if ($hostDigest -cne '007cf7213de8a2ca89232d840afe2e1d98e2e69cef221acb6fbbbdc94c5fbd83') {
    throw 'TASK3B_OFFHOST=REFUSED_DROPLET_IDENTITY; destination is not the existing owner DigitalOcean droplet.'
}
if ($RemoteDirectory -cnotmatch '^/[a-zA-Z0-9_.\/-]+$' -or
    $RemoteDirectory -match '(^|/)\.\.(/|$)' -or
    $RemoteDirectory -match '(?i)dr362|supabase|factory' -or
    $RemoteDirectory -eq '/') {
    throw 'TASK3B_OFFHOST=REFUSED_DESTINATION; use separate Bliss-only storage, never DR362.'
}
foreach ($name in @('ssh.exe','scp.exe')) {
    if ($null -eq (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw 'TASK3B_OFFHOST=OPENSSH_CLIENT_MISSING'
    }
}
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw 'TASK3B_OFFHOST=MANIFEST_NOT_FOUND'
}
$m = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -or
    $m.sourceServiceId -cne '6aa78407-1632-48c9-8ea4-0289a7d36a5b' -or
    $m.sourceDatabase -cne 'bliss' -or $m.sourcePostgresMajor -ne 18 -or
    $m.encryption -cne 'GPG_RECIPIENT' -or
    $m.archiveFileName -cnotmatch '^bliss-pg18-[a-zA-Z0-9-]+\.gpg$' -or
    $m.archiveSha256 -cnotmatch '^[0-9a-f]{64}$' -or
    $m.offhostSha256Verified -eq $true) {
    throw 'TASK3B_OFFHOST=UNACCEPTABLE_SOURCE_MANIFEST'
}
$archivePath = Join-Path (Split-Path -Parent $ManifestPath) $m.archiveFileName
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    throw 'TASK3B_OFFHOST=ENCRYPTED_ARCHIVE_MISSING'
}
$actual = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -cne $m.archiveSha256) {
    throw 'TASK3B_OFFHOST=ENCRYPTED_ARCHIVE_HASH_MISMATCH'
}
$ssh = (Get-Command ssh.exe).Source
$scp = (Get-Command scp.exe).Source
$target = $SshUser + '@' + $DropletIPv4
$remotePath = $RemoteDirectory.TrimEnd('/') + '/' + $m.archiveFileName
# Shell inputs strictly constrained to non-metacharacter path characters.
$remoteCommand = 'umask 077; mkdir -p "' + $RemoteDirectory + '"; chmod 700 "' + $RemoteDirectory + '"; test -d "' + $RemoteDirectory + '" && test -w "' + $RemoteDirectory + '" && test ! -e "' + $remotePath + '"'
& $ssh -o BatchMode=yes -o StrictHostKeyChecking=yes $target $remoteCommand *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'TASK3B_OFFHOST=SSH_DESTINATION_NOT_READY; check private DO SSH and available storage.'
}
& $scp -o BatchMode=yes -o StrictHostKeyChecking=yes $archivePath ($target + ':' + $remotePath) *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'TASK3B_OFFHOST=ENCRYPTED_TRANSFER_NOT_VERIFIED'
}
$checksumCommand = 'sha256sum "' + $remotePath + '"'
$reply = @(& $ssh -o BatchMode=yes -o StrictHostKeyChecking=yes $target $checksumCommand 2>&1)
$sshExit = $LASTEXITCODE
$checksumText = ($reply -join ' ').Trim()
if ($sshExit -ne 0 -or $checksumText -cnotmatch ('^' + [regex]::Escape($actual) + '\s')) {
    throw 'TASK3B_OFFHOST=REMOTE_CHECKSUM_NOT_VERIFIED'
}
$m.offhostProvider = 'DigitalOcean'
$m.offhostSha256Verified = $true
$m | Add-Member -NotePropertyName offhostVerifiedAtUtc -NotePropertyValue ((Get-Date).ToUniversalTime().ToString('o')) -Force
$m | Add-Member -NotePropertyName offhostDropletId -NotePropertyValue '571399692' -Force
$m | Add-Member -NotePropertyName offhostRemoteDirectory -NotePropertyValue $RemoteDirectory -Force
$m | Add-Member -NotePropertyName offhostRemoteChecksum -NotePropertyValue $actual -Force
$offhostManifest = $ManifestPath -replace '\.pending\.json$', '.offhost.json'
if ($offhostManifest -ceq $ManifestPath -or (Test-Path -LiteralPath $offhostManifest)) {
    throw 'TASK3B_OFFHOST=EVIDENCE_PATH_COLLISION'
}
$m | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $offhostManifest -Encoding UTF8 -NoNewline
Write-Output 'TASK3B_DIGITALOCEAN_ENCRYPTED_COPY=SHA256_VERIFIED'
Write-Output 'TASK3B_ISOLATED_PG18_RESTORE=NOT_VERIFIED'
Write-Output 'TASK3B_PUBLIC_REGISTRATION=HELD'
Write-Output ("TASK3B_OFFHOST_MANIFEST=" + $offhostManifest)
