param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][ValidateRange(3,999)][int]$Number,
    [Parameter(Mandatory)][string]$Title,
    [string]$PreviousScene,
    [ValidateSet('rgb','layer')][string]$GroupingMode = 'rgb',
    [string]$GroupConfig,
    [switch]$ReplaceExisting,
    [string]$Python = (Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location -LiteralPath $projectRoot
try {
    if (!(Test-Path -LiteralPath $Python)) { throw 'Set -Python to a Python executable with Pillow and numpy.' }
    if (!$PreviousScene) { $PreviousScene = 'Level_{0:D3}_New' -f ($Number - 1) }
    if ($PreviousScene -notmatch '^Level_\d{3}_New$') { throw 'Invalid previous-scene name.' }
    $sceneName = 'Level_{0:D3}_New' -f $Number
    $levelName = 'Level_{0:D3}' -f $Number
    $artFolder = "Assets/_Project/Art/Levels/$levelName"
    $manifest = "ArtSources/$levelName/level_manifest.json"
    $gitState = & git status --short --branch
    if ($LASTEXITCODE -ne 0) { throw 'Git status failed.' }
    Write-Output ($gitState -join "`n")

    function Invoke-UnityJson([string[]]$Arguments) {
        $raw = & unity @Arguments
        if ($LASTEXITCODE -ne 0) { throw ($raw -join "`n") }
        $envelope = ($raw -join "`n") | ConvertFrom-Json
        if (!$envelope.success) { throw (($envelope.errors | ConvertTo-Json -Compress)) }
        if ($null -ne $envelope.notifications) { Write-Warning ($envelope.notifications | ConvertTo-Json -Compress) }
        return $envelope.data
    }
    $null = Invoke-UnityJson @('status','--until-ready','--project-path',$projectRoot,'--format','json')
    $prepareArguments = @("$PSScriptRoot/prepare_level.py",'--source',$Source,'--output',$artFolder,'--scene',$sceneName,'--title',$Title)
    $prepareArguments += @('--grouping-mode',$GroupingMode)
    if ($GroupConfig) { $prepareArguments += @('--group-config',(Resolve-Path -LiteralPath $GroupConfig).Path) }
    $backupRoot = "Logs/LevelCreation/$levelName-$(Get-Date -Format 'yyyyMMddTHHmmss')/backup"
    if ($ReplaceExisting) {
        if (!(Test-Path -LiteralPath $manifest) -or !(Test-Path -LiteralPath "Assets/_Project/Scenes/$sceneName.unity")) {
            throw 'ReplaceExisting requires a backed-up existing manifest and scene.'
        }
        $guard='if(UnityEditor.EditorApplication.isPlaying||UnityEditor.EditorApplication.isCompiling||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new System.InvalidOperationException("Finish Play/compilation and save user scenes before replacement");return true;'
        $null=Invoke-UnityJson @('command','eval','--project-path',$projectRoot,'--caller','plugin','--skill','unity-cli','--format','json','--','--code',$guard)
        $stageRoot=Join-Path $projectRoot ((Split-Path $backupRoot -Parent)+'/staging')
        New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
        Push-Location -LiteralPath $stageRoot
        try {
            & $Python @prepareArguments --validate-only
            if ($LASTEXITCODE -ne 0) { throw 'Replacement preflight failed; active resources unchanged.' }
            & $Python @prepareArguments
            if ($LASTEXITCODE -ne 0) { throw 'Replacement staging failed; active resources unchanged.' }
            & $Python "$PSScriptRoot/check_level.py" $manifest
            if ($LASTEXITCODE -ne 0) { throw 'Replacement staging verification failed.' }
        }
        finally { Pop-Location }
        $stageArt=Join-Path $stageRoot $artFolder
        $activeArt=Join-Path $projectRoot $artFolder
        $activeFiles=@(Get-ChildItem -LiteralPath $activeArt -Recurse -File -Filter '*.png' | ForEach-Object {$_.FullName.Substring($activeArt.Length+1)} | Sort-Object)
        $stagedFiles=@(Get-ChildItem -LiteralPath $stageArt -Recurse -File -Filter '*.png' | ForEach-Object {$_.FullName.Substring($stageArt.Length+1)} | Sort-Object)
        if (Compare-Object $activeFiles $stagedFiles) { throw 'Replacement requires matching PNG paths; no asset deletion or meta migration is automatic.' }
        foreach ($directory in @($artFolder,"ArtSources/$levelName")) {
            $target=Join-Path $backupRoot $directory
            New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
            Copy-Item -LiteralPath $directory -Destination $target -Recurse
        }
    }
    else {
        & $Python @prepareArguments --validate-only
        if ($LASTEXITCODE -ne 0) { throw 'Source preflight failed; no Unity scene was changed.' }
    }
    $sceneBackupFiles = @('Assets/_Project/Scenes/MainMenu.unity',"Assets/_Project/Scenes/$PreviousScene.unity",'ProjectSettings/EditorBuildSettings.asset')
    if ($ReplaceExisting) { $sceneBackupFiles += "Assets/_Project/Scenes/$sceneName.unity" }
    foreach ($file in $sceneBackupFiles) {
        foreach ($item in @($file,"$file.meta")) {
            if (Test-Path -LiteralPath $item) {
                $target = Join-Path $backupRoot $item
                New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
                Copy-Item -LiteralPath $item -Destination $target
            }
        }
    }
    if ($ReplaceExisting) {
        $activeArchive=[IO.Path]::GetFullPath((Join-Path $projectRoot "ArtSources/$levelName"))
        $archiveBackup=[IO.Path]::GetFullPath((Join-Path $projectRoot "$backupRoot/replaced-archive"))
        # Native PowerShell move; validate both absolute targets remain within their named roots.
        if (!$activeArchive.StartsWith((Join-Path $projectRoot 'ArtSources')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
            !$archiveBackup.StartsWith((Join-Path $projectRoot 'Logs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive target.' }
        Move-Item -LiteralPath $activeArchive -Destination $archiveBackup
        Copy-Item -LiteralPath (Join-Path $stageRoot "ArtSources/$levelName") -Destination $activeArchive -Recurse
        foreach ($relative in $stagedFiles) {
            Copy-Item -LiteralPath (Join-Path $stageArt $relative) -Destination (Join-Path $activeArt $relative) -Force
        }
    }
    else {
        & $Python @prepareArguments
        if ($LASTEXITCODE -ne 0) { throw 'Image preparation failed; conflicting files were retained.' }
    }
    & $Python "$PSScriptRoot/check_level.py" $manifest
    if ($LASTEXITCODE -ne 0) { throw 'Prepared-file checks failed.' }
    foreach ($code in @(
        "return LevelCreationTools.Import(`"$artFolder`");",
        "return LevelCreationTools.Create(`"$artFolder`",`"$PreviousScene`",$($ReplaceExisting.ToString().ToLowerInvariant()));",
        "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(`"Assets/_Project/Scenes/$sceneName.unity`"); return LevelCreationTools.ValidatePixels(`"$artFolder`",4);"
    )) {
        $data = Invoke-UnityJson @('command','eval','--project-path',$projectRoot,'--caller','plugin','--skill','unity-cli','--format','json','--timeout','120','--','--code',$code,'--timeout','60000')
        if ($data.result.success -ne $true) { throw ($data.result | ConvertTo-Json -Compress -Depth 8) }
        Write-Output $data.result.result
    }
    $receiptDirectory=Join-Path $projectRoot 'AgentSync/level-imports'
    New-Item -ItemType Directory -Path $receiptDirectory -Force | Out-Null
    $artifactHashes=@{}
    foreach($artifact in @($manifest,"Assets/_Project/Scenes/$sceneName.unity",'Assets/_Project/Scenes/MainMenu.unity',"Assets/_Project/Scenes/$PreviousScene.unity",'ProjectSettings/EditorBuildSettings.asset')) {
        $artifactHashes[$artifact]=(Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $observedAt=(Get-Date).ToUniversalTime().ToString('o')
    $receipt=@{sceneName=$sceneName;observedAtUtc=$observedAt;status='static_import_passed';manifest=$manifest;artifactHashes=$artifactHashes;playMode='not_run';androidBuild='not_run'}
    $receiptPath=Join-Path $receiptDirectory "$sceneName.json"
    [IO.File]::WriteAllText($receiptPath,($receipt|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    $facts=@{
        completed=@("Static import: $sceneName; source/PNG/Unity/palette/colliders/menu/transitions/Build Settings checked.")
        impact='Creates the requested level and updates its menu/previous-level/build connections. Runtime scripts are reused.'
        checks=@(@{name="$sceneName static import";status='PASS';scope='Static import and ValidatePixels stride=4. Play Mode not implied.';evidence="AgentSync/level-imports/$sceneName.json";observedAtUtc=$observedAt;artifactHashes=$artifactHashes})
        issues=@('Play Mode and device behaviour have not been checked by this command. Android build has not been run by this command.')
        nextSteps=@('Update SCENE_MAP; run permitted Play/Android checks, record results, regenerate report and publish via the connected Drive agent.')
    }
    $factsPath=Join-Path $receiptDirectory "$sceneName-observations.json"
    [IO.File]::WriteAllText($factsPath,($facts|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    & (Join-Path $projectRoot 'Tools/Generate-AgentReport.ps1') -Task "Import $sceneName" -Status static_import_passed -Observations $factsPath -Python $Python
    Write-Output "STATIC IMPORT COMPLETE: $sceneName. Local report prepared; the host agent publishes it through Drive. Play Mode and Android validation remain separate. Backup: $backupRoot"
}
finally { Pop-Location }
