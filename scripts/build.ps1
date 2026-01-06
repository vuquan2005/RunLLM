# Build Script for RunLLM Plugin
# Usage: .\build.ps1 [-Configuration Release|Debug] [-Platform x64|ARM64]

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SrcDir = Join-Path $ProjectRoot "src"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " RunLLM Plugin Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration"
Write-Host "Platform:      $Platform"
Write-Host ""

# Restore packages
Write-Host "[1/2] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $SrcDir --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: NuGet restore failed!" -ForegroundColor Red
    exit 1
}
Write-Host "      Packages restored successfully." -ForegroundColor Green

# Build
Write-Host "[2/2] Building project..." -ForegroundColor Yellow
dotnet build $SrcDir -c $Configuration -p:Platform=$Platform --no-restore --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

$OutputDir = Join-Path $SrcDir "bin\$Platform\$Configuration"
$TargetFramework = Get-ChildItem $OutputDir -Directory | Select-Object -First 1 -ExpandProperty Name
$FinalOutput = Join-Path $OutputDir $TargetFramework

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " BUILD SUCCESSFUL" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Output: $FinalOutput"
Write-Host ""
exit 0
