#!/usr/bin/env pwsh
#Requires -RunAsAdministrator
# Complete Setup Script for Phi-4 Weather Agent on Windows
# This script checks, installs, and configures all prerequisites
# Safe to run multiple times (idempotent)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Phi-4 Weather Agent - Complete Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$needsRestart = $false
$allGood = $true

# ============================================
# 1. Hyper-V Virtualization
# ============================================
Write-Host "[1/6] Checking Hyper-V Virtualization..." -ForegroundColor Yellow

try {
    $hyperV = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction Stop
    
    if ($hyperV.State -eq "Enabled") {
        Write-Host "  OK Hyper-V is enabled" -ForegroundColor Green
    } else {
        Write-Host "  WARNING Hyper-V is not enabled (required for Docker and Aspire)" -ForegroundColor Yellow
        Write-Host ""
        $response = Read-Host "  Enable Hyper-V now? This requires a restart (Y/N)"
        
        if ($response -eq "Y" -or $response -eq "y") {
            Write-Host "  Enabling Hyper-V..." -ForegroundColor Yellow
            Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All -NoRestart | Out-Null
            bcdedit /set hypervisorlaunchtype auto | Out-Null
            Write-Host "  OK Hyper-V enabled" -ForegroundColor Green
            $needsRestart = $true
        } else {
            Write-Host "  SKIPPED Manual enable: Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All" -ForegroundColor Gray
            $allGood = $false
        }
    }
    
    # Check CPU virtualization support
    $cpu = Get-CimInstance -ClassName Win32_Processor
    if ($cpu.VirtualizationFirmwareEnabled) {
        Write-Host "  OK CPU Virtualization enabled in BIOS" -ForegroundColor Green
    } else {
        Write-Host "  WARNING CPU Virtualization disabled in BIOS" -ForegroundColor Yellow
        Write-Host "  Enable Intel VT-x or AMD-V in BIOS settings" -ForegroundColor Gray
        $allGood = $false
    }
} catch {
    Write-Host "  ERROR Could not check Hyper-V: $($_.Exception.Message)" -ForegroundColor Red
    $allGood = $false
}

# ============================================
# 2. .NET 10 SDK
# ============================================
Write-Host ""
Write-Host "[2/6] Checking .NET 10 SDK..." -ForegroundColor Yellow

$dotnetVersion = dotnet --version 2>$null
if ($dotnetVersion -like "10.*") {
    Write-Host "  OK Installed: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "  MISSING .NET 10 SDK not found" -ForegroundColor Red
    Write-Host "  Installing via winget..." -ForegroundColor Yellow
    
    try {
        winget install Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements --silent
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  OK .NET 10 SDK installed" -ForegroundColor Green
        } else {
            Write-Host "  ERROR Installation failed" -ForegroundColor Red
            Write-Host "  Manual install: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
            $allGood = $false
        }
    } catch {
        Write-Host "  ERROR $($_.Exception.Message)" -ForegroundColor Red
        $allGood = $false
    }
}

# ============================================
# 3. Docker Desktop
# ============================================
Write-Host ""
Write-Host "[3/6] Checking Docker Desktop..." -ForegroundColor Yellow

if (Get-Command docker -ErrorAction SilentlyContinue) {
    try {
        docker ps | Out-Null 2>&1
        Write-Host "  OK Docker Desktop installed and running" -ForegroundColor Green
    } catch {
        Write-Host "  WARNING Docker installed but not running" -ForegroundColor Yellow
        Write-Host "  Start Docker Desktop from Start Menu" -ForegroundColor Gray
        $allGood = $false
    }
} else {
    Write-Host "  MISSING Docker Desktop not found" -ForegroundColor Red
    Write-Host "  Installing via winget (this may take 5-10 minutes)..." -ForegroundColor Yellow
    
    try {
        winget install Docker.DockerDesktop --accept-source-agreements --accept-package-agreements --silent
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  OK Docker Desktop installed" -ForegroundColor Green
            Write-Host "  Please start Docker Desktop from Start Menu" -ForegroundColor Cyan
            $allGood = $false
        } else {
            Write-Host "  ERROR Installation failed" -ForegroundColor Red
            Write-Host "  Manual install: winget install Docker.DockerDesktop" -ForegroundColor Yellow
            $allGood = $false
        }
    } catch {
        Write-Host "  ERROR $($_.Exception.Message)" -ForegroundColor Red
        $allGood = $false
    }
}

