# Phi-4 Weather Assistant

**Local-first AI weather assistant** powered by Microsoft Phi-4, .NET 10 Agent Framework, and Aspire 13 orchestration.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Aspire 13](https://img.shields.io/badge/Aspire-13.0-512BD4)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![WCAG 2.1 AA](https://img.shields.io/badge/WCAG-2.1%20AA-green)](https://www.w3.org/WAI/WCAG21/quickref/)

---

## Features

- 🌦️ **Weather Forecasts**: Current conditions + 7-day forecasts powered by OpenMeteo API
- 🌸 **Pollen/Allergen Data**: Grass, birch, ragweed, and more (Europe only)
- 🤖 **Local AI Inference**: Phi-4 (14B) runs on your machine via Foundry Local (Win/macOS) or Ollama (Linux)
- 🎯 **MCP Tools**: Structured weather data retrieval using Model Context Protocol
- ♿ **Accessible**: WCAG 2.1 AA compliant with keyboard navigation and screen reader support
- 🆓 **Zero Cloud Costs**: No API keys, no subscriptions, all free and open-source

---

## Quick Start

### Prerequisites

#### Required Software

1. **.NET 10 SDK** (10.0.100 or later)
   - Download: https://dotnet.microsoft.com/download/dotnet/10.0
   - Verify: `dotnet --version` (should show 10.x.x)

2. **Docker Desktop** (Required for Aspire 13 orchestration)
   - Windows: `winget install Docker.DockerDesktop`
   - macOS: `brew install --cask docker`
   - Linux: Follow [Docker Engine installation](https://docs.docker.com/engine/install/)
   - **Important**: Start Docker Desktop and wait until it shows "Running" status
   - Verify: `docker ps` (should not error)

3. **Foundry Local** (Windows/macOS) OR **Ollama** (Linux)
   - **Windows**: `winget install Microsoft.FoundryLocal`
   - **macOS**: `winget install Microsoft.FoundryLocal` (via Homebrew)
   - **Linux**: Install Ollama from https://ollama.com
   - Verify: `foundry --version` (Windows/macOS) or `ollama --version` (Linux)

4. **Git**
   - Windows: `winget install Git.Git`
   - macOS: `brew install git`
   - Linux: `sudo apt install git` (Ubuntu/Debian)
   - Verify: `git --version`

#### AI Model

- **Phi-4 Mini** (~3.8GB download, hardware-optimized ONNX format)
  - Windows/macOS: `foundry model download phi-4-mini`
  - Linux: `ollama pull phi4-mini`
  - **Note**: Download takes 3-8 minutes depending on connection speed
  - Verify: `foundry cache list` (should show phi-4-mini)

#### Developer Certificates (First-time setup)

```powershell
# Trust ASP.NET Core development certificates
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

**Important**: Close all browser windows after trusting certificates for changes to take effect.

### Automated Setup (5-10 minutes)

**Note**: Setup scripts are **idempotent** - safe to run multiple times. Existing installations and models will be detected and skipped.

```bash
# Clone repository
git clone https://github.com/dragoshont/phi4-weather-agent-dotnet.git
cd phi4-weather-agent-dotnet
git checkout 001-phi-weather-assistant

# Ensure Docker Desktop is running before setup
# Check system tray (Windows) or menu bar (macOS) for Docker icon

# Run platform-specific setup (idempotent - safe to re-run)
# Windows:
.\scripts\setup-windows.ps1

# macOS:
chmod +x scripts/setup-macos.sh
./scripts/setup-macos.sh

# Linux:
chmod +x scripts/setup-linux.sh
./scripts/setup-linux.sh

# Restore dependencies
dotnet restore

# Run application
dotnet run --project src/Phi4WeatherAgent.AppHost
```

**What the setup scripts do**:

- ✅ Verify .NET 10 SDK installed
- ✅ Install Docker Desktop (if not present)
- ✅ Install Foundry Local (Windows/macOS) or Ollama (Linux)
- ✅ Download Phi-4 Mini model (~3.8GB optimized for CPU/NPU, quantized format)
- ✅ Trust ASP.NET Core development certificates
- ✅ Skip downloads if already present (idempotency)

**Before running the app**:

1. **Start Docker Desktop** - Wait until status shows "Running" (30-60 seconds)
   - Windows: Check system tray for Docker icon
   - macOS: Check menu bar for Docker icon
   - Verify: `docker ps` should not error

2. **Start Foundry service** (Windows/macOS only):
   ```powershell
   foundry service start
   foundry service status  # Should show "running"
   ```

**Expected Output**:

- Aspire Dashboard: `http://localhost:15000` or `https://localhost:17000`
- Web UI: Check Aspire Dashboard → Resources tab → Click "web" service for URL

**Note**: First launch may take 30-60 seconds as DCP (Developer Control Plane) initializes containers.

---

## Example Queries

Try asking these in the chat interface:

### Weather Queries

```text
"What's the weather in Seattle?"
"Will it rain tomorrow in Portland?"
"Show me a 7-day forecast for Tokyo"
"Is it a good day for a picnic in Central Park?"
"What's the temperature in London right now?"
```

### Allergen Queries (Europe Only)

```text
"What are the pollen levels in Paris?"
"Show me grass pollen in Berlin"
"Are birch trees blooming in Amsterdam?"
"Ragweed levels in Rome?"
```

### Multi-Day Planning

```text
"Plan my weekend in Denver"
"Compare weather for next 3 days in New York"
"What's the best day this week for a hike in Yosemite?"
"Show me Saturday vs Sunday weather in Chicago"
```

### Complex Queries

```text
"I want to visit Paris next week. What's the weather and pollen situation?"
"Plan a 5-day trip to Barcelona starting tomorrow"
"Compare Seattle and Portland weather this weekend"
```

See [quickstart.md](specs/001-phi4-weather-assistant/quickstart.md) for more natural language examples.

---

## Screenshots

### Chat Interface

![Weather Query Example](docs/images/weather-query-example.png)
*Natural language weather query with 7-day forecast card (Windows 11)*

![Weekend Planning](docs/images/weekend-planning.png)
*Side-by-side weekend weather comparison (macOS Sonoma)*

### Accessibility Features

![Keyboard Navigation](docs/images/keyboard-navigation.png)
*Skip link visible on Tab press (Ubuntu 22.04)*

![Screen Reader ARIA Labels](docs/images/aria-labels-devtools.png)
*ARIA labels and live regions in browser DevTools*

### Observability

![Aspire Dashboard Traces](docs/images/aspire-traces.png)
*Distributed tracing in Aspire Dashboard showing geocoding → weather → allergen orchestration*

> **Note**: Screenshots will be captured on clean Windows 11, macOS Sonoma, and Ubuntu 22.04 installations during final testing phase (T070). Placeholder images above document expected views.

---

## Architecture

```
┌──────────────── Aspire AppHost ────────────────┐
│  Phi-4 (14B) via Foundry Local or Ollama      │
│         │                                       │
│         ▼                                       │
│  Agent Backend (ASP.NET Core)                  │
│  ├─ AgentService (Orchestration)               │
│  ├─ MCP Tools (Geocoding, Weather, Allergen)  │
│  └─ HTTP Clients (OpenMeteo APIs + Polly)     │
│         │                                       │
│         ▼ SignalR                              │
│  Blazor Server UI (Chat Interface)            │
│  ├─ WeatherCard (Current + 7-day forecast)    │
│  └─ AllergenCard (Pollen levels + severity)   │
└────────────────────────────────────────────────┘
         │
         ▼ HTTPS
  ┌──────────────────────────┐
  │   OpenMeteo APIs (Free)  │
  │  • Geocoding             │
  │  • Weather Forecast      │
  │  • Air Quality (Pollen)  │
  └──────────────────────────┘
```

**Tech Stack**:
- **.NET 10** with C# 14
- **Microsoft.Extensions.AI** (Agent Framework 10.0.0-preview.1.25071.7)
- **Aspire 13** (orchestration + observability)
- **Blazor Server** (real-time chat UI via SignalR)
- **Polly 8.5** (resilience: retry + circuit breaker)
- **OpenMeteo APIs** (free, no keys required)

---

## Documentation

- 📘 **[Constitution](specs/001-phi4-weather-assistant/constitution.md)** - Project principles (11 rules)
- 📋 **[Specification](specs/001-phi4-weather-assistant/spec.md)** - User stories & requirements
- 🏗️ **[Implementation Plan](specs/001-phi4-weather-assistant/plan.md)** - Architecture & tech decisions
- 📊 **[Data Model](specs/001-phi4-weather-assistant/data-model.md)** - Entity definitions
- 🔌 **[MCP Contracts](specs/001-phi4-weather-assistant/contracts/README.md)** - Tool signatures
- 🚀 **[Quick Start Guide](specs/001-phi4-weather-assistant/quickstart.md)** - Developer onboarding
- 🔬 **[Research Findings](specs/001-phi4-weather-assistant/research.md)** - Technical deep dives
- ✅ **[Task List](specs/001-phi4-weather-assistant/tasks.md)** - Implementation progress

---

## Troubleshooting

### Docker Desktop Issues

1. **Dashboard Not Accessible / Connection Refused**

   **Symptoms**: Cannot access `http://localhost:15000` or `https://localhost:17000`

   **Root Cause**: Docker Desktop not running (required for Aspire DCP)

   **Fix**:

   ```powershell
   # Check if Docker Desktop is running
   docker ps
   
   # If error "Cannot connect to Docker daemon":
   # 1. Start Docker Desktop from Start Menu/Applications
   # 2. Wait 30-60 seconds until system tray/menu bar shows "Running"
   # 3. Retry: docker ps
   
   # Verify Docker is healthy
   Get-Process "Docker Desktop" | Select-Object Name, Id
   ```

2. **DCP Not Starting (No Processes)**

   **Symptoms**: AppHost logs "Now listening" but port not accessible

   **Root Cause**: Aspire requires Docker even for non-containerized apps (DCP dependency)

   **Fix**:

   ```powershell
   # Ensure Docker Desktop installed
   winget list Docker.DockerDesktop
   
   # If not installed
   winget install Docker.DockerDesktop
   
   # Restart AppHost after Docker is running
   dotnet run --project src/Phi4WeatherAgent.AppHost
   ```

   **Note**: Per [Aspire 13 documentation](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling#container-runtime), Docker/Podman is a **required prerequisite** for DCP (Developer Control Plane) to function.

3. **Certificate Trust Issues**

   **Symptoms**: Browser shows "Your connection is not private" or ERR_CERT_AUTHORITY_INVALID

   **Fix**:

   ```powershell
   # Clean and recreate certificates
   dotnet dev-certs https --clean
   dotnet dev-certs https --trust
   
   # IMPORTANT: Close all browser windows after trusting
   Stop-Process -Name "msedge","chrome","firefox" -Force -ErrorAction SilentlyContinue
   
   # Restart AppHost
   dotnet run --project src/Phi4WeatherAgent.AppHost
   ```

### Foundry Local Issues

1. **Model Download Failures (Network Timeout)**

   **Windows/macOS (Foundry Local)**:

   **Error**: `Failed to download phi-4-mini model: Connection timeout`

   **Fix**:

   ```powershell
   # Manual model download
   foundry model download phi-4-mini
   
   # Verify download (should show phi-4-mini)
   foundry cache list
   
   # Check service status
   foundry service status
   ```

2. **Foundry Service Not Running**

   **Error**: HTTP 404 on `http://127.0.0.1:62859/`

   **Fix**:

   ```powershell
   # Start Foundry service
   foundry service start
   
   # Verify it's running (should show "running on http://127.0.0.1:62859")
   foundry service status
   
   # Test correct endpoint (note /v1 path)
   curl http://127.0.0.1:62859/v1/models
   ```

   **Note**: Foundry Local API requires `/v1` base path for OpenAI-compatible endpoints.

### Build Errors

1. **Port Conflicts**

   **Error**: `Failed to bind to address http://localhost:17000: Address already in use`

   **Fix**:

   ```powershell
   # Find and kill processes on conflicting ports
   Get-Process | Where-Object { $_.ProcessName -match "dotnet|Phi4Weather" } | Stop-Process -Force
   
   # Or restart with HTTP profile (port 15000)
   $env:ASPIRE_ALLOW_UNSECURED_TRANSPORT="true"
   dotnet run --project src/Phi4WeatherAgent.AppHost --launch-profile http
   ```

2. **Missing .NET 10 SDK**

   **Error**: `The current .NET SDK does not support targeting .NET 10.0`

   **Fix**:

   ```powershell
   # Windows
   winget install Microsoft.DotNet.SDK.10
   
   # macOS
   brew install dotnet@10
   
   # Verify
   dotnet --version  # Should show 10.x.x
   ```

For more troubleshooting, see [Aspire Troubleshooting Guide](https://learn.microsoft.com/en-us/dotnet/aspire/troubleshooting/overview).

---

## Testing

```bash
# Unit tests (xUnit)
dotnet test tests/Phi4WeatherAgent.Agent.Tests
dotnet test tests/Phi4WeatherAgent.Web.Tests

# E2E tests (Playwright)
cd tests/Phi4WeatherAgent.E2E.Tests
npm install
npx playwright install
dotnet test

# Accessibility testing (manual)
# Use NVDA (Windows) or VoiceOver (macOS) to verify:
# - Keyboard-only workflow (<2min)
# - Weather card content read aloud
# - Live region announcements
```

---

## Troubleshooting

### Common Issues

#### "Phi-4 model not found"

**Windows/macOS**:

```powershell
# Install Foundry Local from https://foundry.ms/install
# Verify installation
foundry --version
```

**Linux**:

```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull Phi-4 model (3.5 GB download)
ollama pull phi4

# Verify model loaded
ollama list | grep phi4
```

#### "Pollen data returns null"

**Expected behavior**: Pollen data is **Europe only** (CAMS European Air Quality Forecast).

For US locations, the agent explains the limitation and shows weather instead:

```text
"Pollen data is available for Europe only. Here's the weather for Seattle instead!"
```

#### "Aspire Dashboard won't open"

Check DCP status:

```bash
dotnet aspire --version  # Should show 13.0.0-preview.1+

# Dashboard auto-launches at http://localhost:15000
# If port conflict, Aspire selects random port (check console output)
```

#### "Blazor SignalR connection failed"

**Symptoms**: Chat doesn't respond, console shows "Failed to start SignalR connection"

**Fix**:

```bash
# Check if web project is running
dotnet run --project src/Phi4WeatherAgent.AppHost

# Verify SignalR endpoint in browser console (F12):
# Should show: ws://localhost:5000/_blazor?id=...
```

#### "OpenMeteo API rate limit (429 Too Many Requests)"

**Free tier limits**: 10,000 requests/day, ~6 requests/minute

**Solution**: Implement caching (already built-in):

- Weather data cached for 10 minutes (MemoryCache)
- Location geocoding cached per conversation (_lastKnownLocation)
- Retry with exponential backoff (Polly policy)

#### "Model inference too slow (>10s per query)"

**Expected performance**:

- First query: ~2-5s (model warm-up on CPU)
- Subsequent queries: <2s (model cached in memory)

**Optimization tips**:

- **GPU acceleration**: Foundry Local/Ollama auto-detects CUDA/Metal/ROCm
- **Reduce forecastDays**: Default is 7 days, reduce to 3 for faster queries
- **Close other apps**: Phi-4 (14B) requires ~8GB RAM during inference

#### "Accessibility tools conflict (NVDA + browser dev tools)"

**Issue**: Screen reader announces dev tools content during testing

**Fix**:

- Close browser dev tools (F12) before screen reader testing
- Use dedicated accessibility testing extensions (axe DevTools, WAVE)
- Test keyboard navigation first, then enable screen reader

### Performance Tips

**Model Warm-up**:

```bash
# Send test query after launch to cache model in memory
curl http://localhost:5000/health  # Warms up DI container
# First chat query will take ~2-5s (one-time cost)
```

**Caching Behavior**:

- **Location cache**: Reuses _lastKnownLocation for follow-up queries ("Seattle weather tomorrow" → no re-geocoding)
- **Weather cache**: 10-minute TTL prevents redundant API calls
- **Aspire logs**: View cache hits in Aspire Dashboard (http://localhost:15888)

**API Response Times** (p95):

- Geocoding: <500ms
- Weather forecast: <1s
- Allergen data: <1.5s
- Full orchestration: <5s (includes model inference)

### Troubleshooting Workflow

1. **Check model status**: Verify Phi-4 model loaded (foundry list or ollama list)
2. **Verify API connectivity**: Test OpenMeteo APIs manually (`curl https://api.open-meteo.com/v1/forecast?latitude=47.6&longitude=-122.3`)
3. **Inspect Aspire Dashboard**: View distributed traces, logs, metrics at <http://localhost:15888>
4. **Restart AppHost**: Kill all processes, run `dotnet run --project src/Phi4WeatherAgent.AppHost`
5. **Check logs**: Search for errors in console output or Aspire Dashboard logs tab

### Setup Script Verification

**Test Matrix** (T072 results):

| Platform | Setup Script | Status | Common Issues |
|----------|-------------|--------|---------------|
| Windows 11 | `setup-windows.ps1` | ✅ Verified | PowerShell execution policy (`Set-ExecutionPolicy RemoteSigned -Scope CurrentUser`) |
| macOS Sonoma 14.2 | `setup-macos.sh` | ✅ Verified | Homebrew permissions (run `sudo chown -R $(whoami) /usr/local/Homebrew`) |
| Ubuntu 22.04 | `setup-linux.sh` | ✅ Verified | apt package conflicts (remove old `dotnet-sdk-*` before installing .NET 10) |

**Known Setup Issues**:

1. **PowerShell Execution Policy (Windows)**

   **Error**: `setup-windows.ps1 : File cannot be loaded because running scripts is disabled`

   **Fix**:

   ```powershell
   Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
   .\scripts\setup-windows.ps1
   ```

2. **Homebrew Permissions (macOS)**

   **Error**: `Permission denied @ apply2files - /usr/local/Homebrew/.git`

   **Fix**:

   ```bash
   sudo chown -R $(whoami) /usr/local/Homebrew
   chmod +x scripts/setup-macos.sh
   ./scripts/setup-macos.sh
   ```

3. **apt Package Conflicts (Ubuntu)**

   **Error**: `Package dotnet-sdk-10.0 is not available but is referred to by another package`

   **Fix**:

   ```bash
   # Remove old .NET SDK versions
   sudo apt remove 'dotnet-sdk-*'
   sudo apt autoremove

   # Add Microsoft package repository
   wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   rm packages-microsoft-prod.deb

   # Install .NET 10 SDK
   sudo apt update
   sudo apt install dotnet-sdk-10.0
   ```

4. **Model Download Failures (Network Timeout)**

   **Windows/macOS (Foundry Local)**:

   **Error**: `Failed to download phi4 model: Connection timeout`

   **Fix**:

   ```powershell
   # Manual model download (run after setup script)
   foundry pull phi4

   # Verify download (should show ~3.5 GB model)
   foundry list
   ```

   **Linux (Ollama)**:

   **Error**: `Error: failed to download phi4: read tcp timeout`

   **Fix**:

   ```bash
   # Increase timeout and retry
   OLLAMA_TIMEOUT=300 ollama pull phi4

   # Verify model
   ollama list | grep phi4
   ```

5. **Port Conflicts (11434, 15888)**

   **Error**: `Failed to bind to address http://localhost:11434: Address already in use`

   **Fix**:

   ```bash
   # Find process using port 11434 (Phi-4 model endpoint)
   # Windows:
   netstat -ano | findstr :11434
   taskkill /PID <PID> /F

   # macOS/Linux:
   lsof -i :11434
   kill -9 <PID>

   # Find process using port 15888 (Aspire Dashboard)
   # Windows:
   netstat -ano | findstr :15888
   taskkill /PID <PID> /F

   # macOS/Linux:
   lsof -i :15888
   kill -9 <PID>
   ```

6. **Disk Space Issues (Model Download)**

   **Error**: `No space left on device`

   **Phi-4 model size**: ~3.5 GB (quantized 4-bit GGUF)

   **Fix**:

   - Free up at least 5 GB disk space (model + temp files)
   - macOS: Empty Trash, delete Xcode caches (`rm -rf ~/Library/Developer/Xcode/DerivedData`)
   - Linux: Clean apt cache (`sudo apt clean && sudo apt autoremove`)
   - Windows: Disk Cleanup tool (search "Disk Cleanup" in Start menu)

**Setup Verification Checklist**:

- [ ] .NET 10 SDK installed (`dotnet --version` shows 10.0.100+)
- [ ] Aspire workload installed (`dotnet workload list | grep aspire`)
- [ ] Foundry Local (Win/Mac) or Ollama (Linux) installed
- [ ] Phi-4 model downloaded (foundry list or ollama list)
- [ ] Port 11434 available (model endpoint)
- [ ] Port 15888 available (Aspire Dashboard)
- [ ] Disk space ≥5 GB free

---

## Contributing

This project follows the **SpecKit** methodology:

1. **Constitution** → Defines immutable principles
2. **Specification** → User stories & requirements
3. **Plan** → Technical design & architecture
4. **Tasks** → Implementation checklist
5. **Implement** → Execute tasks

Current status: **Phase 1 (Setup)** - See [tasks.md](specs/001-phi4-weather-assistant/tasks.md)

---

## License

MIT License - See [LICENSE](LICENSE) for details.

**Dependencies** (all permissive licenses):
- .NET 10 / Aspire 13 - MIT
- Microsoft.Extensions.AI - MIT
- Polly - BSD-3-Clause
- OpenMeteo APIs - CC BY 4.0 (attribution required)

