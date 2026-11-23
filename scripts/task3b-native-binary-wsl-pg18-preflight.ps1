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
function GetPgDumpHeaderKind([byte[]]$head,[int]$count) {
  if ($count -ne 8) { return 'UNRECOGNIZED' }
  $zero=($head[0] -eq 80 -and $head[1] -eq 71 -and $head[2] -eq 68 -and
         $head[3] -eq 77 -and $head[4] -eq 80)
  if ($zero) { return 'PGDMP_AT_ZERO' }
  $bom=($head[0] -eq 239 -and $head[1] -eq 187 -and $head[2] -eq 191)
  $offset3=($head[3] -eq 80 -and $head[4] -eq 71 -and $head[5] -eq 68 -and
            $head[6] -eq 77 -and $head[7] -eq 80)
  if ($bom -and $offset3) { return 'EXACT_UTF8_BOM_PGDMP' }
  return 'UNRECOGNIZED'
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

  # Read exactly eight bytes. Accept ONLY verified PGDMP at byte 0, or
  # the independently observed exact UTF-8 BOM EF BB BF followed by PGDMP.
  # Strip the BOM from the TEMPORARY decrypted stream ONLY, never from the
  # encrypted source file or its verified custody/checksum.
  [byte[]]$header=New-Object byte[] 8
  $got=0
  while ($got -lt 8) {
    $n=$gproc.StandardOutput.BaseStream.Read($header,$got,8-$got)
    if ($n -le 0) { break }
    $got += $n
  }
  $kind=GetPgDumpHeaderKind $header $got
  if ($kind -ceq 'UNRECOGNIZED') {
    [byte[]]$discard=New-Object byte[] 65536
    while ($gproc.StandardOutput.BaseStream.Read($discard,0,$discard.Length) -gt 0) {}
    $gproc.WaitForExit()
    if ($gproc.ExitCode -eq 0) { Receipt 'NOT_PGDMP' 'NOT_RUN' }
    else { Receipt 'DECRYPT_REFUSED' 'NOT_RUN' }
    exit 1
  }
  $headerStart=0
  $nativeMarker='PASS'
  if ($kind -ceq 'EXACT_UTF8_BOM_PGDMP') {
    $headerStart=3
    $nativeMarker='PASS_UTF8_BOM_STRIPPED_IN_STREAM'
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
# Capture ONLY pg_restore's stderr into an in-process variable. NEVER print
# diagnostics or archive TOC, which may contain customer object identifiers.
# Redirection order preserves stderr capture, discards stdout TOC.
pg_err="$(/usr/lib/postgresql/18/bin/pg_restore --list <&3 2>&1 1>/dev/null)"
pg_status=$?
# pg_restore may read just the TOC; always drain the rest so native GPG
# finishes authenticated decryption rather than seeing an early broken pipe.
cat <&3 >/dev/null 2>/dev/null
drain_status=$?
if [[ "$pg_status" == "0" && "$drain_status" == "0" ]]; then
  printf 'TASK3B_NATIVE_WSL_MARKER=PASS\n'
  exit 0
fi
# Only emit fixed allowlisted classifications; do not print raw pg_err.
category=PG_FORMAT_UNCLASSIFIED
if [[ "$pg_status" != "0" ]]; then
  case "$pg_err" in
    *"did not find magic string"*|*"not appear to be a valid archive"*|*"input file is too short"*)
      category=PG_FORMAT_MAGIC_OR_SHORT ;;
    *"unsupported version"*)
      category=PG_FORMAT_VERSION_UNSUPPORTED ;;
    *"input file appears to be a text format dump"*)
      category=PG_FORMAT_NONCUSTOM ;;
    *"could not read from input file"*|*"unexpected end of file"*|*"could not read archive"*|*"end of file"*)
      category=PG_FORMAT_READ_OR_TRUNCATED ;;
    *"out of memory"*|*"memory exhausted"*)
      category=PG_FORMAT_RESOURCE_REFUSED ;;
    *"permission denied"*|*"could not open input file"*)
      category=PG_FORMAT_INPUT_IO ;;
    *"unsupported compression"*|*"compression method"*)
      category=PG_FORMAT_COMPRESSION_UNSUPPORTED ;;
    *"invalid archive"*|*"corrupt"*|*"invalid toc"*|*"invalid TOC"*|*"could not find block ID"*)
      category=PG_FORMAT_STRUCTURE_REFUSED ;;
    *) category=PG_FORMAT_UNCLASSIFIED ;;
  esac
elif [[ "$drain_status" != "0" ]]; then
  category=PG_FORMAT_DRAIN_REFUSED
fi
printf 'TASK3B_NATIVE_WSL_MARKER=%s\n' "$category"
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
  [int64]$total=8
  [int64]$limit=64*1024*1024
  $transferBad=$false
  # Forward the complete PGDMP header in the stream. For the known BOM case,
  # skip EXACTLY the three BOM bytes; preserve the remaining bytes bit-for-bit.
  try { $wproc.StandardInput.BaseStream.Write($header,$headerStart,8-$headerStart) }
  catch { $transferBad=$true }
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
  if ($transferBad) { Receipt $nativeMarker 'TRANSFER_REFUSED'; exit 1 }
  $lines=@($output -split "\r?\n")
  if ($wproc.ExitCode -eq 0 -and @($lines | Where-Object { $_ -ceq 'TASK3B_NATIVE_WSL_MARKER=PASS' }).Count -eq 1) {
    Receipt $nativeMarker 'PASS_ARCHIVE_FORMAT'; exit 0
  }
  $allowedPg=@('PG_FORMAT_MAGIC_OR_SHORT','PG_FORMAT_VERSION_UNSUPPORTED',
    'PG_FORMAT_NONCUSTOM','PG_FORMAT_READ_OR_TRUNCATED','PG_FORMAT_RESOURCE_REFUSED',
    'PG_FORMAT_INPUT_IO','PG_FORMAT_COMPRESSION_UNSUPPORTED',
    'PG_FORMAT_STRUCTURE_REFUSED','PG_FORMAT_UNCLASSIFIED','PG_FORMAT_DRAIN_REFUSED')
  $safePg=@($lines | ForEach-Object {
    if ($_ -cmatch '^TASK3B_NATIVE_WSL_MARKER=([A-Z_]+)$' -and
        $allowedPg -ccontains $Matches[1]) { $Matches[1] }
  } | Select-Object -Unique)
  if ($safePg.Count -eq 1) { Receipt $nativeMarker ([string]$safePg[0]); exit 1 }
  Receipt $nativeMarker 'PREFLIGHT_REFUSED'; exit 1
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
