# ALPHA Task3B — classification-only Railway PG18 backup preflight.
# Windows PowerShell 5.1, existing authenticated CLI, no database writes.
# Reads database/catalog identity only; no pg_dump of customer rows,
# credentials, raw SSH output, encryption or Railway configuration changes.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$railway = Get-Command railway.cmd -ErrorAction SilentlyContinue
if ($null -eq $railway) { throw 'TASK3B_REMOTE_PREFLIGHT=LOCAL_RAILWAY_CLI_MISSING' }
$project = 'fb33c1b7-03fb-42c8-89b1-081951a738fd'
$remote = @'
set -euo pipefail
# Railway SSH can drop stdout but still propagate the remote exit code.
# One distinct non-secret exit code per stage survives that transport issue.
refuse() {
  case "$1" in
    SCOPE_MISMATCH) stage_exit=31 ;;
    DATABASE_TOOL_MISSING) stage_exit=32 ;;
    DATABASE_ENV_MISSING) stage_exit=33 ;;
    DATABASE_CONNECTION_OR_QUERY_REFUSED) stage_exit=34 ;;
    DATABASE_IDENTITY_MISMATCH) stage_exit=35 ;;
    PG_DUMP_SCHEMA_PREFLIGHT_REFUSED) stage_exit=36 ;;
    *) stage_exit=2 ;;
  esac
  printf 'TASK3B_REMOTE_PREFLIGHT=%s\n' "$1"
  exit "$stage_exit"
}
if [ "$(printenv RAILWAY_PROJECT_ID || true)" != 'fb33c1b7-03fb-42c8-89b1-081951a738fd' ] ||
   [ "$(printenv RAILWAY_ENVIRONMENT_ID || true)" != 'b67a9836-36bf-4083-93f7-00c867b13f39' ] ||
   [ "$(printenv RAILWAY_SERVICE_ID || true)" != '6aa78407-1632-48c9-8ea4-0289a7d36a5b' ]; then
  refuse 'SCOPE_MISMATCH'
fi
for tool in pg_dump psql base64; do
  command -v "$tool" >/dev/null 2>&1 || refuse 'DATABASE_TOOL_MISSING'
done
for name in PGHOST PGPORT PGUSER PGPASSWORD; do
  [ -n "$(printenv "$name" || true)" ] || refuse 'DATABASE_ENV_MISSING'
done
# Read ONLY current_database(), server major and aggregate table count.
# Suppress actual psql messages; no customer details appear in stdout.
identity="$(psql -XAt -v ON_ERROR_STOP=1 --dbname=bliss -c \
 "SELECT current_database() || '|' || split_part(current_setting('server_version'),'.',1) || '|' || (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public')" 2>/dev/null)" ||
  refuse 'DATABASE_CONNECTION_OR_QUERY_REFUSED'
printf '%s' "$identity" | grep -Eq '^bliss\|18\|[0-9]+$' ||
  refuse 'DATABASE_IDENTITY_MISMATCH'
# Metadata-only dump verifies pg_dump access without reading customer rows.
pg_dump --dbname=bliss --schema-only --no-owner --no-acl --file=/dev/null >/dev/null 2>&1 ||
  refuse 'PG_DUMP_SCHEMA_PREFLIGHT_REFUSED'
printf '%s\n' 'TASK3B_REMOTE_PREFLIGHT=PASS_SCOPE_DATABASE_AND_DUMP_TOOLS'
'@
# Source code only; no credentials or database rows encoded.
# Windows Git checkouts may convert this PowerShell here-string to CRLF.
# Linux Bash interprets the trailing carriage return as part of 'pipefail',
# producing exit 2 before any controlled failure marker is emitted.
# Normalize ONLY the reviewed remote source, not GPG or dump bytes.
$remote = $remote.Replace("`r`n", "`n").Replace("`r", "")
$source64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($remote))
$runner = 'set -o pipefail; printf %s ' + $source64 + ' | base64 -d | bash'
$raw = @()
$exitCode = 1
$oldPreference = $ErrorActionPreference
try {
    # Railway SSH writes routine key banner to native stderr on Windows.
    $ErrorActionPreference = 'Continue'
    $raw = @(& $railway.Source ssh -p $project -s 'Postgres' -e production -- bash -c $runner 2>&1)
    $exitCode = $LASTEXITCODE
} catch {
    $exitCode = 1
} finally {
    $ErrorActionPreference = $oldPreference
}
# Prefer distinct remote exit codes for failure classification. The owner's
# latest real SSH session reached Postgres but its receipt stdout was absent;
# numerical remote status is still reported by Railway. Do not display raw
# SSH stdout/stderr, bash scripts, PG environment or database records.
$exitStage = @{
    31 = 'SCOPE_MISMATCH'
    32 = 'DATABASE_TOOL_MISSING'
    33 = 'DATABASE_ENV_MISSING'
    34 = 'DATABASE_CONNECTION_OR_QUERY_REFUSED'
    35 = 'DATABASE_IDENTITY_MISMATCH'
    36 = 'PG_DUMP_SCHEMA_PREFLIGHT_REFUSED'
}
if ($exitStage.ContainsKey([int]$exitCode)) {
    Write-Output ('TASK3B_REMOTE_PREFLIGHT=' + $exitStage[[int]$exitCode])
    exit 2
}
# Fixed, privacy-safe vocabulary only. NEVER print raw remote output.
$allowed = @(
    'SCOPE_MISMATCH', 'DATABASE_TOOL_MISSING',
    'DATABASE_ENV_MISSING', 'DATABASE_CONNECTION_OR_QUERY_REFUSED',
    'DATABASE_IDENTITY_MISMATCH', 'PG_DUMP_SCHEMA_PREFLIGHT_REFUSED',
    'PASS_SCOPE_DATABASE_AND_DUMP_TOOLS'
)
$markers = @($raw | ForEach-Object { [string]$_ } |
    Where-Object { $_.Trim() -cmatch '^TASK3B_REMOTE_PREFLIGHT=[A-Z_]+$' })
if ($markers.Count -ne 1) {
    # Code 2 with an absent marker is an unclassified shell/CLI refusal,
    # not evidence that Railway or the PostgreSQL connection is unavailable.
    Write-Output 'TASK3B_REMOTE_PREFLIGHT=REMOTE_SHELL_OR_OUTPUT_UNVERIFIED'
    exit 2
}
$stage = $markers[0].Trim().Substring('TASK3B_REMOTE_PREFLIGHT='.Length)
if ($allowed -cnotcontains $stage) {
    Write-Output 'TASK3B_REMOTE_PREFLIGHT=TRANSPORT_OR_OUTPUT_UNVERIFIED'
    exit 2
}
if ($exitCode -eq 0 -and $stage -ceq 'PASS_SCOPE_DATABASE_AND_DUMP_TOOLS') {
    Write-Output ('TASK3B_REMOTE_PREFLIGHT=' + $stage)
    exit 0
}
if ($exitCode -ne 0 -and $stage -cne 'PASS_SCOPE_DATABASE_AND_DUMP_TOOLS') {
    Write-Output ('TASK3B_REMOTE_PREFLIGHT=' + $stage)
    exit 2
}
Write-Output 'TASK3B_REMOTE_PREFLIGHT=EXIT_STATUS_MISMATCH'
exit 2
