# ALPHA GLOBAL MEDIA / Task #3B — OWNER WORKSTATION HANDOFF
# Runs the EXISTING source read-only Bliss PostgreSQL 18 -> GPG backup script.
# Does not create/modify production DB, Railway config, DR362, Docker or DO.
# PowerShell 5.1: launch with:
# powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\task3b-owner-start-backup.ps1
# -ExecutionPolicy Bypass only applies to this newly started process, not
# the user's machine-wide or CurrentUser security policy.
[CmdletBinding()]
param([string] $OutputDirectory = (Join-Path $HOME 'Downloads'))
$ErrorActionPreference = 'Stop'
$backupScript = Join-Path $PSScriptRoot 'task3b-bliss-pg18-capture.ps1'
if (-not (Test-Path -LiteralPath $backupScript -PathType Leaf)) {
    throw 'TASK3B_OWNER_BACKUP=TRUSTED_CAPTURE_SCRIPT_MISSING; no database operation.'
}
if (-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)) {
    throw 'TASK3B_OWNER_BACKUP=PRIVATE_OUTPUT_DIRECTORY_MISSING; no database operation.'
}

# Explicitly select the Windows npm .cmd launcher. The railway.ps1 npm shim
# is blocked by the owner's observed PowerShell execution-policy settings.
$railway = Get-Command railway.cmd -ErrorAction SilentlyContinue
if ($null -eq $railway) {
    throw 'TASK3B_OWNER_BACKUP=RAILWAY_CMD_NOT_FOUND; install/repair the existing CLI, no DB operation.'
}
$whoamiExit = 1
try {
    # Suppress all account output; do not show usernames, tokens or diagnostics.
    & $railway.Source whoami *> $null
    $whoamiExit = $LASTEXITCODE
} catch {
    $whoamiExit = 1
}
if ($whoamiExit -ne 0) {
    throw 'TASK3B_OWNER_BACKUP=RAILWAY_OWNER_LOGIN_REQUIRED; use railway.cmd login locally, then retry.'
}

$gpg = Get-Command gpg.exe -ErrorAction SilentlyContinue
if ($null -eq $gpg) { $gpg = Get-Command gpg -ErrorAction SilentlyContinue }
if ($null -eq $gpg) {
    throw 'TASK3B_OWNER_BACKUP=GPG_NOT_FOUND; no database operation.'
}
# The encryption public key must exist locally; its matching private key may
# (and preferably should) be stored separately in offline owner custody.
# Print ONLY public fingerprints, never key file contents or full user IDs.
$keyList = @(& $gpg.Source --batch --no-tty --with-colons --list-keys 2>$null)
if ($LASTEXITCODE -ne 0) {
    throw 'TASK3B_OWNER_BACKUP=GPG_PUBLIC_RECIPIENT_UNAVAILABLE; no database operation.'
}
$found = New-Object 'System.Collections.Generic.List[string]'
$nextFingerprintIsPrimary = $false
foreach ($item in $keyList) {
    $line = [string] $item
    if ($line -cmatch '^pub:') { $nextFingerprintIsPrimary = $true; continue }
    if ($line -cmatch '^(sub|sec):') { $nextFingerprintIsPrimary = $false; continue }
    if ($nextFingerprintIsPrimary -and $line -cmatch '^fpr:') {
        $pieces = $line.Split(':')
        if ($pieces.Length -ge 10 -and $pieces[9] -cmatch '^[A-Fa-f0-9]{40}$') {
            $fp = $pieces[9].ToUpperInvariant()
            if (-not $found.Contains($fp)) { $found.Add($fp) }
        }
        $nextFingerprintIsPrimary = $false
    }
}
if ($found.Count -lt 1) {
    throw 'TASK3B_OWNER_BACKUP=GPG_PUBLIC_KEY_NOT_FOUND; no backup attempted.'
}
Write-Output 'TASK3B_RAILWAY_OWNER_AUTH=AVAILABLE'
Write-Output 'TASK3B_GPG_OWNER_PUBLIC_KEYS=DETECTED_LOCAL'
Write-Output 'TASK3B_FINGERPRINTS_ARE_PUBLIC_IDENTIFIERS_ONLY'
foreach ($fingerprint in $found) { Write-Output ('GPG_PUBLIC_FINGERPRINT=' + $fingerprint) }

$selected = (Read-Host 'Enter ONE listed public fingerprint for an existing RECOVERABLE GPG key (40 hex characters)').Trim().ToUpperInvariant()
if ($selected -cnotmatch '^[A-F0-9]{40}$' -or -not $found.Contains($selected)) {
    throw 'TASK3B_OWNER_BACKUP=RECIPIENT_NOT_A_LISTED_PUBLIC_GPG_KEY; no backup attempted.'
}
# Do not require or import the decryption private key into this workstation.
# Owner affirms that a recoverable offline copy exists, without disclosing it.
$recoveryCustody = (Read-Host 'Confirm the matching PRIVATE decryption key is recoverable in secure owner custody, separately from the backup (type RECOVERY-KEY-HELD)').Trim()
if ($recoveryCustody -cne 'RECOVERY-KEY-HELD') {
    throw 'TASK3B_OWNER_BACKUP=PRIVATE_DECRYPTION_KEY_CUSTODY_NOT_ATTESTED; no backup attempted.'
}
Write-Output 'TASK3B_GPG_RECOVERY_PRIVATE_KEY_CUSTODY=OWNER_ATTESTED_OFFLINE'
Write-Output 'TASK3B_BACKUP_CAPTURE=STARTING_READ_ONLY_EXISTING_RAILWAY_PG18'
# Execution cannot proceed without a present encryption public recipient.
# No passwords or private keys are copied into process arguments or logs.
& $backupScript -GpgRecipientFingerprint $selected -OutputDirectory $OutputDirectory
if (-not $?) {
    throw 'TASK3B_OWNER_BACKUP=CAPTURE_SCRIPT_NOT_VERIFIED; do not claim backup completed.'
}
