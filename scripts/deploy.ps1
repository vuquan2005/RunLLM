# Deploy Script for RunLLM Plugin
# Usage: .\deploy.ps1 [-Configuration Release|Debug] [-Platform x64|ARM64] [-NoRestart]

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",
    
    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SrcDir = Join-Path $ProjectRoot "src"
$PluginName = "RunLLM"
$PluginDest = Join-Path $env:LOCALAPPDATA "Microsoft\PowerToys\PowerToys Run\Plugins\$PluginName"

# Find build output
$OutputDir = Join-Path $SrcDir "bin\$Platform\$Configuration"
$TargetFramework = Get-ChildItem $OutputDir -Directory -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Name
if (-not $TargetFramework) {
    Write-Host "ERROR: Build output not found. Run build.ps1 first." -ForegroundColor Red
    exit 1
}
$SourceDir = Join-Path $OutputDir $TargetFramework

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " RunLLM Plugin Deploy Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Source: $SourceDir"
Write-Host "Target: $PluginDest"
Write-Host ""

# Stop PowerToys
Write-Host "[1/3] Stopping PowerToys..." -ForegroundColor Yellow
$ptProcesses = Get-Process -Name "PowerToys*" -ErrorAction SilentlyContinue
if ($ptProcesses) {
    Stop-Process -Name "PowerToys*" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "      PowerToys stopped." -ForegroundColor Green
}
else {
    Write-Host "      PowerToys not running." -ForegroundColor Gray
}

# Copy files
Write-Host "[2/3] Copying plugin files..." -ForegroundColor Yellow
if (Test-Path $PluginDest) {
    Remove-Item $PluginDest -Recurse -Force
}
New-Item -ItemType Directory -Path $PluginDest -Force | Out-Null
Copy-Item -Path "$SourceDir\*" -Destination $PluginDest -Recurse -Force
Write-Host "      Files copied successfully." -ForegroundColor Green

# Restart PowerToys
if (-not $NoRestart) {
    Write-Host "[3/3] Starting PowerToys..." -ForegroundColor Yellow
    $ptPath = "C:\Program Files\PowerToys\PowerToys.exe"
    if (-not (Test-Path $ptPath)) {
        $ptPath = Join-Path $env:LOCALAPPDATA "PowerToys\PowerToys.exe"
    }
    if (Test-Path $ptPath) {
        Start-Process $ptPath
        Write-Host "      PowerToys started." -ForegroundColor Green
    }
    else {
        Write-Host "      WARNING: PowerToys.exe not found." -ForegroundColor Yellow
    }
}
else {
    Write-Host "[3/3] Skipping PowerToys restart (NoRestart flag)." -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " DEPLOY SUCCESSFUL" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
exit 0
