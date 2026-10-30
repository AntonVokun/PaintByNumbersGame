param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][ValidateRange(3,999)][int]$Number,
    [Parameter(Mandatory)][string]$Title,
    [string]$PreviousScene,
    [ValidateSet('rgb','layer')][string]$GroupingMode = 'rgb',
    [string]$GroupConfig,
    [switch]$ReplaceExisting,
    [switch]$LegacyRgbSplit,
    [switch]$GenerateReport,
    [switch]$VerboseOutput,
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
    $diagnosticRoot=Join-Path $projectRoot ("Logs/LevelCreation/diagnostics-"+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $diagnosticRoot -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $diagnosticRoot 'git-before.txt'),($gitState -join "`n"),[Text.UTF8Encoding]::new($false))
    $script:P0UnityCalls=0
    if($VerboseOutput){Write-Output ($gitState -join "`n")}

    function Invoke-UnityJson([string[]]$Arguments) {
        $script:P0UnityCalls++
        $callName='unity-'+$script:P0UnityCalls
        $watch=[Diagnostics.Stopwatch]::StartNew()
        $raw = & unity @Arguments 2> (Join-Path $diagnosticRoot ($callName+'.stderr.log'))
        $exit=$LASTEXITCODE
        [IO.File]::WriteAllText((Join-Path $diagnosticRoot ($callName+'.stdout.log')),($raw -join "`n"),[Text.UTF8Encoding]::new($false))
        $row=@{stage=$Arguments[0];attempt=1;unityCalls=1;exitCode=$exit;durationMs=$watch.Elapsed.TotalMilliseconds;stdoutBytes=[Text.Encoding]::UTF8.GetByteCount(($raw -join "`n"));diagnostic=$callName;cacheHit=$false}
        Add-Content -LiteralPath (Join-Path $diagnosticRoot 'metrics.jsonl') -Value ($row|ConvertTo-Json -Compress)
        if ($exit -ne 0) { throw "UNITY_COMMAND_FAILED: see $diagnosticRoot/$callName.stdout.log" }
        $envelope = ($raw -join "`n") | ConvertFrom-Json
        if (!$envelope.success) { throw "UNITY_COMMAND_FAILED: see $diagnosticRoot/$callName.stdout.log" }
        return $envelope.data
    }
    $null = Invoke-UnityJson @('status','--until-ready','--project-path',$projectRoot,'--format','json')
    # New source contract: one RGB / one full RGBA PNG / one gameplay group.
    # Keep historic replacement and explicit layer mode unchanged.
    if ($GroupingMode -eq 'rgb' -and !$ReplaceExisting -and !$LegacyRgbSplit) {
        & $Python "$PSScriptRoot/check_flat_source.py" --source $Source
        if ($LASTEXITCODE -ne 0) { throw 'Flat source preflight failed; nothing was imported. Use -LegacyRgbSplit only for deliberate older RGB-source workflows.' }
    }
    if ($LegacyRgbSplit -and $GroupingMode -ne 'rgb') { throw '-LegacyRgbSplit only applies to -GroupingMode rgb.' }
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
    $code="return LevelCreationTools.ImportCreateValidate(`"$artFolder`",`"$PreviousScene`",$($ReplaceExisting.ToString().ToLowerInvariant()));"
    $data = Invoke-UnityJson @('command','eval','--project-path',$projectRoot,'--caller','plugin','--skill','unity-cli','--format','json','--timeout','120','--','--code',$code,'--timeout','60000')
    if ($data.result.success -ne $true) { throw "UNITY_BATCH_FAILED: see $diagnosticRoot" }
    if($VerboseOutput){Write-Output $data.result.result}
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
    if($GenerateReport){ & (Join-Path $projectRoot 'Tools/Generate-AgentReport.ps1') -Task "Import $sceneName" -Status static_import_passed -Observations $factsPath -Python $Python }
    Write-Output (@{status="STATIC_IMPORT_COMPLETE";scene=$sceneName;unityCalls=$script:P0UnityCalls;diagnostic=$diagnosticRoot;backup=$backupRoot;playMode="NOT_RUN";reportGenerated=[bool]$GenerateReport}|ConvertTo-Json -Compress)
}
finally { Pop-Location }
