param(
    [ValidateSet('overview','architecture','scenes','gameplay','levels','assets','troubleshooting','sync')][string]$Topic='overview',
    [switch]$IncludeStatus,
    [switch]$Compact,
    [switch]$WithUnity
)
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if($Compact){
    $py=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    $inspectArgs=@((Join-Path $projectRoot 'Tools/LevelCreation/inspect_pipeline.py'),'--project-root',$projectRoot)
    if($WithUnity){$inspectArgs+='--with-unity'}
    & $py @inspectArgs
    if($LASTEXITCODE -ne 0){throw 'Compact context failed; inspect the explicit error.'}
    return
}
$routes=@{overview='INDEX.md';architecture='PROJECT_MAP.md';scenes='SCENE_MAP.md';gameplay='GAMEPLAY_SYSTEMS.md';levels='LEVEL_PIPELINE.md';assets='ASSET_STRUCTURE.md';troubleshooting='TROUBLESHOOTING.md';sync='AGENT_SYNC_GUIDE.md'}
Get-Content -LiteralPath (Join-Path $projectRoot 'AGENTS.md') -Raw
Get-Content -LiteralPath (Join-Path $projectRoot ('docs/'+$routes[$Topic])) -Raw
if($IncludeStatus -and (Test-Path -LiteralPath (Join-Path $projectRoot 'AgentSync/STATUS.md'))){
    Get-Content -LiteralPath (Join-Path $projectRoot 'AgentSync/STATUS.md') -Raw
}
