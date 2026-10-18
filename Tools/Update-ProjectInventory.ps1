param([switch]$ValidateDocs)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location -LiteralPath $projectRoot
try {
    & python (Join-Path $PSScriptRoot 'AgentSync/agent_report.py') --inventory
    if($LASTEXITCODE -ne 0){throw 'Inventory failed'}
    if($ValidateDocs){
        & python (Join-Path $PSScriptRoot 'AgentSync/agent_report.py') --validate-docs
        if($LASTEXITCODE -ne 0){throw 'Documentation validation failed'}
    }
}
finally { Pop-Location }
