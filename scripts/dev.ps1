# Development Script for RunLLM Plugin
# All-in-one: Clean -> Build -> Deploy -> Test
# Usage: .\dev.ps1 [-Configuration Release|Debug] [-Platform x64|ARM64] [-SkipClean]

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",
    
    [switch]$SkipClean
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host " RunLLM Plugin - Development Workflow" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# Step 1: Clean (optional)
if (-not $SkipClean) {
    Write-Host ">>> STEP 1: CLEAN <<<" -ForegroundColor Cyan
    & "$ScriptDir\clean.ps1"
    if ($LASTEXITCODE -ne 0) { exit 1 }
} else {
    Write-Host ">>> STEP 1: CLEAN (Skipped) <<<" -ForegroundColor Gray
}

# Step 2: Build
Write-Host ">>> STEP 2: BUILD <<<" -ForegroundColor Cyan
& "$ScriptDir\build.ps1" -Configuration $Configuration -Platform $Platform
if ($LASTEXITCODE -ne 0) { exit 1 }

# Step 3: Deploy
Write-Host ">>> STEP 3: DEPLOY <<<" -ForegroundColor Cyan
& "$ScriptDir\deploy.ps1" -Configuration $Configuration -Platform $Platform
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host " DEVELOPMENT WORKFLOW COMPLETE" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "Press Alt+Space to open PowerToys Run and test the plugin!" -ForegroundColor Yellow
Write-Host "Try: runllm Hello, how are you?" -ForegroundColor Yellow
Write-Host ""
