param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.0.68f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$dotnetPath = Join-Path $repoRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) { throw 'Run scripts/bootstrap-dotnet.ps1 first.' }
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Pinned Unity executable not found: $UnityPath" }
$unityEditorRoot = Split-Path (Resolve-Path -LiteralPath $UnityPath).Path -Parent
$projectPath = Join-Path $repoRoot 'tools/RubikSim.UnityApi.csproj'
New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot 'artifacts') | Out-Null
foreach ($mode in @('Editor', 'Web')) {
    $logPath = Join-Path $repoRoot ("artifacts/unity-api-" + $mode.ToLowerInvariant() + '.log')
    & $dotnetPath build $projectPath -c Release "-p:UnityEditorRoot=$unityEditorRoot" "-p:ApiMode=$mode" --no-incremental 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "Real Unity API compilation failed for $mode. See $logPath" }
}
Write-Output 'Both source branches compiled against the supplied Unity assemblies. This does not run the Unity engine, shaders, IL2CPP or Web player.'
