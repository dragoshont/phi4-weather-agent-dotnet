#!/usr/bin/env pwsh
# Windows Setup Script for Local AI Agent
# Checks and installs .NET 10 SDK, Foundry Local, and AI models

Write-Host "=== Local AI Agent - Windows Setup ===" -ForegroundColor Cyan
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
$phi4Cached = foundry cache list 2>$null | Select-String "phi-4-mini"
if ($phi4Cached) {
    Write-Host "✓ Phi-4 Mini model found in cache" -ForegroundColor Green
} else {
    Write-Host "Downloading Phi-4 Mini model (~3.8GB, optimized ONNX format)..." -ForegroundColor Yellow
    Write-Host "This may take 3-8 minutes depending on connection speed" -ForegroundColor Cyan
    Write-Host "Note: Using hardware-optimized variant (CPU/GPU/NPU auto-detection)" -ForegroundColor Gray
    Write-Host "Please wait... (foundry will show download progress)" -ForegroundColor Gray
    Write-Host ""

    foundry model download phi-4-mini

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Phi-4 Mini model downloaded successfully" -ForegroundColor Green
    } else {
        Write-Host "✗ Phi-4 Mini model download failed. Check network connection and disk space." -ForegroundColor Red
        Write-Host "  Retry: foundry model download phi-4-mini" -ForegroundColor Yellow
        exit 1
    }
}

# Note: Qwen 2.5 VL 3B requires Ollama (Linux-only in current configuration)
# Windows users can use Phi-4 Mini via Foundry Local
Write-Host "`nNote: Qwen 2.5 VL 3B model available via Ollama on Linux" -ForegroundColor Cyan
Write-Host "Windows users: Phi-4 Mini via Foundry Local is recommended" -ForegroundColor Gray

# Check if Ollama is installed for Qwen model (alternative to Foundry)
Write-Host "`nChecking Ollama for Qwen model support..." -ForegroundColor Yellow
if (Get-Command ollama -ErrorAction SilentlyContinue) {
    Write-Host "✓ Ollama found" -ForegroundColor Green

    # Check if Qwen 2.5-VL model exists
    $qwenCached = ollama list 2>$null | Select-String "qwen2.5-vl:3b-instruct"
    if ($qwenCached) {
        Write-Host "✓ Qwen 2.5-VL 3B model found" -ForegroundColor Green
    } else {
        Write-Host "Downloading Qwen 2.5-VL 3B model (~2GB)..." -ForegroundColor Yellow
        Write-Host "This may take 2-5 minutes depending on connection speed" -ForegroundColor Cyan
        Write-Host "Note: Qwen supports vision capabilities and alternative model selection" -ForegroundColor Gray

        ollama pull qwen2.5-vl:3b-instruct

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Qwen 2.5-VL model downloaded successfully" -ForegroundColor Green
        } else {
            Write-Host "⚠ Qwen model download failed (optional - Phi-4 will work)" -ForegroundColor Yellow
            Write-Host "  Retry later: ollama pull qwen2.5-vl:3b-instruct" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "ℹ Ollama not installed (optional - only needed for Qwen model)" -ForegroundColor Cyan
    Write-Host "  Install: winget install Ollama.Ollama" -ForegroundColor Gray
}

Write-Host "`n=== Setup Complete ===" -ForegroundColor Cyan
Write-Host "✓ .NET 10 SDK: $dotnetVersion" -ForegroundColor Green
Write-Host "✓ Foundry Local: Installed" -ForegroundColor Green
Write-Host "✓ Phi-4 Mini model: Ready" -ForegroundColor Green
if (Get-Command ollama -ErrorAction SilentlyContinue) {
    Write-Host "✓ Qwen 2.5-VL model: Ready (optional)" -ForegroundColor Green
}
Write-Host "`nYou can now run: dotnet run --project src/LocalAIAgent.AppHost" -ForegroundColor Green
Write-Host "Aspire Dashboard will be available at: http://localhost:15888" -ForegroundColor Cyan
