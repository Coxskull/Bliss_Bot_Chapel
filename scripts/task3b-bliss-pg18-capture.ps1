# ALPHA GLOBAL MEDIA / Task #3B
# Capture existing Railway Bliss PostgreSQL 18 into a GPG-recipient-encrypted
# logical backup on owner's Windows PC. No public TCP proxy, DR362 writes,
# schema migration, or plaintext backup files. Intended only for small,
# pre-registration database: bounded memory, fail-closed on oversized dumps.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string] $GpgRecipientFingerprint,
    [string] $OutputDirectory = (Join-Path $HOME 'Downloads')
)
$ErrorActionPreference = 'Stop'
$project = 'fb33c1b7-03fb-42c8-89b1-081951a738fd'
$environment = 'production'
$sourceServiceId = '6aa78407-1632-48c9-8ea4-0289a7d36a5b'
$expectedEnvironmentId = 'b67a9836-36bf-4083-93f7-00c867b13f39'
$database = 'bliss'
$maxBytes = 64 * 1024 * 1024
$railway = Get-Command railway.cmd -ErrorAction SilentlyContinue
if ($null -eq $railway) { $railway = Get-Command railway -ErrorAction SilentlyContinue }
$gpg = Get-Command gpg.exe -ErrorAction SilentlyContinue
if ($null -eq $gpg) { $gpg = Get-Command gpg -ErrorAction SilentlyContinue }
if ($null -eq $railway -or $null -eq $gpg) {
    throw 'BLISS_PG18_BACKUP=PRECHECK_FAILED; Railway CLI and trusted GPG required. No database changes.'
}
if (-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)) {
    throw 'BLISS_PG18_BACKUP=OUTPUT_DIRECTORY_MISSING; no backup attempted.'
}
# Owner must configure and trust a portable GPG key with protected private key.
& $gpg.Source --batch --no-tty --list-keys $GpgRecipientFingerprint *> $null
if ($LASTEXITCODE -ne 0) { throw 'BLISS_PG18_BACKUP=GPG_RECIPIENT_UNKNOWN; no backup attempted.' }

$remote = @'
set -euo pipefail
if [ "$RAILWAY_PROJECT_ID" != "fb33c1b7-03fb-42c8-89b1-081951a738fd" ] ||
   [ "$RAILWAY_ENVIRONMENT_ID" != "b67a9836-36bf-4083-93f7-00c867b13f39" ] ||
   [ "$RAILWAY_SERVICE_ID" != "6aa78407-1632-48c9-8ea4-0289a7d36a5b" ]; then
  exit 2
fi
for tool in pg_dump psql base64; do
  command -v "$tool" >/dev/null || exit 2
done
: "$PGUSER"
: "$PGPASSWORD"
: "$PGHOST"
: "$PGPORT"
db='bliss'
identity=$(psql -XAt -v ON_ERROR_STOP=1 --dbname="$db" -c "SELECT current_database() || '|' || split_part(current_setting('server_version'),'.',1) || '|' || (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public')")
if ! printf '%s' "$identity" | grep -Eq '^bliss\|18\|[0-9]+$'; then
  exit 2
fi
printf 'TASK3B_SOURCE=%s\n' "$identity"
printf 'TASK3B_DUMP_BEGIN\n'
pg_dump --dbname="$db" --format=custom --no-owner --no-acl | base64 -w0
printf '\nTASK3B_DUMP_END\n'
'@

