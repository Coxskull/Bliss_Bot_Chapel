# Task 4: binary-safe Windows-native GPG to WSL PostgreSQL 18 TOC preflight.
# No database access, plaintext file, customer-row output, secrets, or Railway calls.
[CmdletBinding()]
param([string]$ManifestPath = (Join-Path (Join-Path $HOME 'Downloads') 'bliss-pg18-20261122T055153Z-b34126b0.pending.json'))
$ErrorActionPreference='Stop'
$gproc=$null; $wproc=$null
function Receipt([string]$native,[string]$wsl) {
  Write-Output ('TASK3B_NATIVE_PGDMP='+$native)
  Write-Output ('TASK3B_NATIVE_WSL_PG18='+$wsl)
  Write-Output 'TASK4_CREATOR_RESTORE=HELD'
}
function Refuse { Receipt 'NOT_VERIFIED' 'PREFLIGHT_REFUSED'; exit 1 }
try {
  if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf) -or
      (Split-Path -Leaf $ManifestPath) -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.pending\.json$') { Refuse }
  $m=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
  if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
      $m.sourceProjectId -cne 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -or
      $m.sourceEnvironmentId -cne 'b67a9836-36bf-4083-93f7-00c867b13f39' -or
      $m.sourceServiceId -cne '6aa78407-1632-48c9-8ea4-0289a7d36a5b' -or
      $m.sourceDatabase -cne 'bliss' -or [int]$m.sourcePostgresMajor -ne 18 -or
      [int]$m.sourcePublicTableCount -lt 90 -or
      $m.encryption -cne 'GPG_RECIPIENT' -or
      $m.offhostProvider -cne 'NOT_VERIFIED' -or $m.offhostSha256Verified -ne $false -or
      $m.isolatedRestoreVerified -ne $false -or
      $m.gpgRecipientFingerprint -cnotmatch '^[A-F0-9]{40}$' -or
      $m.archiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or
      $m.archiveFileName -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.gpg$' -or
      $m.archiveFileName -cne ((Split-Path -Leaf $ManifestPath) -replace '\.pending\.json$', '.gpg')) { Refuse }
  $archive=Join-Path (Split-Path -Parent $ManifestPath) ([string]$m.archiveFileName)
  if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { Refuse }
  if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.archiveSha256) { Refuse }
  $gpg=Get-Command gpg.exe -ErrorAction SilentlyContinue
  $wsl=Get-Command wsl.exe -ErrorAction SilentlyContinue
  if ($null -eq $gpg -or $null -eq $wsl) { Refuse }

  # Start Windows GPG NATIVELY and use StandardOutput.BaseStream, never a text pipe.
  $gp=New-Object Diagnostics.ProcessStartInfo
  $gp.FileName=$gpg.Source
  $gp.Arguments='--batch --no-tty --yes --decrypt --output - "'+$archive+'"'
  $gp.UseShellExecute=$false; $gp.CreateNoWindow=$true
  $gp.RedirectStandardInput=$true; $gp.RedirectStandardOutput=$true; $gp.RedirectStandardError=$true
  $gproc=New-Object Diagnostics.Process
  $gproc.StartInfo=$gp
  if (-not $gproc.Start()) { Refuse }
  $gproc.BeginErrorReadLine()
  $gproc.StandardInput.Close()

  [byte[]]$magic=New-Object byte[] 5
  $got=0
  while ($got -lt 5) {
    $n=$gproc.StandardOutput.BaseStream.Read($magic,$got,5-$got)
    if ($n -le 0) { break }
    $got += $n
  }
  $pgdmp=($got -eq 5 -and [Text.Encoding]::ASCII.GetString($magic) -ceq 'PGDMP')
  if (-not $pgdmp) {
    [byte[]]$discard=New-Object byte[] 65536
    while ($gproc.StandardOutput.BaseStream.Read($discard,0,$discard.Length) -gt 0) {}
    $gproc.WaitForExit()
    if ($gproc.ExitCode -eq 0) { Receipt 'NOT_PGDMP' 'NOT_RUN' }
    else { Receipt 'DECRYPT_REFUSED' 'NOT_RUN' }
    exit 1
  }

  # FD3 is copied from original WSL stdin BEFORE the Bash script source pipe.
  # WSL reads raw native bytes on fd3 and only lists PG18 TOC, not a database.
  $helper=@'
