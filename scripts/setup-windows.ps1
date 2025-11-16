#!/usr/bin/env pwsh
# Windows Setup Script for Phi-4 Weather Agent
# Checks and installs .NET 10 SDK, Foundry Local, and Phi-4 model

Write-Host "=== Phi-4 Weather Agent - Windows Setup ===" -ForegroundColor Cyan
Write-Host "This script is idempotent - safe to run multiple times" -ForegroundColor Gray

# Check .NET 10 SDK
Write-Host "`nChecking .NET 10 SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($dotnetVersion -like "10.*") {
    Write-Host "✓ .NET 10 SDK found: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "✗ .NET 10 SDK not found. Please install from https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Red
    exit 1
}

# Note: Aspire workload is no longer needed (Aspire 13.0 uses NuGet packages)

# Check Foundry Local (standalone installation via winget)
Write-Host "`nChecking Foundry Local..." -ForegroundColor Yellow
if (Get-Command foundry -ErrorAction SilentlyContinue) {
    $foundryVersion = foundry --version 2>$null
    Write-Host "✓ Foundry Local installed: $foundryVersion" -ForegroundColor Green
} else {
    Write-Host "Installing Foundry Local via winget..." -ForegroundColor Yellow
    winget install Microsoft.FoundryLocal --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Failed to install Foundry Local" -ForegroundColor Red
        Write-Host "  Manual install: winget install Microsoft.FoundryLocal" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "✓ Foundry Local installed successfully" -ForegroundColor Green
    Write-Host "  Note: You may need to restart your terminal for 'foundry' command to be available" -ForegroundColor Cyan
}

# Check if Phi-4 Mini model exists
Write-Host "`nChecking Phi-4 Mini model..." -ForegroundColor Yellow
$phi4Cached = foundry cache list 2>$null | Select-String "phi4-mini"
if ($phi4Cached) {
    Write-Host "✓ Phi-4 Mini model found in cache" -ForegroundColor Green
} else {
    Write-Host "Downloading Phi-4 Mini model (~3.8GB, optimized ONNX format)..." -ForegroundColor Yellow
    Write-Host "This may take 3-8 minutes depending on connection speed" -ForegroundColor Cyan
    Write-Host "Note: Using hardware-optimized variant (CPU/GPU/NPU auto-detection)" -ForegroundColor Gray
    Write-Host "Please wait... (foundry will show download progress)" -ForegroundColor Gray
    Write-Host ""
    
    & foundry model download phi4-mini
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Phi-4 Mini model downloaded successfully" -ForegroundColor Green
    } else {
        Write-Host "✗ Phi-4 Mini model download failed. Check network connection and disk space." -ForegroundColor Red
        Write-Host "  Retry: foundry model download phi4-mini" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "`n=== Setup Complete ===" -ForegroundColor Cyan
Write-Host "✓ .NET 10 SDK: $dotnetVersion" -ForegroundColor Green
Write-Host "✓ Foundry Local: Installed" -ForegroundColor Green
Write-Host "✓ Phi-4 Mini model: Ready" -ForegroundColor Green
Write-Host "`nYou can now run: dotnet run --project src/Phi4WeatherAgent.AppHost" -ForegroundColor Green
Write-Host "Aspire Dashboard will be available at: http://localhost:15888" -ForegroundColor Cyan
