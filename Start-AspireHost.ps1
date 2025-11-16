#!/usr/bin/env pwsh
# Start Aspire Host for Phi-4 Weather Agent
# Ensures prerequisites are running and launches the application

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Starting Phi-4 Weather Agent" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# 1. Check Docker Desktop
# ============================================
Write-Host "[1/3] Verifying Docker Desktop..." -ForegroundColor Yellow

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "  ERROR Docker Desktop not installed" -ForegroundColor Red
    Write-Host "  Run: .\Setup-Environment.ps1" -ForegroundColor Yellow
    exit 1
}

try {
    docker ps | Out-Null 2>&1
    Write-Host "  OK Docker is running" -ForegroundColor Green
} catch {
    Write-Host "  ERROR Docker Desktop is not running" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Please start Docker Desktop:" -ForegroundColor Yellow
    Write-Host "    1. Launch Docker Desktop from Start Menu" -ForegroundColor White
    Write-Host "    2. Wait for 'Docker Desktop is running' in system tray" -ForegroundColor White
    Write-Host "    3. Run this script again" -ForegroundColor White
    Write-Host ""
    Write-Host "  If Docker fails to start, check Hyper-V:" -ForegroundColor Yellow
    Write-Host "    - Task Manager - Performance - CPU - Virtualization: Enabled" -ForegroundColor White
    Write-Host "    - Run: .\Setup-Environment.ps1 (as Administrator)" -ForegroundColor White
    Write-Host ""
    exit 1
}

# ============================================
# 2. Start Foundry Local Service
# ============================================
Write-Host ""
Write-Host "[2/3] Starting Foundry Local service..." -ForegroundColor Yellow

if (-not (Get-Command foundry -ErrorAction SilentlyContinue)) {
    Write-Host "  ERROR Foundry Local not installed" -ForegroundColor Red
    Write-Host "  Run: .\Setup-Environment.ps1" -ForegroundColor Yellow
    exit 1
}

# Start Foundry service (idempotent - safe if already running)
foundry service start 2>&1 | Out-Null

# Get service status
$foundryStatus = (foundry service status 2>&1) -join ' '
if ($foundryStatus -match "http://[^:]+:(\d+)") {
    $foundryPort = $Matches[1]
    $env:FOUNDRY_PORT = $foundryPort
    Write-Host "  OK Foundry service running on port $foundryPort" -ForegroundColor Green
} else {
    # Fallback to default port
    $env:FOUNDRY_PORT = "62859"
    Write-Host "  OK Foundry service started (using default port)" -ForegroundColor Green
}

# ============================================
# 3. Launch Aspire AppHost
# ============================================
Write-Host ""
Write-Host "[3/3] Launching Aspire AppHost..." -ForegroundColor Yellow
Write-Host "  Foundry Port: $($env:FOUNDRY_PORT)" -ForegroundColor Gray

# Set environment variable for HTTP profile
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Dashboard: http://localhost:15000" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to AppHost and run
Push-Location src\Phi4WeatherAgent.AppHost
try {
    dotnet run --launch-profile http
} catch {
    Write-Host ""
    Write-Host "ERROR Application failed to start: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Check if Docker Desktop is running: docker ps" -ForegroundColor White
    Write-Host "  2. Verify Hyper-V enabled: Task Manager - Performance - CPU" -ForegroundColor White
    Write-Host "  3. Review README.md Troubleshooting section" -ForegroundColor White
    Write-Host ""
} finally {
    Pop-Location
}
