# Task #4: PGDMP signature over authenticated WSL/GPG binary stream; no DB.
# Reuses current manifest + ciphertext SHA; never writes decrypted bytes or calls Railway.
[CmdletBinding()]
param([string]$ManifestPath = (Join-Path (Join-Path $HOME 'Downloads') 'bliss-pg18-20261122T055153Z-b34126b0.pending.json'))
$ErrorActionPreference='Stop'
function Refuse {
  Write-Output 'TASK3B_WSL_PGDMP=REFUSED_OR_INTEROP_UNVERIFIED'
  Write-Output 'TASK4_CREATOR_RESTORE=HELD'
  exit 1
}
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf) -or
    (Split-Path -Leaf $ManifestPath) -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.pending\.json$') { Refuse }
try { $m=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json } catch { Refuse }
if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
    $m.sourceProjectId -cne 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -or
    $m.sourceEnvironmentId -cne 'b67a9836-36bf-4083-93f7-00c867b13f39' -or
    $m.sourceServiceId -cne '6aa78407-1632-48c9-8ea4-0289a7d36a5b' -or
    $m.sourceDatabase -cne 'bliss' -or
    [int]$m.sourcePostgresMajor -ne 18 -or
    $m.encryption -cne 'GPG_RECIPIENT' -or
    $m.gpgRecipientFingerprint -cnotmatch '^[A-F0-9]{40}$' -or
    $m.archiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or
    $m.archiveFileName -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.gpg$' -or
    $m.archiveFileName -cne ((Split-Path -Leaf $ManifestPath) -replace '\.pending\.json$', '.gpg')) { Refuse }
$archive=Join-Path (Split-Path -Parent $ManifestPath) ([string]$m.archiveFileName)
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { Refuse }
try { $sha=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() } catch { Refuse }
if ($sha -cne $m.archiveSha256) { Refuse }
$wsl=Get-Command wsl.exe -ErrorAction SilentlyContinue
$gpg=Get-Command gpg.exe -ErrorAction SilentlyContinue
$helper=Join-Path $PSScriptRoot 'task3b-wsl-pgdmp-header-preflight.sh'
if ($null -eq $wsl -or $null -eq $gpg -or -not (Test-Path -LiteralPath $helper -PathType Leaf)) { Refuse }
$source=(Get-Content -LiteralPath $helper -Raw).Replace("`r`n", "`n").Replace("`r", "")
$helper64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($source))
$archive64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($archive))
$gpg64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($gpg.Source))
# Quote-free Windows PowerShell 5.1 => WSL transport, same invocation as the real helper.
$cmd='set -o pipefail; printf %s '+$helper64+' | base64 -d | bash -s -- '+$archive64+' '+$gpg64
$raw=@(); $exitCode=1; $prior=$ErrorActionPreference
try {
  $ErrorActionPreference='Continue'
  $raw=@(& $wsl.Source -d Ubuntu -u root -- bash -lc $cmd 2>&1)
  $exitCode=$LASTEXITCODE
} catch { $exitCode=1 } finally { $ErrorActionPreference=$prior }
$allow=@('PASS_PGDMP_AND_AUTHENTICATION','NOT_PGDMP','REFUSED_OR_INTEROP_UNVERIFIED')
$statuses=@($raw | ForEach-Object {
  $line=([string]$_).Trim()
  if ($line -cmatch '^TASK3B_WSL_PGDMP=([A-Z_]+)$' -and $allow -ccontains $Matches[1]) { $Matches[1] }
} | Select-Object -Unique)
if ($exitCode -eq 0 -and $statuses.Count -eq 1 -and $statuses[0] -ceq 'PASS_PGDMP_AND_AUTHENTICATION') {
  Write-Output 'TASK3B_WSL_PGDMP=PASS_PGDMP_AND_AUTHENTICATION'
  Write-Output 'TASK4_CREATOR_RESTORE=HELD'
  exit 0
}
if ($statuses.Count -eq 1 -and $statuses[0] -ceq 'NOT_PGDMP') {
  Write-Output 'TASK3B_WSL_PGDMP=NOT_PGDMP'
  Write-Output 'TASK4_CREATOR_RESTORE=HELD'
  exit 1
}
Refuse