Write-Output 'TASK3B_BACKUP_SOURCE=EXISTING_RAILWAY_BLISS_PG18'
Write-Output 'TASK3B_BACKUP_MODE=READ_ONLY_ENCRYPTED_IN_MEMORY'
Write-Output 'TASK3B_DB_PUBLIC_PROXY=UNCHANGED_NONE'
# Railway CLI 2026-11-21 behavior on owner's Windows PowerShell 5.1:
# direct "railway.cmd ssh -- /bin/sh -c 'echo ...'" succeeds, but piped
# "script | railway.cmd ssh -- bash -se" is NOT forwarded correctly (127).
# Encode ONLY the reviewed script source (no credentials or database records).
# Passing one ASCII shell -c argument avoids PowerShell 5.1 re-quoting the
# many embedded shell quotes/dollar signs of the script.
# Unlike the broken CLIENT stdin pipe, this decodes locally INSIDE the remote
# shell and pipes into a remote bash, with pipefail retained at both stages.
$remoteBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($remote))
$runner = 'set -o pipefail; printf %s ' + $remoteBase64 + ' | base64 -d | bash'
$raw = @()
$remoteExit = 1
try {
    $raw = @(& $railway.Source ssh -p $project -s 'Postgres' -e $environment -- bash -c $runner 2>&1)
    $remoteExit = $LASTEXITCODE
} catch {
    $raw = @()
    $remoteExit = 1
}
if ($remoteExit -ne 0) {
    throw 'BLISS_PG18_BACKUP=REMOTE_COMMAND_FAILED; Railway SSH key may work but remote bash -c, database identity or pg_dump failed. No backup accepted.'
}
# The CLI can print exactly these human-facing SSH status messages around
# remote stdout on Windows. Remove only these known fixed-format lines.
# Never display raw output, raw errors, or the encoded plaintext archive.
$lines = @($raw | ForEach-Object { [string]$_ })
$dataLines = @($lines | Where-Object {
    $_ -cnotmatch '^Using SSH key from file .+
if (-not $m.Success) {
    throw 'BLISS_PG18_BACKUP=INVALID_REMOTE_RECEIPT; no archive accepted.'
}
$publicTables = [int]$m.Groups[1].Value
$b64 = $m.Groups[2].Value
if ($b64.Length -gt [int64]($maxBytes / 3 * 4 + 10)) {
    throw 'BLISS_PG18_BACKUP=SIZE_CAP_EXCEEDED; do not use helper for large databases.'
}
try { [byte[]]$plain = [Convert]::FromBase64String($b64) }
catch { throw 'BLISS_PG18_BACKUP=INVALID_DUMP_ENCODING; no backup accepted.' }
if ($plain.Length -lt 100 -or $plain.Length -gt $maxBytes) {
    throw 'BLISS_PG18_BACKUP=INVALID_DUMP_SIZE; no backup accepted.'
}
if ([Text.Encoding]::ASCII.GetString($plain, 0, 5) -cne 'PGDMP') {
    throw 'BLISS_PG18_BACKUP=NOT_A_POSTGRES_CUSTOM_DUMP; no backup accepted.'
}

$id = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$archivePath = Join-Path $OutputDirectory ("bliss-pg18-" + $id + ".gpg")
$manifestPath = Join-Path $OutputDirectory ("bliss-pg18-" + $id + ".pending.json")
$gpgProcess = $null
try {
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $gpg.Source
    $psi.Arguments = '--batch --no-tty --encrypt --recipient ' + $GpgRecipientFingerprint + ' --output "' + $archivePath + '"'
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $gpgProcess = New-Object Diagnostics.Process
    $gpgProcess.StartInfo = $psi
    if (-not $gpgProcess.Start()) { throw 'GPG startup refused' }
    $gpgProcess.StandardInput.BaseStream.Write($plain, 0, $plain.Length)
    $gpgProcess.StandardInput.Close()
    $gpgProcess.WaitForExit()
    if ($gpgProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $archivePath -PathType Leaf) -or
        (Get-Item -LiteralPath $archivePath).Length -lt 100) {
        throw 'GPG encryption refused'
    }
    $archiveSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest = [ordered]@{
        schema = 'alpha-bliss-pg18-recovery/v1'
        sourceProjectId = $project
        sourceEnvironmentId = $expectedEnvironmentId
        sourceServiceId = $sourceServiceId
        sourceDatabase = $database
        sourcePostgresMajor = 18
        sourcePublicTableCount = $publicTables
        capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        archiveFileName = [IO.Path]::GetFileName($archivePath)
        archiveSha256 = $archiveSha256
        encryption = 'GPG_RECIPIENT'
        gpgRecipientFingerprint = $GpgRecipientFingerprint.ToUpperInvariant()
        offhostProvider = 'NOT_VERIFIED'
        offhostSha256Verified = $false
        isolatedRestoreVerified = $false
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8 -NoNewline
} catch {
    Remove-Item -LiteralPath $archivePath, $manifestPath -Force -ErrorAction SilentlyContinue
    throw 'BLISS_PG18_BACKUP=ENCRYPTION_OR_MANIFEST_FAILED; no trusted archive retained.'
} finally {
    if ($null -ne $plain) { [Array]::Clear($plain, 0, $plain.Length) }
    $b64 = $null
    $raw = $null
    if ($null -ne $gpgProcess) { $gpgProcess.Dispose() }
}
Write-Output 'TASK3B_ENCRYPTED_SOURCE_BACKUP=CAPTURED'
Write-Output 'TASK3B_OFFHOST_DIGITALOCEAN_COPY=NOT_VERIFIED'
Write-Output 'TASK3B_ISOLATED_PG18_RESTORE=NOT_VERIFIED'
Write-Output 'TASK3B_REGISTRATION_RELEASE=HELD'
Write-Output ("TASK3B_ARCHIVE=" + $archivePath)
Write-Output ("TASK3B_MANIFEST=" + $manifestPath)
 -and
    $_ -cne 'Connection to ssh.railway.com closed.'
})
$joined = ($dataLines -join [Environment]::NewLine).Trim()
$m = [regex]::Match($joined, '\ATASK3B_SOURCE=bliss\|18\|([0-9]+)\r?\nTASK3B_DUMP_BEGIN\r?\n([A-Za-z0-9+/=]+)\r?\nTASK3B_DUMP_END\z')
if (-not $m.Success) {
    throw 'BLISS_PG18_BACKUP=INVALID_REMOTE_RECEIPT; no archive accepted.'
}
$publicTables = [int]$m.Groups[1].Value
$b64 = $m.Groups[2].Value
if ($b64.Length -gt [int64]($maxBytes / 3 * 4 + 10)) {
    throw 'BLISS_PG18_BACKUP=SIZE_CAP_EXCEEDED; do not use helper for large databases.'
}
try { [byte[]]$plain = [Convert]::FromBase64String($b64) }
catch { throw 'BLISS_PG18_BACKUP=INVALID_DUMP_ENCODING; no backup accepted.' }
if ($plain.Length -lt 100 -or $plain.Length -gt $maxBytes) {
    throw 'BLISS_PG18_BACKUP=INVALID_DUMP_SIZE; no backup accepted.'
}
if ([Text.Encoding]::ASCII.GetString($plain, 0, 5) -cne 'PGDMP') {
    throw 'BLISS_PG18_BACKUP=NOT_A_POSTGRES_CUSTOM_DUMP; no backup accepted.'
}

