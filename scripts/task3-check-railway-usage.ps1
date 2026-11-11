# Task #3 owner-only Railway billing and isolation preflight.
# READ-ONLY: no creation, deploy, restart, or limit changes.
# Run in Windows PowerShell with Railway CLI signed into the Railway owner account.
# Keep billing details private; never share keys/passwords.
$ErrorActionPreference = "Stop"
$workspaceId = "7f378f31-9aa7-46fa-8431-6bbf89a99088"
Write-Host "ALPHA TASK3: READ-ONLY RAILWAY USAGE / LIMIT INSPECTION"
Write-Host "Workspace: $workspaceId (shared alpha7auto projects with production)"
Write-Host "WARNING: The compute hard limit is workspace-wide; never set a low limit here for staging."
if (-not (Get-Command railway -ErrorAction SilentlyContinue)) {
    throw "Railway CLI missing. Use Railway Dashboard > Workspace Usage, or install Railway CLI from official documentation."
}
function Invoke-RailwayReadOnly([string[]]$ArgsArray) {
    Write-Host ""
    Write-Host ("railway " + ($ArgsArray -join " "))
    & railway @ArgsArray
    if ($LASTEXITCODE -ne 0) {
        throw ("Read-only Railway query failed (exit " + $LASTEXITCODE + "). No changes made.")
    }
}
Invoke-RailwayReadOnly @("usage", "--workspace", $workspaceId, "--json")
Invoke-RailwayReadOnly @("usage", "projects", "--workspace", $workspaceId, "--limit", "20", "--json")
Invoke-RailwayReadOnly @("usage", "limit", "status", "--target", "workspace", "--workspace", $workspaceId, "--json")
Write-Host ""
Write-Host "READ_ONLY_INSPECTION=PASS"
Write-Host "ISOLATED_TASK3_COMPUTE_BUDGET=NOT_PROVEN_BY_SHARED_WORKSPACE_LIMIT"
Write-Host "NEXT: owner must verify separate staging workspace and its spending controls before provisioning."
