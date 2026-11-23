# Task #4: owner-local synthetic Windows-to-WSL input transport diagnostic.
# No GPG, no archive, no keys, no PostgreSQL, no file writes, no network.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'

function Emit([string]$raw, [string]$fd3, [string]$armor) {
  Write-Output ('TASK3B_WSL_STDIN='+$raw)
  Write-Output ('TASK3B_WSL_FD3='+$fd3)
  Write-Output ('TASK3B_WSL_ARMOR_SYNTHETIC='+$armor)
  Write-Output 'TASK4_CREATOR_RESTORE=HELD'
}
function ExecSynthetic([string]$arguments,[byte[]]$payload) {
  $p=$null
  try {
    $wsl=Get-Command wsl.exe -ErrorAction Stop
    $psi=New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName=$wsl.Source
    $psi.Arguments=$arguments
    $psi.CreateNoWindow=$true
    $psi.UseShellExecute=$false
    $psi.RedirectStandardInput=$true
    $psi.RedirectStandardOutput=$true
    $psi.RedirectStandardError=$true
    $p=New-Object System.Diagnostics.Process
    $p.StartInfo=$psi
    if (-not $p.Start()) { return '' }
    $p.BeginErrorReadLine()
    try {
      $p.StandardInput.BaseStream.Write($payload,0,$payload.Length)
    } catch {
      # Keep the fixed result fail-closed, but read any bounded existing status.
    }
    try { $p.StandardInput.Close() } catch {}
    if (-not $p.WaitForExit(12000)) {
      try { $p.Kill() } catch {}
      return ''
    }
    if ($p.ExitCode -ne 0) { return '' }
    $output=$p.StandardOutput.ReadToEnd().Replace(([string][char]13),'')
    if ($output.Length -gt 2048) { return '' }
    return $output
  } catch {
    return ''
  } finally {
    if ($null -ne $p) {
      try { if (-not $p.HasExited) { $p.Kill() } } catch {}
      $p.Dispose()
    }
  }
}
$raw='REFUSED'
$fd3='NOT_RUN'
$armor='NOT_RUN'
try {
  $msg=[Text.Encoding]::ASCII.GetBytes(('ALPHA_STDIN_PROBE'+[char]10))
  $r=ExecSynthetic '-d Ubuntu -u root -- cat' $msg
  if ($r.Trim() -cne 'ALPHA_STDIN_PROBE') { Emit $raw $fd3 $armor; exit 1 }
  $raw='PASS'

  # Exercise exactly the existing Bash FD3 duplication and script-source pipe.
  # Code is static and supplied only as Base64 argument, not stdin.
  $shell=@'
set -euo pipefail
IFS= read -r token <&3
if [[ "$token" == "ALPHA_STDIN_PROBE" ]]; then
  printf 'TASK3B_SYNTHETIC_FD3=PASS\n'
else
  printf 'TASK3B_SYNTHETIC_FD3=REFUSED\n'
  exit 2
fi
'@
  $shell=$shell.Replace(([string][char]13+[string][char]10),([string][char]10)).Replace(([string][char]13),'')
  $shell64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($shell))
  $fd3Cmd='exec 3<&0; set -o pipefail; printf %s '+$shell64+' | base64 -d | bash -s -- 3<&3 2>/dev/null'
  $r=ExecSynthetic ('-d Ubuntu -u root -- bash -lc "'+$fd3Cmd+'"') $msg
  if ($r.Trim() -cne 'TASK3B_SYNTHETIC_FD3=PASS') {
    $fd3='REFUSED'
    Emit $raw $fd3 $armor
    exit 1
  }
  $fd3='PASS'

  # Run the actual reviewed decoder in PROBE_ONLY mode with public fake data.
  # It never starts pg_restore in this mode, or accesses a DB or ciphertext.
  $receiver=Join-Path $PSScriptRoot 'task3b-wsl-ascii-armor-receiver.py'
  if (-not (Test-Path -LiteralPath $receiver -PathType Leaf)) {
    $armor='REFUSED'; Emit $raw $fd3 $armor; exit 1
  }
  $py=Get-Content -LiteralPath $receiver -Raw
  $py=$py.Replace(([string][char]13+[string][char]10),([string][char]10)).Replace(([string][char]13),'')
  $py64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($py))
  $payload=New-Object byte[] 8197
  [byte[]]$magic=[Text.Encoding]::ASCII.GetBytes('PGDMP')
  [Array]::Copy($magic,0,$payload,0,5)
  for($i=5;$i -lt $payload.Length;$i++) { $payload[$i]=143 }
  $codec=[Text.Encoding]::ASCII.GetBytes([Convert]::ToBase64String($payload))
  $sha=[Security.Cryptography.SHA256]::Create()
  $wanted=[BitConverter]::ToString($sha.ComputeHash($payload)).Replace('-','').ToLowerInvariant()
  $sha.Dispose()
  $armorCmd='exec 3<&0; set -o pipefail; printf %s '+$py64+' | base64 -d | python3 - --probe-only 3<&3 2>/dev/null'
  $r=ExecSynthetic ('-d Ubuntu -u root -- bash -lc "'+$armorCmd+'"') $codec
  if ($r -match '(?m)^TASK3B_ARMOR_WSL_SHA256='+$wanted+'$' -and
      $r -match '(?m)^TASK3B_ARMOR_WSL_PG18=PROBE_ONLY$') {
    $armor='PASS'
    Emit $raw $fd3 $armor
    exit 0
  }
  $armor='REFUSED'
  Emit $raw $fd3 $armor
  exit 1
} catch {
  Emit $raw $fd3 'REFUSED'
  exit 1
}
