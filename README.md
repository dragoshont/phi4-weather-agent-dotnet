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

- **.NET 10 SDK** (10.0.100+) - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Foundry Local** (Windows/macOS) OR **Ollama** (Linux) - [Foundry Docs](https://foundry.ms) | [Ollama Docs](https://ollama.com)
- **Git** - [Download](https://git-scm.com/downloads)

### Setup (5 minutes)

```bash
# Clone repository
git clone https://github.com/dragoshont/phi4-weather-agent-dotnet.git
cd phi4-weather-agent-dotnet
git checkout 001-phi-weather-assistant

# Run platform-specific setup
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

**Expected Output**:
- Aspire Dashboard: `http://localhost:15000`
- Web UI: `http://localhost:5000` (or random port shown in console)

---

## Example Queries

Try asking these in the chat interface:

```text
"What's the weather in Seattle?"
"Will it rain tomorrow in Portland?"
"What are the pollen levels in Paris?"
"Plan my weekend in Denver"
"Is it a good day for a picnic?"
```

See [quickstart.md](specs/001-phi4-weather-assistant/quickstart.md) for more natural language examples.

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

### "Phi-4 model not found"

**Windows/macOS**:
```powershell
# Install Foundry Local from https://foundry.ms/install
```

**Linux**:
```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull Phi-4 model
ollama pull phi4
```

### "Pollen data returns null"

**Expected behavior**: Pollen data is **Europe only** (CAMS European Air Quality Forecast).

For US locations, the agent explains the limitation and shows weather instead:
```text
"Pollen data is available for Europe only. Here's the weather for Seattle instead!"
```

### "Aspire Dashboard won't open"

Check DCP status:
```bash
dotnet aspire --version  # Should show 13.0.0-preview.1+
```

Dashboard auto-launches at `http://localhost:15000` (or random port if in use).

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

