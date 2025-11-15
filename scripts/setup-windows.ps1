#!/usr/bin/env pwsh
# Windows Setup Script for Phi-4 Weather Agent
# Checks and installs .NET 10 SDK, Aspire workload, and Foundry Local

Write-Host "=== Phi-4 Weather Agent - Windows Setup ===" -ForegroundColor Cyan

# Check .NET 10 SDK
Write-Host "`nChecking .NET 10 SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($dotnetVersion -like "10.*") {
    Write-Host "✓ .NET 10 SDK found: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "✗ .NET 10 SDK not found. Please install from https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Red
    exit 1
}

# Check Aspire workload
Write-Host "`nChecking Aspire workload..." -ForegroundColor Yellow
$aspireInstalled = dotnet workload list | Select-String "aspire"
if ($aspireInstalled) {
    Write-Host "✓ Aspire workload installed" -ForegroundColor Green
} else {
    Write-Host "Installing Aspire workload..." -ForegroundColor Yellow
    dotnet workload install aspire
}

# Check Foundry Local (aspire-ai workload)
Write-Host "`nChecking Foundry Local (aspire-ai)..." -ForegroundColor Yellow
$foundryInstalled = dotnet workload list | Select-String "aspire-ai"
if ($foundryInstalled) {
    Write-Host "✓ Foundry Local (aspire-ai) workload installed" -ForegroundColor Green
} else {
    Write-Host "Installing Foundry Local workload..." -ForegroundColor Yellow
    dotnet workload install aspire-ai
}

Write-Host "`n=== Setup Complete ===" -ForegroundColor Cyan
Write-Host "You can now run: dotnet run --project src/Phi4WeatherAgent.AppHost" -ForegroundColor Green
