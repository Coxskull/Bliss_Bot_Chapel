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
# Fixed-stage refusal markers; no raw SQL, SSH, credential or dump output.
refuse() { printf 'TASK3B_CAPTURE_ABORT=%s\n' "$1"; exit 2; }
if [ "$(printenv RAILWAY_PROJECT_ID || true)" != "fb33c1b7-03fb-42c8-89b1-081951a738fd" ] ||
   [ "$(printenv RAILWAY_ENVIRONMENT_ID || true)" != "b67a9836-36bf-4083-93f7-00c867b13f39" ] ||
   [ "$(printenv RAILWAY_SERVICE_ID || true)" != "6aa78407-1632-48c9-8ea4-0289a7d36a5b" ]; then
  refuse 'SCOPE_MISMATCH'
fi
for tool in pg_dump psql base64; do
  command -v "$tool" >/dev/null 2>&1 || refuse 'DATABASE_TOOL_MISSING'
done
for name in PGUSER PGPASSWORD PGHOST PGPORT; do
  [ -n "$(printenv "$name" || true)" ] || refuse 'DATABASE_ENV_MISSING'
done
db='bliss'
identity=$(psql -XAt -v ON_ERROR_STOP=1 --dbname="$db" -c "SELECT current_database() || '|' || split_part(current_setting('server_version'),'.',1) || '|' || (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public')" 2>/dev/null) ||
  refuse 'DATABASE_CONNECTION_OR_QUERY_REFUSED'
if ! printf '%s' "$identity" | grep -Eq '^bliss\|18\|[0-9]+$'; then
  refuse 'DATABASE_IDENTITY_MISMATCH'
fi
printf 'TASK3B_SOURCE=%s\n' "$identity"
printf 'TASK3B_DUMP_BEGIN\n'
if ! pg_dump --dbname="$db" --format=custom --no-owner --no-acl 2>/dev/null | base64 -w0; then
  printf '\n'
  refuse 'PG_DUMP_EXPORT_REFUSED'
fi
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
# Windows PowerShell 5.1 treats native stderr redirected with 2>&1 as
# NativeCommandError. Under the script-wide 'Stop' preference, Railway's
# harmless "Using SSH key ..." notice can TERMINATE this block before the
# remote command exits, even when SSH authentication and pg_dump work.
# Capture both streams privately with Continue ONLY during native execution.
# A nonzero exit code and any unrecognized output still fail closed below.
$priorErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $raw = @(& $railway.Source ssh -p $project -s 'Postgres' -e $environment -- bash -c $runner 2>&1)
    $remoteExit = $LASTEXITCODE
} catch {
    $raw = @()
    $remoteExit = 1
} finally {
    $ErrorActionPreference = $priorErrorActionPreference
}
if ($remoteExit -ne 0) {
    # Parse ONLY the fixed allowlisted refusal; never print raw encoded data.
    $allowed = @('SCOPE_MISMATCH','DATABASE_TOOL_MISSING',
        'DATABASE_ENV_MISSING','DATABASE_CONNECTION_OR_QUERY_REFUSED',
        'DATABASE_IDENTITY_MISMATCH','PG_DUMP_EXPORT_REFUSED')
    $markers = @($raw | ForEach-Object { [string]$_ } |
        ForEach-Object {
            [regex]::Matches($_, 'TASK3B_CAPTURE_ABORT=([A-Z_]+)') |
                ForEach-Object { $_.Groups[1].Value }
        } | Where-Object { $allowed -ccontains $_ } | Select-Object -Unique)
    if ($markers.Count -eq 1) {
        Write-Output ('TASK3B_CAPTURE_REMOTE_STAGE=' + $markers[0])
    } else {
        Write-Output 'TASK3B_CAPTURE_REMOTE_STAGE=TRANSPORT_OR_UNCLASSIFIED'
    }
    throw 'BLISS_PG18_BACKUP=REMOTE_COMMAND_FAILED; no new backup accepted.'
}
# The CLI can print exactly these human-facing SSH status messages around
# remote stdout on Windows. Remove only these known fixed-format lines.
# Never display raw output, raw errors, or the encoded plaintext archive.
$lines = @($raw | ForEach-Object { [string]$_ })
$dataLines = @($lines | Where-Object {
    -not (($_ -match '^Using SSH key from file ') -or ($_ -eq 'Connection to ssh.railway.com closed.'))
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
