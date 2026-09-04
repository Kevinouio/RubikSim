param([int]$Count = 100, [int]$FirstSeed = 0, [switch]$Fast)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$dotnetExe = Join-Path $repoRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExe)) { throw 'Run ./scripts/bootstrap-dotnet.ps1 first.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Push-Location $repoRoot
try {
    New-Item -ItemType Directory -Force -Path 'artifacts' | Out-Null
    & $dotnetExe build tools/RubikSim.Compatibility.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'C# 9 / .NET Standard 2.1 compatibility build failed.' }
    $runCount = if ($Fast) { 0 } else { $Count }
    & $dotnetExe run --project tests/RubikSim.Tests.csproj --configuration Release -- --count $runCount --seed $FirstSeed 2>&1 | Tee-Object -FilePath 'artifacts/test-results.log'
    if ($LASTEXITCODE -ne 0) { throw "Tests failed (exit $LASTEXITCODE)." }
} finally { Pop-Location }
