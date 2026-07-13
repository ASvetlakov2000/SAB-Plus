param(
    [string]$RevitApiDir = $env:REVIT_API_DIR
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "SABPlus.sln"
$testPath = Join-Path $repositoryRoot "tests\SABPlus.Radial.Core.SmokeTests\bin\Release\net48\SABPlus.Radial.Core.SmokeTests.exe"
$setupSource = Join-Path $repositoryRoot "src\SABPlus.Radial.Setup\bin\Release\net48\SABPlus.Radial.Setup.exe"
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$setupTarget = Join-Path $artifactsDirectory "SABPlus.Radial.Setup.exe"

if ([string]::IsNullOrWhiteSpace($RevitApiDir)) {
    throw "Pass -RevitApiDir or set the REVIT_API_DIR environment variable."
}

if (-not (Test-Path -LiteralPath (Join-Path $RevitApiDir "RevitAPI.dll")) -or
    -not (Test-Path -LiteralPath (Join-Path $RevitApiDir "RevitAPIUI.dll"))) {
    throw "RevitApiDir must contain RevitAPI.dll and RevitAPIUI.dll."
}

dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) {
    throw "Package restore failed."
}

dotnet build $solutionPath -c Release --no-restore -p:RevitApiDir="$RevitApiDir"
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed."
}

& $testPath
if ($LASTEXITCODE -ne 0) {
    throw "Smoke tests failed."
}

New-Item -ItemType Directory -Force -Path $artifactsDirectory | Out-Null
Copy-Item -LiteralPath $setupSource -Destination $setupTarget -Force
Write-Host "Created: $setupTarget"
