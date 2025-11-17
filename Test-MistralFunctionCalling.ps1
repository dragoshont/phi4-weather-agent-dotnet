#!/usr/bin/env pwsh
# Test Mistral-7B Function Calling Support
# This script tests if Mistral-7B properly invokes functions using OpenAI format

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Testing Mistral-7B Function Calling" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Foundry service is running
Write-Host "[1/4] Checking Foundry Local service..." -ForegroundColor Yellow
$foundryStatus = (foundry service status 2>&1) -join ' '
if ($foundryStatus -match "http://[^:]+:(\d+)") {
    $foundryPort = $Matches[1]
    Write-Host "  ✅ Foundry running on port $foundryPort" -ForegroundColor Green
} else {
    Write-Host "  ❌ Foundry not running. Starting..." -ForegroundColor Yellow
    foundry service start
    Start-Sleep -Seconds 3
    $foundryStatus = (foundry service status 2>&1) -join ' '
    if ($foundryStatus -match "http://[^:]+:(\d+)") {
        $foundryPort = $Matches[1]
        Write-Host "  ✅ Foundry started on port $foundryPort" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Failed to start Foundry" -ForegroundColor Red
        exit 1
    }
}

# Check if Mistral model is downloaded
Write-Host ""
Write-Host "[2/4] Checking Mistral-7B model..." -ForegroundColor Yellow
$mistralModel = foundry cache list 2>$null | Select-String "mistral-7b-v0.2"
if ($mistralModel) {
    Write-Host "  ✅ Mistral-7B model found in cache" -ForegroundColor Green
} else {
    Write-Host "  ❌ Mistral-7B not downloaded" -ForegroundColor Red
    Write-Host "  Run: foundry model download mistral-7b-v0.2" -ForegroundColor Yellow
    exit 1
}

# Load the model
Write-Host ""
Write-Host "[3/4] Loading Mistral-7B model..." -ForegroundColor Yellow
Write-Host "  This may take 10-30 seconds..." -ForegroundColor Gray
foundry model load mistral-7b-v0.2 2>&1 | Out-Null
Start-Sleep -Seconds 5
Write-Host "  ✅ Model loaded" -ForegroundColor Green

# Test function calling with OpenAI-compatible API
Write-Host ""
Write-Host "[4/4] Testing function calling..." -ForegroundColor Yellow
Write-Host ""

$endpoint = "http://localhost:$foundryPort/v1/chat/completions"
$modelId = "mistralai-Mistral-7B-Instruct-v0-2-generic-cpu:2"

# Define a simple weather tool
$tools = @(
    @{
        type = "function"
        function = @{
            name = "get_weather"
            description = "Get the current weather for a location"
            parameters = @{
                type = "object"
                properties = @{
                    location = @{
                        type = "string"
                        description = "The city name, e.g. 'Seattle'"
                    }
                    unit = @{
                        type = "string"
                        enum = @("celsius", "fahrenheit")
                        description = "Temperature unit"
                    }
                }
                required = @("location")
            }
        }
    }
)

$requestBody = @{
    model = $modelId
    messages = @(
        @{
            role = "user"
            content = "What's the weather in Seattle?"
        }
    )
    tools = $tools
    tool_choice = "auto"
    stream = $false
    max_tokens = 500
} | ConvertTo-Json -Depth 10

Write-Host "Request:" -ForegroundColor Cyan
Write-Host "  Endpoint: $endpoint" -ForegroundColor Gray
Write-Host "  Model: $modelId" -ForegroundColor Gray
Write-Host "  User: 'What's the weather in Seattle?'" -ForegroundColor Gray
Write-Host "  Tools: get_weather(location, unit)" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $requestBody -ContentType "application/json"
    
    Write-Host "Response:" -ForegroundColor Cyan
    
    if ($response.choices -and $response.choices.Count -gt 0) {
        $choice = $response.choices[0]
        
        # Check if model wants to call a function
        if ($choice.message.tool_calls) {
            Write-Host "  ✅ SUCCESS! Model requested function call:" -ForegroundColor Green
            Write-Host ""
            foreach ($toolCall in $choice.message.tool_calls) {
                Write-Host "  Function: $($toolCall.function.name)" -ForegroundColor Cyan
                Write-Host "  Arguments: $($toolCall.function.arguments)" -ForegroundColor Cyan
                Write-Host "  ID: $($toolCall.id)" -ForegroundColor Gray
            }
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Green
            Write-Host "  ✅ Mistral-7B SUPPORTS function calling!" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Green
            exit 0
        } else {
            Write-Host "  ⚠️ WARNING: No tool_calls in response" -ForegroundColor Yellow
            Write-Host "  Content: $($choice.message.content)" -ForegroundColor Gray
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Yellow
            Write-Host "  ❌ Mistral-7B may NOT support function calling" -ForegroundColor Red
            Write-Host "========================================" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "  ❌ ERROR: No choices in response" -ForegroundColor Red
        Write-Host "  Response: $($response | ConvertTo-Json -Depth 5)" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "  ❌ ERROR: Request failed" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "  Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
    exit 1
}
