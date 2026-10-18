param(
    [string]$Task = 'Project status',
    [string]$Status = 'ready_for_review',
    [string]$Observations = 'AgentSync/observations.json',
    [string]$Python = 'python',
    [switch]$SkipEditor
)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location -LiteralPath $projectRoot
try {
    $reportArguments=@((Join-Path $PSScriptRoot 'AgentSync/agent_report.py'),'--task',$Task,'--status',$Status,'--observations',$Observations)
    if($SkipEditor){$reportArguments+='--skip-editor'}
    & $Python @reportArguments
    if($LASTEXITCODE -ne 0){throw 'Agent report generation failed; existing STATE retained.'}
}
finally { Pop-Location }
