# Task #3B: transfer ONLY the already GPG-encrypted Bliss PG18 dump to
# the existing owner-controlled DigitalOcean droplet. Never upload plaintext.
# Requires OpenSSH client, the established SSH host key/owner identity, and
# explicit separate storage directory (not the DR362 database directory).
[CmdletBinding()]
param(
    [string] $ManifestPath = '',
    [Parameter(Mandatory = $true)][ValidatePattern('^([0-9]{1,3}\.){3}[0-9]{1,3}$')][string] $DropletIPv4,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z_][a-z0-9_-]*$')][string] $SshUser,
    [Parameter(Mandatory = $true)][string] $RemoteDirectory,
    [switch] $PreflightOnly
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
if ($PreflightOnly) {
    if (-not [string]::IsNullOrWhiteSpace($ManifestPath)) {
        throw 'TASK3B_OFFHOST=PREFLIGHT_ONLY_MANIFEST_REFUSED'
    }
    # Read-only: require an already existing private Bliss destination;
    # never create directories, change mounts or transfer data in this mode.
    $target = $SshUser + '@' + $DropletIPv4
    $probe = @'
set -eu
dir="__REMOTE_DIRECTORY__"
[ -d "$dir" ] && [ ! -L "$dir" ] || exit 2
[ "$(readlink -f "$dir")" = "$dir" ] || exit 2
[ -w "$dir" ] && [ "$(stat -c %a "$dir")" = 700 ] || exit 2
mount_target=$(findmnt -T "$dir" -n -o TARGET) || exit 2
mount_source=$(findmnt -T "$dir" -n -o SOURCE) || exit 2
mount_type=$(findmnt -T "$dir" -n -o FSTYPE) || exit 2
mount_device=$(findmnt -T "$dir" -n -o MAJ:MIN) || exit 2
root_device=$(findmnt -T / -n -o MAJ:MIN) || exit 2
[ -n "$mount_device" ] && [ "$mount_device" != "$root_device" ] || exit 2
[ -n "$mount_target" ] && [ "$mount_target" != / ] || exit 2
case "$mount_target $mount_source" in *[Dd][Rr]362*|*[Ss][Uu][Pp][Aa][Bb][Aa][Ss][Ee]*|*[Ff][Aa][Cc][Tt][Oo][Rr][Yy]*) exit 2 ;; esac
case "$mount_source" in /dev/*) ;; *) exit 2 ;; esac
case "$mount_type" in ext4|xfs|btrfs) ;; *) exit 2 ;; esac
printf 'TASK3B_OFFHOST_READONLY_PREFLIGHT_READY\n'
'@
    $probe = $probe.Replace('__REMOTE_DIRECTORY__', $RemoteDirectory)
    $reply = @()
    $code = 1
    try {
        $reply = @(& ssh.exe -o BatchMode=yes -o StrictHostKeyChecking=yes -o ConnectTimeout=10 $target $probe 2>&1)
        $code = $LASTEXITCODE
    } catch { $code = 1 }
    if ($code -ne 0 -or @($reply | Where-Object { ([string]$_).Trim() -ceq 'TASK3B_OFFHOST_READONLY_PREFLIGHT_READY' }).Count -ne 1) {
        throw 'TASK3B_OFFHOST=READONLY_DESTINATION_PREFLIGHT_REFUSED'
    }
    Write-Output 'TASK3B_OFFHOST_READONLY_PREFLIGHT_READY'
    return
}
if ([string]::IsNullOrWhiteSpace($ManifestPath) -or -not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
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
# Block Storage is already attached to this Droplet, but the previous script
# accepted any writable path (even the root filesystem). Require an actual
# non-root, disk-backed mountpoint and enough storage BEFORE remote writes.
$archiveBytes = (Get-Item -LiteralPath $archivePath).Length
$requiredKiB = [int64][Math]::Ceiling($archiveBytes / 1024.0) + 102400
$remoteCommand = @'
set -eu
umask 077
dir="__REMOTE_DIRECTORY__"
archive="__REMOTE_ARCHIVE__"
needed_kib=__REQUIRED_KIB__
existing="$dir"
while [ ! -e "$existing" ]; do
  [ "$existing" != "/" ] || exit 2
  existing=$(dirname "$existing")
done
[ -d "$existing" ] || exit 2
[ "$(readlink -m "$dir")" = "$dir" ] || exit 2
mount_target=$(findmnt -T "$existing" -n -o TARGET) || exit 2
mount_source=$(findmnt -T "$existing" -n -o SOURCE) || exit 2
mount_type=$(findmnt -T "$existing" -n -o FSTYPE) || exit 2
mount_device=$(findmnt -T "$existing" -n -o MAJ:MIN) || exit 2
root_device=$(findmnt -T / -n -o MAJ:MIN) || exit 2
[ -n "$mount_device" ] && [ -n "$root_device" ] && [ "$mount_device" != "$root_device" ] || exit 2
[ -n "$mount_target" ] && [ "$mount_target" != "/" ] || exit 2
case "$mount_source" in /dev/*) ;; *) exit 2 ;; esac
case "$mount_type" in ext4|xfs|btrfs) ;; *) exit 2 ;; esac
free_kib=$(df -Pk "$existing" | awk 'NR==2 { print $4 }')
case "$free_kib" in ''|*[!0-9]*) exit 2 ;; esac
[ "$free_kib" -ge "$needed_kib" ] || exit 2
test ! -e "$archive" && test ! -L "$archive" || exit 2
mkdir -p "$dir"
[ -d "$dir" ] && [ ! -L "$dir" ] || exit 2
[ "$(readlink -f "$dir")" = "$dir" ] || exit 2
[ "$(findmnt -T "$dir" -n -o TARGET)" = "$mount_target" ] || exit 2
chmod 700 "$dir"
[ -w "$dir" ] || exit 2
test ! -e "$archive" && test ! -L "$archive" || exit 2
printf 'TASK3B_MOUNTED_VOLUME_READY\n'
'@
$remoteCommand = $remoteCommand.Replace('__REMOTE_DIRECTORY__', $RemoteDirectory)
$remoteCommand = $remoteCommand.Replace('__REMOTE_ARCHIVE__', $remotePath)
$remoteCommand = $remoteCommand.Replace('__REQUIRED_KIB__', [string]$requiredKiB)
$preflight = @()
$preflightExit = 1
try {
    $preflight = @(& $ssh -o BatchMode=yes -o StrictHostKeyChecking=yes $target $remoteCommand 2>&1)
    $preflightExit = $LASTEXITCODE
} catch {
    $preflight = @()
    $preflightExit = 1
}
$mountReady = @($preflight | Where-Object { ([string]$_).Trim() -ceq 'TASK3B_MOUNTED_VOLUME_READY' }).Count
if ($preflightExit -ne 0 -or $mountReady -ne 1) {
    throw 'TASK3B_OFFHOST=NONROOT_ATTACHED_VOLUME_OR_SPACE_NOT_VERIFIED; no encrypted copy attempted.'
}

& $scp -o BatchMode=yes -o StrictHostKeyChecking=yes $archivePath ($target + ':' + $remotePath) *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'TASK3B_OFFHOST=ENCRYPTED_TRANSFER_NOT_VERIFIED'
}
$checksumCommand = 'test -f "' + $remotePath + '" && test ! -L "' + $remotePath + '" && test "$(findmnt -T "' + $remotePath + '" -n -o TARGET)" != / && sha256sum "' + $remotePath + '"'
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
