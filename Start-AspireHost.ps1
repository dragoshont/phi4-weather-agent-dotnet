#!/usr/bin/env pwsh
# Start Aspire Host for Local AI Agent
# Ensures prerequisites are running and launches the application

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Starting Local AI Agent" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# 0. Clean up any running LocalAIAgent processes
# ============================================
$processesToStop = Get-Process -Name "LocalAIAgent.Agent","LocalAIAgent.Web","LocalAIAgent.AppHost","dcpctrl","dcp" -ErrorAction SilentlyContinue
if ($processesToStop) {
    Write-Host "Stopping existing LocalAIAgent processes..." -ForegroundColor Yellow
    $processesToStop | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "  OK All processes stopped" -ForegroundColor Green
}

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
# 3. Validate Model Configuration
# ============================================
Write-Host ""
Write-Host "[3/4] Validating model configuration..." -ForegroundColor Yellow

$configPath = "src\LocalAIAgent.Web\appsettings.json"
if (-not (Test-Path $configPath)) {
    Write-Host "  ERROR Configuration file not found: $configPath" -ForegroundColor Red
    exit 1
}

try {
    $config = Get-Content $configPath -Raw | ConvertFrom-Json

    # Check if AI section exists
    if (-not $config.AI) {
        Write-Host "  ERROR 'AI' section missing in appsettings.json" -ForegroundColor Red
        Write-Host "  See README.md for configuration format" -ForegroundColor Yellow
        exit 1
    }

    # Check DefaultModel
    $defaultModel = $config.AI.DefaultModel
    if (-not $defaultModel) {
        Write-Host "  ERROR 'AI:DefaultModel' not set in appsettings.json" -ForegroundColor Red
        exit 1
    }

    Write-Host "  Default model: $defaultModel" -ForegroundColor Cyan

    # Find model configuration
    $modelConfig = $null
    foreach ($model in $config.AI.Models) {
        if ($model.Name -eq $defaultModel) {
            $modelConfig = $model
            break
        }
    }

    if (-not $modelConfig) {
        Write-Host "  ERROR Model '$defaultModel' not found in AI:Models array" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Available models:" -ForegroundColor Yellow
        foreach ($m in $config.AI.Models) {
            Write-Host "    - $($m.Name) ($($m.Provider))" -ForegroundColor White
        }
        Write-Host ""
        exit 1
    }

    # Validate model availability based on provider
    $provider = $modelConfig.Provider
    $modelId = $modelConfig.ModelId

    Write-Host "  Provider: $provider" -ForegroundColor Cyan
    Write-Host "  Model ID: $modelId" -ForegroundColor Cyan

    switch ($provider) {
        "Ollama" {
            if (Get-Command ollama -ErrorAction SilentlyContinue) {
                $ollamaModels = ollama list 2>$null
                if ($ollamaModels -match [regex]::Escape($modelId)) {
                    Write-Host "  OK Model available in Ollama" -ForegroundColor Green
                } else {
                    Write-Host "  ERROR Model not available in Ollama" -ForegroundColor Red
                    Write-Host "  Download: ollama pull $modelId" -ForegroundColor Yellow
                    Write-Host ""
                    Write-Host "  Available Ollama models:" -ForegroundColor Yellow
                    ollama list | Select-Object -Skip 1 | ForEach-Object {
                        if ($_ -match '^(\S+)') {
                            Write-Host "    - $($Matches[1])" -ForegroundColor White
                        }
                    }
                    Write-Host ""
                    exit 1
                }
            } else {
                Write-Host "  ERROR Ollama not installed (required for provider 'Ollama')" -ForegroundColor Red
                Write-Host "  Install: winget install Ollama.Ollama" -ForegroundColor Yellow
                exit 1
            }
        }

        "FoundryLocal" {
            if (Get-Command foundry -ErrorAction SilentlyContinue) {
                $foundryCache = foundry cache list 2>$null
                if ($foundryCache -match [regex]::Escape($modelId)) {
                    Write-Host "  OK Model cached in Foundry" -ForegroundColor Green
                } else {
                    Write-Host "  ERROR Model not cached in Foundry" -ForegroundColor Red
                    Write-Host "  Download: foundry model download phi-4-mini" -ForegroundColor Yellow
                    Write-Host ""
                    exit 1
                }
            } else {
                Write-Host "  ERROR Foundry not installed (required for provider 'FoundryLocal')" -ForegroundColor Red
                Write-Host "  Install: winget install Microsoft.FoundryLocal" -ForegroundColor Yellow
                exit 1
            }
        }

        "AzureOpenAI" {
            Write-Host "  INFO Azure OpenAI requires network connection and API key" -ForegroundColor Cyan
            if ($modelConfig.ApiKey -match '^\$\{.*\}$') {
                $envVar = $modelConfig.ApiKey -replace '^\$\{|\}$'
                if (-not (Test-Path "env:$envVar")) {
                    Write-Host "  WARNING Environment variable not set: $envVar" -ForegroundColor Yellow
                    Write-Host "  Set: `$env:$envVar = 'your-api-key'" -ForegroundColor Gray
                }
            }
        }

        "OpenAI" {
            Write-Host "  INFO OpenAI requires network connection and API key" -ForegroundColor Cyan
            if ($modelConfig.ApiKey -match '^\$\{.*\}$') {
                $envVar = $modelConfig.ApiKey -replace '^\$\{|\}$'
                if (-not (Test-Path "env:$envVar")) {
                    Write-Host "  WARNING Environment variable not set: $envVar" -ForegroundColor Yellow
                    Write-Host "  Set: `$env:$envVar = 'your-api-key'" -ForegroundColor Gray
                }
            }
        }

        "Gemini" {
            Write-Host "  INFO Google Gemini requires network connection and API key" -ForegroundColor Cyan
            if ($modelConfig.ApiKey -match '^\$\{.*\}$') {
                $envVar = $modelConfig.ApiKey -replace '^\$\{|\}$'
                if (-not (Test-Path "env:$envVar")) {
                    Write-Host "  WARNING Environment variable not set: $envVar" -ForegroundColor Yellow
                    Write-Host "  Set: `$env:$envVar = 'your-api-key'" -ForegroundColor Gray
                }
            }
        }

        default {
            Write-Host "  WARNING Unknown provider: $provider" -ForegroundColor Yellow
        }
    }

} catch {
    Write-Host "  ERROR Failed to validate configuration: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ============================================
# 4. Launch Aspire AppHost
# ============================================
Write-Host ""
Write-Host "[4/4] Launching Aspire AppHost..." -ForegroundColor Yellow
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
Push-Location src\LocalAIAgent.AppHost
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