$id = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$archivePath = Join-Path $OutputDirectory ("bliss-pg18-" + $id + ".gpg")
$manifestPath = Join-Path $OutputDirectory ("bliss-pg18-" + $id + ".pending.json")
$gpgProcess = $null
try {
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $gpg.Source
    $psi.Arguments = '--batch --no-tty --encrypt --recipient ' + $GpgRecipientFingerprint + ' --output "' + $archivePath + '"'
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $gpgProcess = New-Object Diagnostics.Process
    $gpgProcess.StartInfo = $psi
    if (-not $gpgProcess.Start()) { throw 'GPG startup refused' }
    $gpgProcess.StandardInput.BaseStream.Write($plain, 0, $plain.Length)
    $gpgProcess.StandardInput.Close()
    $gpgProcess.WaitForExit()
    if ($gpgProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $archivePath -PathType Leaf) -or
        (Get-Item -LiteralPath $archivePath).Length -lt 100) {
        throw 'GPG encryption refused'
    }
    $archiveSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest = [ordered]@{
        schema = 'alpha-bliss-pg18-recovery/v1'
        sourceProjectId = $project
        sourceEnvironmentId = $expectedEnvironmentId
        sourceServiceId = $sourceServiceId
        sourceDatabase = $database
        sourcePostgresMajor = 18
        sourcePublicTableCount = $publicTables
        capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        archiveFileName = [IO.Path]::GetFileName($archivePath)
        archiveSha256 = $archiveSha256
        encryption = 'GPG_RECIPIENT'
        gpgRecipientFingerprint = $GpgRecipientFingerprint.ToUpperInvariant()
        offhostProvider = 'NOT_VERIFIED'
        offhostSha256Verified = $false
        isolatedRestoreVerified = $false
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8 -NoNewline
} catch {
    Remove-Item -LiteralPath $archivePath, $manifestPath -Force -ErrorAction SilentlyContinue
    throw 'BLISS_PG18_BACKUP=ENCRYPTION_OR_MANIFEST_FAILED; no trusted archive retained.'
} finally {
    if ($null -ne $plain) { [Array]::Clear($plain, 0, $plain.Length) }
    $b64 = $null
    $raw = $null
    if ($null -ne $gpgProcess) { $gpgProcess.Dispose() }
}
Write-Output 'TASK3B_ENCRYPTED_SOURCE_BACKUP=CAPTURED'
Write-Output 'TASK3B_OFFHOST_DIGITALOCEAN_COPY=NOT_VERIFIED'
Write-Output 'TASK3B_ISOLATED_PG18_RESTORE=NOT_VERIFIED'
Write-Output 'TASK3B_REGISTRATION_RELEASE=HELD'
Write-Output ("TASK3B_ARCHIVE=" + $archivePath)
Write-Output ("TASK3B_MANIFEST=" + $manifestPath)
