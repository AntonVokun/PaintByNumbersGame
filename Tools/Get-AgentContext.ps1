param(
    [ValidateSet('overview','architecture','scenes','gameplay','levels','assets','troubleshooting','sync')][string]$Topic='overview',
    [switch]$IncludeStatus
)
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$routes=@{overview='INDEX.md';architecture='PROJECT_MAP.md';scenes='SCENE_MAP.md';gameplay='GAMEPLAY_SYSTEMS.md';levels='LEVEL_PIPELINE.md';assets='ASSET_STRUCTURE.md';troubleshooting='TROUBLESHOOTING.md';sync='AGENT_SYNC_GUIDE.md'}
Get-Content -LiteralPath (Join-Path $projectRoot 'AGENTS.md') -Raw
Get-Content -LiteralPath (Join-Path $projectRoot ('docs/'+$routes[$Topic])) -Raw
if($IncludeStatus -and (Test-Path -LiteralPath (Join-Path $projectRoot 'AgentSync/STATUS.md'))){
    Get-Content -LiteralPath (Join-Path $projectRoot 'AgentSync/STATUS.md') -Raw
}
