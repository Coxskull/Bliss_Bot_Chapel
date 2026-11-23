# Task 4: ASCII-armored Windows GPG stream to WSL PostgreSQL 18 TOC preflight.
# No database access, plaintext file, customer-row output, secrets, or Railway calls.
[CmdletBinding()]
param([string]$ManifestPath = (Join-Path (Join-Path $HOME 'Downloads') 'bliss-pg18-20261122T055153Z-b34126b0.pending.json'))
$ErrorActionPreference='Stop'
$gproc=$null; $wproc=$null
function Receipt([string]$native,[string]$wsl,[string]$fidelity='NOT_VERIFIED') {
  Write-Output ('TASK3B_NATIVE_PGDMP='+$native)
  Write-Output ('TASK3B_ARMOR_BYTE_FIDELITY='+$fidelity)
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

  # The Python helper source is checked into this repository and passed as
  # command-line Base64 ASCII; WSL FD3 separately receives ASCII Base64 data.
  # No decrypted binary bytes travel directly through wsl.exe stdin.
  $helperFile=Join-Path $PSScriptRoot 'task3b-wsl-ascii-armor-receiver.py'
  if (-not (Test-Path -LiteralPath $helperFile -PathType Leaf)) { Refuse }
  $helper=Get-Content -LiteralPath $helperFile -Raw
  $helper=$helper.Replace(([string][char]13+[string][char]10),([string][char]10)).Replace(([string][char]13),'')
  $b64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($helper))
  $cmd='exec 3<&0; set -o pipefail; printf %s '+$b64+' | base64 -d | python3 - 3<&3 2>/dev/null'
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
  [byte[]]$script:carry=New-Object byte[] 2
  $script:carryLen=0
  [int64]$script:plainBytes=0
  [int64]$script:maxBytes=64*1024*1024
  $script:transferBad=$false
  $script:tooLarge=$false
  $script:sha=[Security.Cryptography.SHA256]::Create()
  function SendAsciiArmored([byte[]]$data,[int]$offset,[int]$count) {
    if ($count -le 0) { return }
    $script:plainBytes += $count
    if ($script:plainBytes -gt $script:maxBytes) { $script:tooLarge=$true; return }
    if ($script:transferBad) { return }
    [void]$script:sha.TransformBlock($data,$offset,$count,$data,$offset)
    [byte[]]$combined=New-Object byte[] ($script:carryLen+$count)
    if ($script:carryLen -gt 0) { [Array]::Copy($script:carry,0,$combined,0,$script:carryLen) }
    [Array]::Copy($data,$offset,$combined,$script:carryLen,$count)
    $bulk=$combined.Length-($combined.Length % 3)
    if ($bulk -gt 0) {
      [byte[]]$ascii=[Text.Encoding]::ASCII.GetBytes([Convert]::ToBase64String($combined,0,$bulk))
      try { $script:wproc.StandardInput.BaseStream.Write($ascii,0,$ascii.Length) }
      catch { $script:transferBad=$true }
      [Array]::Clear($ascii,0,$ascii.Length)
    }
    $script:carryLen=$combined.Length-$bulk
    if ($script:carryLen -gt 0) { [Array]::Copy($combined,$bulk,$script:carry,0,$script:carryLen) }
    [Array]::Clear($combined,0,$combined.Length)
  }
  # Remove ONLY the exact independently confirmed UTF8 BOM, if present.
  SendAsciiArmored $header $headerStart (8-$headerStart)
  while (($length=$gproc.StandardOutput.BaseStream.Read($buffer,0,$buffer.Length)) -gt 0) {
    SendAsciiArmored $buffer 0 $length
  }
  if (-not $script:tooLarge -and -not $script:transferBad) {
    if ($script:carryLen -gt 0) {
      [byte[]]$tail=[Text.Encoding]::ASCII.GetBytes([Convert]::ToBase64String($script:carry,0,$script:carryLen))
      try { $wproc.StandardInput.BaseStream.Write($tail,0,$tail.Length) }
      catch { $script:transferBad=$true }
      [Array]::Clear($tail,0,$tail.Length)
    }
  }
  try { $wproc.StandardInput.Close() } catch {}
  $gproc.WaitForExit()
  $output=$wproc.StandardOutput.ReadToEnd()
  $wproc.WaitForExit()
  [void]$script:sha.TransformFinalBlock([byte[]]@(),0,0)
  $expectedDigest=[BitConverter]::ToString($script:sha.Hash).Replace('-','').ToLowerInvariant()
  $script:sha.Dispose()
  [Array]::Clear($buffer,0,$buffer.Length)
  [Array]::Clear($script:carry,0,$script:carry.Length)
  if ($gproc.ExitCode -ne 0) { Receipt 'DECRYPT_REFUSED' 'NOT_VERIFIED'; exit 1 }
  if ($script:tooLarge) { Receipt $nativeMarker 'SIZE_LIMIT_REFUSED'; exit 1 }
  $lines=@($output -split "\r?\n" | Where-Object { $_ -ne '' })
  $hashes=@($lines | Where-Object { $_ -cmatch '^TASK3B_ARMOR_WSL_SHA256=[a-f0-9]{64}
  if ($bridge.Count -eq 1) { Receipt $nativeMarker 'BRIDGE_REFUSED'; exit 1 }
  if ($hashes.Count -ne 1) { Receipt $nativeMarker 'BRIDGE_REFUSED'; exit 1 }
  $receivedDigest=$hashes[0].Substring('TASK3B_ARMOR_WSL_SHA256='.Length)
  if ($receivedDigest -cne $expectedDigest) { Receipt $nativeMarker 'BRIDGE_REFUSED' 'SHA256_MISMATCH'; exit 1 }
  $pg=@($lines | Where-Object { $_ -cmatch '^TASK3B_ARMOR_WSL_PG18=(PASS_ARCHIVE_FORMAT|PG_FORMAT_REFUSED|PG18_UNAVAILABLE)$' })
  if ($pg.Count -ne 1) { Receipt $nativeMarker 'PREFLIGHT_REFUSED' 'PASS_BYTE_IDENTICAL'; exit 1 }
  $pgStatus=$pg[0].Substring('TASK3B_ARMOR_WSL_PG18='.Length)
  if ($wproc.ExitCode -eq 0 -and $pgStatus -ceq 'PASS_ARCHIVE_FORMAT') {
    Receipt $nativeMarker 'PASS_ARCHIVE_FORMAT' 'PASS_BYTE_IDENTICAL'; exit 0
  }
  Receipt $nativeMarker $pgStatus 'PASS_BYTE_IDENTICAL'; exit 1
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
 })
  $bridge=@($lines | Where-Object { $_ -cmatch '^TASK3B_ARMOR_WSL_HEADER=(NOT_PGDMP|BRIDGE_REFUSED)
  if ($bridge.Count -eq 1) { Receipt $nativeMarker 'BRIDGE_REFUSED'; exit 1 }
  if ($hashes.Count -ne 1) { Receipt $nativeMarker 'BRIDGE_REFUSED'; exit 1 }
  $receivedDigest=$hashes[0].Substring('TASK3B_ARMOR_WSL_SHA256='.Length)
  if ($receivedDigest -cne $expectedDigest) { Receipt $nativeMarker 'BRIDGE_REFUSED' 'SHA256_MISMATCH'; exit 1 }
  $pg=@($lines | Where-Object { $_ -cmatch '^TASK3B_ARMOR_WSL_PG18=(PASS_ARCHIVE_FORMAT|PG_FORMAT_REFUSED|PG18_UNAVAILABLE)$' })
  if ($pg.Count -ne 1) { Receipt $nativeMarker 'PREFLIGHT_REFUSED' 'PASS_BYTE_IDENTICAL'; exit 1 }
  $pgStatus=$pg[0].Substring('TASK3B_ARMOR_WSL_PG18='.Length)
  if ($wproc.ExitCode -eq 0 -and $pgStatus -ceq 'PASS_ARCHIVE_FORMAT') {
    Receipt $nativeMarker 'PASS_ARCHIVE_FORMAT' 'PASS_BYTE_IDENTICAL'; exit 0
  }
  Receipt $nativeMarker $pgStatus 'PASS_BYTE_IDENTICAL'; exit 1
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
 })
  # Preserve the receiver's safe reason even when it closes its input early.
  # The previous blanket TRANSFER_REFUSED concealed these useful categories.
  if ($script:transferBad) {
    if ($bridge.Count -eq 1) { Receipt $nativeMarker 'RECEIVER_REFUSED'; exit 1 }
    if ($wproc.ExitCode -ne 0) { Receipt $nativeMarker 'WSL_EARLY_EXIT'; exit 1 }
    Receipt $nativeMarker 'HOST_PIPE_REFUSED'
    exit 1
  }
  if ($bridge.Count -eq 1) { Receipt $nativeMarker 'BRIDGE_REFUSED'; exit 1 }
  if ($hashes.Count -ne 1) { Receipt $nativeMarker 'BRIDGE_REFUSED'; exit 1 }
  $receivedDigest=$hashes[0].Substring('TASK3B_ARMOR_WSL_SHA256='.Length)
  if ($receivedDigest -cne $expectedDigest) { Receipt $nativeMarker 'BRIDGE_REFUSED' 'SHA256_MISMATCH'; exit 1 }
  $pg=@($lines | Where-Object { $_ -cmatch '^TASK3B_ARMOR_WSL_PG18=(PASS_ARCHIVE_FORMAT|PG_FORMAT_REFUSED|PG18_UNAVAILABLE)$' })
  if ($pg.Count -ne 1) { Receipt $nativeMarker 'PREFLIGHT_REFUSED' 'PASS_BYTE_IDENTICAL'; exit 1 }
  $pgStatus=$pg[0].Substring('TASK3B_ARMOR_WSL_PG18='.Length)
  if ($wproc.ExitCode -eq 0 -and $pgStatus -ceq 'PASS_ARCHIVE_FORMAT') {
    Receipt $nativeMarker 'PASS_ARCHIVE_FORMAT' 'PASS_BYTE_IDENTICAL'; exit 0
  }
  Receipt $nativeMarker $pgStatus 'PASS_BYTE_IDENTICAL'; exit 1
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
