# Clean Script for RunLLM Plugin
# Usage: .\clean.ps1 [-All]

param(
    [switch]$All  # Also remove installed plugin from PowerToys
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SrcDir = Join-Path $ProjectRoot "src"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " RunLLM Plugin Clean Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean build artifacts
Write-Host "[1/2] Cleaning build artifacts..." -ForegroundColor Yellow
$binDir = Join-Path $SrcDir "bin"
$objDir = Join-Path $SrcDir "obj"

if (Test-Path $binDir) {
    Remove-Item $binDir -Recurse -Force
    Write-Host "      Removed: bin/" -ForegroundColor Green
}
if (Test-Path $objDir) {
    Remove-Item $objDir -Recurse -Force
    Write-Host "      Removed: obj/" -ForegroundColor Green
}

# Clean installed plugin
if ($All) {
    Write-Host "[2/2] Removing installed plugin..." -ForegroundColor Yellow
    $PluginDest = Join-Path $env:LOCALAPPDATA "Microsoft\PowerToys\PowerToys Run\Plugins\RunLLM"
    if (Test-Path $PluginDest) {
        # Stop PowerToys first
        Stop-Process -Name "PowerToys*" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
        Remove-Item $PluginDest -Recurse -Force
        Write-Host "      Removed installed plugin." -ForegroundColor Green
    }
    else {
        Write-Host "      Plugin not installed." -ForegroundColor Gray
    }
}
else {
    Write-Host "[2/2] Skipping installed plugin (use -All to remove)." -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " CLEAN COMPLETE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
exit 0
