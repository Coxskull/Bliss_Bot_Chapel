# ALPHA Task #4 creator PG18 recovery: local, read-only GPG key-presence preflight.
# Does NOT decrypt, import/export keys, contact a provider, open a database,
# touch the archive/manifest, or prove the private key can unlock.
[CmdletBinding()]
param(
  [string]$ManifestPath = (Join-Path (Join-Path $HOME 'Downloads') 'bliss-pg18-20261122T055153Z-b34126b0.pending.json')
)
$ErrorActionPreference='Stop'
function Refuse([string]$status) {
  Write-Output ('TASK3B_GPG_RECOVERY_PREFLIGHT=' + $status)
  exit 1
}
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf) -or
    (Split-Path -Leaf $ManifestPath) -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.pending\.json$') {
  Refuse 'MANIFEST_NOT_FOUND_OR_INVALID'
}
try { $m=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json }
catch { Refuse 'MANIFEST_NOT_FOUND_OR_INVALID' }
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -or
    $m.sourceServiceId -cne '6aa78407-1632-48c9-8ea4-0289a7d36a5b' -or
    [int]$m.sourcePostgresMajor -ne 18 -or
    $m.archiveFileName -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.gpg$' -or
    $m.archiveFileName -cne ((Split-Path -Leaf $ManifestPath) -replace '\.pending\.json$', '.gpg') -or
    $m.archiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or
    $m.gpgRecipientFingerprint -cnotmatch '^[A-F0-9]{40}$') {
  Refuse 'MANIFEST_NOT_FOUND_OR_INVALID'
}
$archive=Join-Path (Split-Path -Parent $ManifestPath) ([string]$m.archiveFileName)
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
  Refuse 'ARCHIVE_MISSING'
}
try { $hash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() }
catch { Refuse 'CIPHERTEXT_CHECK_FAILED' }
if ($hash -cne $m.archiveSha256) { Refuse 'CIPHERTEXT_CHECK_FAILED' }
$gpg=Get-Command gpg.exe -ErrorAction SilentlyContinue
if ($null -eq $gpg) { Refuse 'WINDOWS_GPG_NOT_FOUND' }
# Restrict listing to the exact manifest-specified PUBLIC recipient identifier.
# Capture secret-key listing only in process memory; do not print its raw output.
$fingerprint=[string]$m.gpgRecipientFingerprint
try {
  $lines=@(& $gpg.Source --batch --no-tty --with-colons --fingerprint --list-secret-keys $fingerprint 2>$null)
  $exitCode=$LASTEXITCODE
} catch { Refuse 'GPG_KEYRING_READ_REFUSED' }
if ($exitCode -ne 0) { Refuse 'GPG_KEYRING_READ_REFUSED' }
$expectPrimary=$false
$matched=$false
foreach ($item in $lines) {
  $line=[string]$item
  if ($line -cmatch '^sec[^:]*:') { $expectPrimary=$true; continue }
  if ($line -cmatch '^(ssb|sub)[^:]*:') { $expectPrimary=$false; continue }
  if ($expectPrimary -and $line -cmatch '^fpr:') {
    $fields=$line.Split(':')
    if ($fields.Length -ge 10 -and $fields[9].ToUpperInvariant() -ceq $fingerprint) {
      $matched=$true
    }
    $expectPrimary=$false
  }
}
if (-not $matched) { Refuse 'MATCHING_PRIVATE_KEY_NOT_VISIBLE_TO_GPG' }
# A listed secret key can still be an inaccessible stub, card, locked key, or
# unable to decrypt. This NEVER certifies the archive/restore as successful.
Write-Output 'TASK3B_GPG_RECOVERY_PREFLIGHT=MATCHING_PRIVATE_KEY_LISTED_DECRYPTION_UNVERIFIED'
Write-Output 'TASK4_CREATOR_RESTORE=HELD'
