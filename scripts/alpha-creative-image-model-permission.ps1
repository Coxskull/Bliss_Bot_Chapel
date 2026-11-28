# ALPHA read-only GPT Image model visibility probe.
# Prompts for the project key as hidden input; never writes it to disk or prints it.
# This performs GET /v1/models/gpt-image-2.5-flare only. It does NOT generate an image.
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'alpha-creative-image-model-permission.mjs'
$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) {
    [Console]::Error.WriteLine('MODEL_PROBE_HELD: NODE_NOT_FOUND')
    exit 2
}
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    [Console]::Error.WriteLine('MODEL_PROBE_HELD: PROBE_SCRIPT_NOT_FOUND')
    exit 2
}

$secureKey = $null
$pointer = [IntPtr]::Zero
$plainKey = $null
$previousKey = $env:OPENAI_API_KEY
$previousScope = $env:ALPHA_IMAGE_PILOT_SCOPE
$previousStop = $env:ALPHA_GLOBAL_STOP
$exitCode = 2

try {
    $secureKey = Read-Host -Prompt 'Enter the NEW ALPHA project API key (input hidden)' -AsSecureString
    if ($null -eq $secureKey -or $secureKey.Length -lt 8) {
        throw [System.InvalidOperationException]::new('PRIVATE_PROJECT_KEY_INVALID')
    }
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($plainKey) -or
        $plainKey.Length -gt 512 -or -not $plainKey.StartsWith('sk-')) {
        throw [System.InvalidOperationException]::new('PRIVATE_PROJECT_KEY_INVALID')
    }

    $env:OPENAI_API_KEY = $plainKey
    $env:ALPHA_IMAGE_PILOT_SCOPE = 'ISOLATED_NO_DELIVERY'
    $env:ALPHA_GLOBAL_STOP = 'ENGAGED'

    & $node.Source $scriptPath
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) { $exitCode = 2 }
}
catch {
    [Console]::Error.WriteLine('MODEL_PROBE_HELD: ' + $_.Exception.GetType().Name)
    $exitCode = 2
}
finally {
    if ($pointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
    $env:OPENAI_API_KEY = $previousKey
    $env:ALPHA_IMAGE_PILOT_SCOPE = $previousScope
    $env:ALPHA_GLOBAL_STOP = $previousStop
    $plainKey = $null
    if ($null -ne $secureKey) { $secureKey.Dispose() }
}

exit $exitCode