set -euo pipefail
if [[ ! -x /usr/lib/postgresql/18/bin/pg_restore ]]; then
  cat <&3 >/dev/null 2>/dev/null || true
  printf 'TASK3B_NATIVE_WSL_MARKER=PG18_UNAVAILABLE\n'
  exit 2
fi
set +e
/usr/lib/postgresql/18/bin/pg_restore --list <&3 >/dev/null 2>/dev/null
pg_status=$?
cat <&3 >/dev/null 2>/dev/null
drain_status=$?
if [[ "$pg_status" == 0 && "$drain_status" == 0 ]]; then
  printf 'TASK3B_NATIVE_WSL_MARKER=PASS\n'
  exit 0
fi
printf 'TASK3B_NATIVE_WSL_MARKER=PG_FORMAT_REFUSED\n'
exit 2
'@
  $helper=$helper.Replace(([string][char]13+[string][char]10),([string][char]10)).Replace(([string][char]13),'')
  $b64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($helper))
  $cmd='exec 3<&0; set -o pipefail; printf %s '+$b64+' | base64 -d | bash -s -- 3<&3'
  $wp=New-Object Diagnostics.ProcessStartInfo
  $wp.FileName=$wsl.Source
  $wp.Arguments='-d Ubuntu -u root -- bash -lc "'+$cmd+'"'
  $wp.UseShellExecute=$false; $wp.CreateNoWindow=$true
  $wp.RedirectStandardInput=$true; $wp.RedirectStandardOutput=$true; $wp.RedirectStandardError=$true
  $wproc=New-Object Diagnostics.Process
  $wproc.StartInfo=$wp
  if (-not $wproc.Start()) { Refuse }
  $wproc.BeginErrorReadLine()

  [byte[]]$buffer=New-Object byte[] 65536
  [int64]$total=5
  [int64]$limit=64*1024*1024
  $transferBad=$false
  try { $wproc.StandardInput.BaseStream.Write($magic,0,5) } catch { $transferBad=$true }
  while (($length=$gproc.StandardOutput.BaseStream.Read($buffer,0,$buffer.Length)) -gt 0) {
    $total += $length
    if ($total -gt $limit) { $transferBad=$true }
    if (-not $transferBad) {
      try { $wproc.StandardInput.BaseStream.Write($buffer,0,$length) }
      catch { $transferBad=$true }
    }
  }
  try { $wproc.StandardInput.Close() } catch {}
  $gproc.WaitForExit()
  $output=$wproc.StandardOutput.ReadToEnd()
  $wproc.WaitForExit()

  if ($gproc.ExitCode -ne 0) { Receipt 'DECRYPT_REFUSED' 'NOT_VERIFIED'; exit 1 }
  if ($transferBad) { Receipt 'PASS' 'TRANSFER_REFUSED'; exit 1 }
  $lines=@($output -split "\r?\n")
  if ($wproc.ExitCode -eq 0 -and @($lines | Where-Object { $_ -ceq 'TASK3B_NATIVE_WSL_MARKER=PASS' }).Count -eq 1) {
    Receipt 'PASS' 'PASS_ARCHIVE_FORMAT'; exit 0
  }
  if (@($lines | Where-Object { $_ -ceq 'TASK3B_NATIVE_WSL_MARKER=PG_FORMAT_REFUSED' }).Count -eq 1) {
    Receipt 'PASS' 'PG_FORMAT_REFUSED'; exit 1
  }
  Receipt 'PASS' 'PREFLIGHT_REFUSED'; exit 1
} catch {
  Receipt 'NOT_VERIFIED' 'PREFLIGHT_REFUSED'; exit 1
} finally {
  if ($null -ne $gproc) {
    try { if (-not $gproc.HasExited) { $gproc.Kill() } } catch {}
    $gproc.Dispose()
  }
  if ($null -ne $wproc) {
    try { if (-not $wproc.HasExited) { $wproc.Kill() } } catch {}
    $wproc.Dispose()
  }
}