# ============================================
# 4. Foundry Local
# ============================================
Write-Host ""
Write-Host "[4/6] Checking Foundry Local..." -ForegroundColor Yellow

if (Get-Command foundry -ErrorAction SilentlyContinue) {
    $foundryVersion = foundry --version 2>$null
    Write-Host "  OK Installed: $foundryVersion" -ForegroundColor Green
} else {
    Write-Host "  MISSING Foundry Local not found" -ForegroundColor Red
    Write-Host "  Installing via winget..." -ForegroundColor Yellow
    
    try {
        winget install Microsoft.FoundryLocal --accept-source-agreements --accept-package-agreements --silent
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  OK Foundry Local installed" -ForegroundColor Green
            Write-Host "  You may need to restart terminal for 'foundry' command" -ForegroundColor Cyan
        } else {
            Write-Host "  ERROR Installation failed" -ForegroundColor Red
            Write-Host "  Manual install: winget install Microsoft.FoundryLocal" -ForegroundColor Yellow
            $allGood = $false
        }
    } catch {
        Write-Host "  ERROR $($_.Exception.Message)" -ForegroundColor Red
        $allGood = $false
    }
}

# ============================================
# 5. Phi-4 Mini Model
# ============================================
Write-Host ""
Write-Host "[5/6] Checking Phi-4 Mini Model..." -ForegroundColor Yellow

if (Get-Command foundry -ErrorAction SilentlyContinue) {
    $phi4Cached = foundry cache list 2>$null | Select-String "phi-4-mini"
    if ($phi4Cached) {
        Write-Host "  OK Phi-4 Mini model cached" -ForegroundColor Green
    } else {
        Write-Host "  MISSING Phi-4 Mini model not downloaded" -ForegroundColor Yellow
        Write-Host "  Downloading (~3.8GB, optimized ONNX format)..." -ForegroundColor Yellow
        Write-Host "  This may take 3-8 minutes depending on connection speed" -ForegroundColor Gray
        Write-Host "" 
        
        try {
            foundry model download phi-4-mini
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  OK Phi-4 Mini model downloaded" -ForegroundColor Green
            } else {
                Write-Host "  ERROR Download failed" -ForegroundColor Red
                Write-Host "  Retry: foundry model download phi-4-mini" -ForegroundColor Yellow
                $allGood = $false
            }
        } catch {
            Write-Host "  ERROR $($_.Exception.Message)" -ForegroundColor Red
            $allGood = $false
        }
    }
} else {
    Write-Host "  SKIPPED Foundry not available" -ForegroundColor Gray
}

# ============================================
# 6. Developer Certificates
# ============================================
Write-Host ""
Write-Host "[6/6] Checking Developer Certificates..." -ForegroundColor Yellow

$certOutput = dotnet dev-certs https --check 2>&1
if ($certOutput -match "valid certificate found" -or $certOutput -match "A valid HTTPS certificate is already present") {
    Write-Host "  OK HTTPS certificate trusted" -ForegroundColor Green
} else {
    Write-Host "  WARNING Certificate not trusted" -ForegroundColor Yellow
    Write-Host "  Trusting certificate..." -ForegroundColor Yellow
    
    try {
        dotnet dev-certs https --clean | Out-Null
        dotnet dev-certs https --trust
        Write-Host "  OK Certificate trusted" -ForegroundColor Green
        Write-Host "  Close all browser windows for changes to take effect" -ForegroundColor Cyan
    } catch {
        Write-Host "  ERROR $($_.Exception.Message)" -ForegroundColor Red
    }
}

# ============================================
# Summary
# ============================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

if ($needsRestart) {
    Write-Host "RESTART REQUIRED" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Hyper-V has been enabled. You must restart your computer." -ForegroundColor Yellow
    Write-Host "After restart, run this script again to complete setup." -ForegroundColor Cyan
    Write-Host ""
    $response = Read-Host "Restart now? (Y/N)"
    if ($response -eq "Y" -or $response -eq "y") {
        Write-Host "Restarting in 5 seconds..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5
        Restart-Computer -Force
    }
} elseif ($allGood) {
    Write-Host "SUCCESS - All prerequisites ready!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Ensure Docker Desktop is running" -ForegroundColor White
    Write-Host "  2. Run: .\Start-AspireHost.ps1" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "INCOMPLETE - Some prerequisites need attention" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Review the warnings above and:" -ForegroundColor Yellow
    Write-Host "  1. Fix any issues" -ForegroundColor White
    Write-Host "  2. Run this script again" -ForegroundColor White
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
