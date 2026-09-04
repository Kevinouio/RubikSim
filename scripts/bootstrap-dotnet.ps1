$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$toolDir = Join-Path $repoRoot '.tools/dotnet'
$dotnetExe = Join-Path $toolDir 'dotnet.exe'
if (Test-Path -LiteralPath $dotnetExe) {
    $installedVersion = & $dotnetExe --version
    if ($installedVersion -eq '8.0.419') { Write-Output $dotnetExe; exit 0 }
}
$archive = Join-Path $repoRoot '.tools/dotnet-sdk-8.0.419-win-x64.zip'
$expectedHash = 'CB83CCB60CF6A3D9DC3C5F3BF0270114628C5487505CDA21A46FBDB45220FB1A04395C2E041F8025E711A89D6B526CA995E1A3E9A04E31A0A1E2A2A64276B5A8'
New-Item -ItemType Directory -Force -Path $toolDir | Out-Null
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -UseBasicParsing -Uri 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.419/dotnet-sdk-8.0.419-win-x64.zip' -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $expectedHash) {
    throw 'SDK archive SHA-512 mismatch. Remove the local archive and retry.'
}
Expand-Archive -LiteralPath $archive -DestinationPath $toolDir -Force
& $dotnetExe --version
if ($LASTEXITCODE -ne 0) { throw 'Local SDK installation failed.' }
Write-Output $dotnetExe
