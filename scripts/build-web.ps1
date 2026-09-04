param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.0.68f1/Editor/Unity.exe', [switch]$VerifyViewOnly)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity Editor not found at '$UnityPath'. Install Unity 6000.0.68f1 with Web Build Support and activate a valid license; pass -UnityPath for a custom location."
}
New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot 'artifacts') | Out-Null
$entrypoint = if ($VerifyViewOnly) { 'RubikSim.Editor.BuildWeb.VerifyView' } else { 'RubikSim.Editor.BuildWeb.Build' }
$logFile = Join-Path $repoRoot $(if ($VerifyViewOnly) { 'artifacts/unity-view.log' } else { 'artifacts/unity-build.log' })
$arguments = @('-batchmode','-quit','-projectPath',('"' + $repoRoot + '"'),'-buildTarget','WebGL','-executeMethod',$entrypoint,'-logFile',('"' + $logFile + '"'))
$resultPath = Join-Path $repoRoot $(if ($VerifyViewOnly) { 'artifacts/unity-view-result.json' } else { 'artifacts/unity-build-result.json' })
$startedUtc = [DateTime]::UtcNow
$buildProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($buildProcess.ExitCode -ne 0) { throw "Unity command failed (exit $($buildProcess.ExitCode)). See $logFile" }
if (-not (Test-Path -LiteralPath $resultPath) -or (Get-Item -LiteralPath $resultPath).LastWriteTimeUtc -lt $startedUtc) {
    throw "Unity exited without fresh verification evidence. See $logFile"
}
if (-not $VerifyViewOnly -and -not (Test-Path -LiteralPath (Join-Path $repoRoot 'website/unity/build-manifest.json'))) {
    throw "Unity exited without the required build manifest. See $logFile"
}
Write-Output "Unity command succeeded. Log: $logFile"
