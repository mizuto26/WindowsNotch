param(
    [string]$DotnetCommand = "dotnet",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$TargetFramework = "net8.0-windows10.0.19041.0",
    [string]$ProjectPath = "src\WindowsNotch.App\WindowsNotch.App.csproj",
    [string]$ZipPath = "WindowsNotch-win-x64.zip"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectFullPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$projectDirectory = Split-Path $projectFullPath -Parent
$publishDirectory = Join-Path $projectDirectory "bin\$Configuration\$TargetFramework\$Runtime\publish"
$zipFullPath = Join-Path $repoRoot $ZipPath

Write-Host "Publishing WindowsNotch..."
& $DotnetCommand publish $projectFullPath -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=false

if (-not (Test-Path $publishDirectory)) {
    throw "Publish directory was not found: $publishDirectory"
}

if (Test-Path $zipFullPath) {
    Remove-Item -LiteralPath $zipFullPath -Force
}

Write-Host "Creating zip: $zipFullPath"
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipFullPath -Force

Write-Host "Release package created."
Write-Host "Zip path: $zipFullPath"

if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "zip_path=$zipFullPath"
}
