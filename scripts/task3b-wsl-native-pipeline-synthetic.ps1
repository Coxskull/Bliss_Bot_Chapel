# ALPHA Task #4 — Windows PowerShell 5.1 native pipeline synthetic-only smoke.
# Owner local only. NO encrypted backup, GPG, PostgreSQL, remote calls, or disk writes.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
function Receipt([string]$stdin,[string]$fd3,[string]$armor) {
  Write-Output ('TASK3B_PIPE_STDIN='+$stdin)
  Write-Output ('TASK3B_PIPE_FD3='+$fd3)
  Write-Output ('TASK3B_PIPE_ARMOR_SYNTHETIC='+$armor)
  Write-Output 'TASK4_CREATOR_RESTORE=HELD'
}
$stdin='REFUSED'; $fd3='NOT_RUN'; $armor='NOT_RUN'
try {
  # Explicitly reproduce the owner's proven BOM-free PowerShell-to-WSL encoding.
  # A child powershell.exe -NoProfile has its OWN encoding settings.
  $utf8=New-Object System.Text.UTF8Encoding($false)
  $OutputEncoding=$utf8
  [Console]::InputEncoding=$utf8
  $wsl=(Get-Command wsl.exe -ErrorAction Stop).Source
  $public='ALPHA_STDIN_PROBE'

  # Stage 1: use the PowerShell native command pipeline, not ProcessStartInfo.
  $response=@(Write-Output $public | & $wsl -d Ubuntu -u root -- /bin/cat 2>$null)
  if ($LASTEXITCODE -ne 0 -or $response.Count -ne 1 -or
      $response[0].Trim() -cne $public) {
    Receipt $stdin $fd3 $armor; exit 1
  }
  $stdin='PASS'

  # Stage 2: preserve original stdin as Bash FD3 while passing source as an
  # ASCII Base64 command argument. No data or secret source used here.
  $helper=@'
set -euo pipefail
IFS= read -r token <&3
# Windows PowerShell 5.1 native pipelines may terminate this probe with CRLF.
# Strip exactly one terminal CR from the public synthetic marker only.
token="${token%$'\r'}"
if [[ "$token" == "ALPHA_STDIN_PROBE" ]]; then
  printf 'TASK3B_SYNTHETIC_FD3=PASS\n'
else
  printf 'TASK3B_SYNTHETIC_FD3=REFUSED\n'
  exit 2
fi
'@
  $helper=$helper.Replace(([string][char]13+[string][char]10),([string][char]10)).Replace(([string][char]13),'')
  $script64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($helper))
  $cmd='exec 3<&0; set -o pipefail; printf %s '+$script64+' | base64 -d | bash -s -- 3<&3 2>/dev/null'
  $response=@(Write-Output $public | & $wsl -d Ubuntu -u root -- bash -lc $cmd 2>$null)
  if ($LASTEXITCODE -ne 0 -or $response.Count -ne 1 -or
      $response[0].Trim() -cne 'TASK3B_SYNTHETIC_FD3=PASS') {
    $fd3='REFUSED'; Receipt $stdin $fd3 $armor; exit 1
  }
  $fd3='PASS'

  # Stage 3: run existing Python decoder in probe-only mode with 8197
  # deterministic synthetic bytes (including 0x8f, non-UTF-8).
  $receiver=Join-Path $PSScriptRoot 'task3b-wsl-ascii-armor-receiver.py'
  if (-not (Test-Path -LiteralPath $receiver -PathType Leaf)) {
    $armor='REFUSED'; Receipt $stdin $fd3 $armor; exit 1
  }
  $py=Get-Content -LiteralPath $receiver -Raw
  $py=$py.Replace(([string][char]13+[string][char]10),([string][char]10)).Replace(([string][char]13),'')
  $py64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($py))
  $bytes=New-Object byte[] 8197
  [byte[]]$magic=[Text.Encoding]::ASCII.GetBytes('PGDMP')
  [Array]::Copy($magic,0,$bytes,0,5)
  for ($i=5;$i -lt $bytes.Length;$i++) { $bytes[$i]=143 }
  $base64=[Convert]::ToBase64String($bytes)
  $sha=[Security.Cryptography.SHA256]::Create()
  $digest=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','').ToLowerInvariant()
  $sha.Dispose()
  $cmd='exec 3<&0; set -o pipefail; printf %s '+$py64+' | base64 -d | python3 - --probe-only 3<&3 2>/dev/null'
  $response=@(Write-Output $base64 | & $wsl -d Ubuntu -u root -- bash -lc $cmd 2>$null)
  $foundHash=@($response | Where-Object { $_ -ceq ('TASK3B_ARMOR_WSL_SHA256='+$digest) })
  $foundProbe=@($response | Where-Object { $_ -ceq 'TASK3B_ARMOR_WSL_PG18=PROBE_ONLY' })
  if ($LASTEXITCODE -eq 0 -and $foundHash.Count -eq 1 -and $foundProbe.Count -eq 1) {
    $armor='PASS'; Receipt $stdin $fd3 $armor; exit 0
  }
  $armor='REFUSED'; Receipt $stdin $fd3 $armor; exit 1
} catch {
  Receipt $stdin $fd3 'REFUSED'
  exit 1
}
