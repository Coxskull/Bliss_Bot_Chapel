# ALPHA Task #4: owner-only read-only decrypted format envelope check (Windows-native).
# No plaintext or customer data shown, saved, exported or sent to WSL/PostgreSQL.
[CmdletBinding()]
param([string]$ManifestPath = (Join-Path (Join-Path $HOME 'Downloads') 'bliss-pg18-20261122T055153Z-b34126b0.pending.json'))
$ErrorActionPreference='Stop'
$p=$null
function Report([string]$code) {
  Write-Output ('TASK3B_NATIVE_ENVELOPE='+$code)
  Write-Output 'TASK4_CREATOR_RESTORE=HELD'
}
try {
  if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf) -or
      (Split-Path -Leaf $ManifestPath) -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.pending\.json$') { Report 'PREFLIGHT_REFUSED'; exit 1 }
  $m=Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
  if ($m.schema -cne 'alpha-bliss-pg18-recovery/v1' -or
      $m.sourceProjectId -cne 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -or
      $m.sourceEnvironmentId -cne 'b67a9836-36bf-4083-93f7-00c867b13f39' -or
      $m.sourceServiceId -cne '6aa78407-1632-48c9-8ea4-0289a7d36a5b' -or
      $m.sourceDatabase -cne 'bliss' -or [int]$m.sourcePostgresMajor -ne 18 -or
      [int]$m.sourcePublicTableCount -lt 90 -or
      $m.encryption -cne 'GPG_RECIPIENT' -or
      $m.gpgRecipientFingerprint -cnotmatch '^[A-F0-9]{40}$' -or
      $m.archiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or
      $m.archiveFileName -cnotmatch '^bliss-pg18-[0-9TZ-]+-[a-f0-9]{8}\.gpg$' -or
      $m.archiveFileName -cne ((Split-Path -Leaf $ManifestPath) -replace '\.pending\.json$', '.gpg')) { Report 'PREFLIGHT_REFUSED'; exit 1 }
  $archive=Join-Path (Split-Path -Parent $ManifestPath) ([string]$m.archiveFileName)
  if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { Report 'PREFLIGHT_REFUSED'; exit 1 }
  if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.archiveSha256) { Report 'PREFLIGHT_REFUSED'; exit 1 }
  $gpg=Get-Command gpg.exe -ErrorAction SilentlyContinue
  if ($null -eq $gpg) { Report 'PREFLIGHT_REFUSED'; exit 1 }
  $psi=New-Object Diagnostics.ProcessStartInfo
  $psi.FileName=$gpg.Source
  $psi.Arguments='--batch --no-tty --yes --decrypt --output - "'+$archive+'"'
  $psi.UseShellExecute=$false
  $psi.CreateNoWindow=$true
  $psi.RedirectStandardInput=$true
  $psi.RedirectStandardOutput=$true
  $psi.RedirectStandardError=$true
  $p=New-Object Diagnostics.Process
  $p.StartInfo=$psi
  if (-not $p.Start()) { Report 'PREFLIGHT_REFUSED'; exit 1 }
  $p.BeginErrorReadLine()
  $p.StandardInput.Close()
  # Retain only first 1024 binary bytes in memory and discard all remaining bytes.
  [byte[]]$first=New-Object byte[] 1024
  [byte[]]$buf=New-Object byte[] 65536
  [int]$have=0
  [int64]$total=0
  [int64]$limit=64*1024*1024
  while (($n=$p.StandardOutput.BaseStream.Read($buf,0,$buf.Length)) -gt 0) {
    if (($total+$n) -gt $limit) { Report 'SIZE_LIMIT_REFUSED'; exit 1 }
    if ($have -lt $first.Length) {
      $copy=[Math]::Min($n,$first.Length-$have)
      [Array]::Copy($buf,0,$first,$have,$copy)
      $have += $copy
    }
    $total += $n
  }
  $p.WaitForExit()
  if ($p.ExitCode -ne 0) { Report 'DECRYPT_REFUSED'; exit 1 }
  if ($total -eq 0) { Report 'EMPTY'; exit 1 }
  $offset=-1
  for ($i=0; $i -le $have-5; $i++) {
    if ($first[$i] -eq 80 -and $first[$i+1] -eq 71 -and $first[$i+2] -eq 68 -and
        $first[$i+3] -eq 77 -and $first[$i+4] -eq 80) { $offset=$i; break }
  }
  if ($offset -eq 0) { Report 'PGDMP_AT_START'; exit 0 }
  if ($offset -gt 0 -and $offset -le 64) { Report 'PGDMP_AFTER_SHORT_PREFIX'; exit 1 }
  if ($offset -gt 64) { Report 'PGDMP_AFTER_LONG_PREFIX'; exit 1 }
  if ($have -ge 3 -and $first[0] -eq 239 -and $first[1] -eq 187 -and $first[2] -eq 191) { Report 'UTF8_BOM_NO_PGDMP'; exit 1 }
  if ($have -ge 2 -and $first[0] -eq 255 -and $first[1] -eq 254) { Report 'UTF16_BOM_NO_PGDMP'; exit 1 }
  if ($have -ge 2 -and $first[0] -eq 31 -and $first[1] -eq 139) { Report 'GZIP_NOT_PGDMP'; exit 1 }
  if ($have -ge 4 -and $first[0] -eq 80 -and $first[1] -eq 75 -and $first[2] -eq 3 -and $first[3] -eq 4) { Report 'ZIP_NOT_PGDMP'; exit 1 }
  if ($have -ge 4 -and $first[0] -eq 40 -and $first[1] -eq 181 -and $first[2] -eq 47 -and $first[3] -eq 253) { Report 'ZSTD_NOT_PGDMP'; exit 1 }
  if ($have -ge 7 -and [Text.Encoding]::ASCII.GetString($first,0,7) -ceq 'UEdETVA') { Report 'BASE64_TEXT_NOT_PGDMP'; exit 1 }
  if ($have -ge 10 -and [Text.Encoding]::ASCII.GetString($first,0,10) -ceq '-----BEGIN') { Report 'ARMORED_NOT_PGDMP'; exit 1 }
  Report 'UNKNOWN_NOT_PGDMP'
  exit 1
} catch {
  Report 'PREFLIGHT_REFUSED'
  exit 1
} finally {
  if ($null -ne $p) {
    try { if (-not $p.HasExited) { $p.Kill() } } catch {}
    $p.Dispose()
  }
}
