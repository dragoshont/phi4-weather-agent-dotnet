# Implementation Plan: Phi-4 Weather Assistant

**Branch**: `001-phi4-weather-assistant` | **Date**: 2025-11-16 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-phi4-weather-assistant/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build a local-first weather assistant using Phi-4 Mini model for natural language processing, Microsoft.Extensions.AI Agent Framework for orchestration, MCP tools for OpenMeteo API integration, and Blazor Server (from aichatweb template) for real-time chat UI. System runs entirely on-device (Windows/macOS/Linux) with zero cloud runtime costs, maintains WCAG 2.1 AA accessibility, and provides weather forecasts, allergen data, and multi-day planning through conversational interface.

## Technical Context

**Language/Version**: .NET 10 SDK (10.0.100+), C# 13  
**Primary Dependencies**: Microsoft.Extensions.AI (10.0.0-preview.1.25071.7+), Aspire 13.0.0-preview.1+, Polly 8.5+, Blazor Server  
**AI Model**: Phi-4 Mini (3.8GB, ONNX quantized) via Foundry Local (Windows/macOS) or Ollama (Linux)  
**Storage**: In-memory conversation context only (session-scoped), no persistent database  
**Testing**: xUnit 2.9.2+ (unit), bUnit 1.31.3+ (component), Playwright 1.49.0+ (E2E), BenchmarkDotNet 0.14.0+ (performance)  
**Target Platform**: Windows 11+, macOS Sonoma+, Ubuntu 22.04 LTS+ (cross-platform desktop)  
**Project Type**: Web application (Blazor Server + Agent Framework orchestration)  
**Performance Goals**: <5s end-to-end query (p95), <2s Agent Framework init, <100ms UI response, <500MB memory  
**Constraints**: Zero cloud costs, offline-capable AI inference, WCAG 2.1 AA compliance, no Semantic Kernel, no paid APIs  
**Scale/Scope**: Single-user desktop app, 4 user stories (basic weather, allergen info, multi-day planning, accessibility), ~15-20 components/services

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Local-First AI | ✅ PASS | Phi-4 Mini via Foundry Local/Ollama, no cloud AI |
| II. .NET 10 Requirement | ✅ PASS | Targets net10.0, SDK 10.0.100+, global.json pinned |
| III. Agent Framework Only | ✅ PASS | Uses Microsoft.Extensions.AI exclusively, Semantic Kernel forbidden |
| IV. Aspire 13 Orchestration | ✅ PASS | Aspire 13.0.0-preview.1+, AppHost for service discovery |
| V. Model Context Protocol | ✅ PASS | MCP tools for geocoding, weather, allergen data |
| VI. Zero Cloud Runtime Costs | ✅ PASS | OpenMeteo free APIs, local model hosting, no subscriptions |
| VII. WCAG 2.1 AA Accessibility | ✅ PASS | Keyboard navigation, screen readers, 4.5:1 contrast, SVG icons with aria-label |
| VIII. Template-Based Architecture | ✅ PASS | aichatweb template as base, customizes chat UI, removes vector store |
| IX. Comprehensive Testing | ✅ PASS | xUnit/bUnit/Playwright/BenchmarkDotNet, >80% coverage target |
| X. Cross-Platform Development | ✅ PASS | Setup scripts for Windows/macOS/Linux, CI matrix tests all platforms |
| XI. MIT License | ✅ PASS | MIT license for project code, permissive dependencies only |

**Overall**: ✅ **ALL GATES PASS** - Constitution fully compliant, proceed to Phase 0

## Project Structure

### Documentation (this feature)

```text
specs/001-phi4-weather-assistant/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── geocoding.json   # OpenMeteo Geocoding API contract
│   ├── weather.json     # OpenMeteo Weather API contract
│   └── allergen.json    # OpenMeteo Air Quality API contract
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Phi4WeatherAgent.Web/                  # Blazor Server UI (from aichatweb template)
│   ├── Components/
│   │   ├── Chat/                          # Keep from template
│   │   │   ├── ChatInput.razor
│   │   │   ├── ChatMessageList.razor
│   │   │   ├── ChatMessageItem.razor      # Customize for weather cards
│   │   │   ├── ChatHeader.razor
│   │   │   └── ChatSuggestions.razor
│   │   ├── WeatherCard.razor              # Add: Responsive weather display (SVG icons)
│   │   └── AllergenCard.razor             # Add: Pollen level display
│   ├── Services/
│   │   ├── ChatState.cs                   # In-memory conversation context
│   │   └── IChatClient integration        # Customize for Foundry Local/Ollama
│   ├── wwwroot/
│   │   ├── icons/                         # Add: SVG weather icons (sunny, cloudy, rainy, etc.)
│   │   └── css/                           # Customize: Responsive card layout, WCAG contrast
│   └── Program.cs                         # Configure services, SignalR, Aspire
│
├── Phi4WeatherAgent.Agent/                # Agent Framework + MCP tools
│   ├── Services/
│   │   └── AgentService.cs                # Agent Framework orchestration, in-memory context
│   ├── Tools/                             # MCP tool implementations
│   │   ├── GeocodingTool.cs               # Convert location names to coordinates
│   │   ├── WeatherTool.cs                 # Retrieve weather forecasts
│   │   └── AllergenTool.cs                # Retrieve pollen/allergen data
│   ├── HttpClients/
│   │   └── OpenMeteoClient.cs             # HTTP client with Polly retry (3x exponential backoff)
│   └── Models/
│       ├── Location.cs                    # lat/lon, name, country, state
│       ├── WeatherData.cs                 # Current + 7-day forecast
│       └── AllergenData.cs                # Pollen levels by category
│
├── Phi4WeatherAgent.AppHost/              # Aspire orchestration
│   ├── Program.cs                         # Platform detection, Foundry Local vs Ollama config
│   └── appsettings.json                   # Aspire Dashboard port (15888)
│
└── Phi4WeatherAgent.ServiceDefaults/      # Shared Aspire configuration
    └── Extensions.cs                      # Telemetry, resilience, service discovery

tests/
├── Phi4WeatherAgent.Agent.Tests/          # xUnit unit tests
│   ├── Tools/
│   │   ├── GeocodingToolTests.cs          # Mock HTTP responses, verify MCP tool contracts
│   │   ├── WeatherToolTests.cs
│   │   └── AllergenToolTests.cs
│   ├── Services/
│   │   └── AgentServiceTests.cs           # Verify Agent Framework orchestration
│   └── HttpClients/
│       └── OpenMeteoClientTests.cs        # Verify Polly retry behavior
│
├── Phi4WeatherAgent.Web.Tests/            # bUnit component tests
│   ├── WeatherCardTests.cs                # Verify responsive layout, SVG rendering, aria-label
│   ├── AllergenCardTests.cs               # Verify pollen severity indicators
│   └── ChatMessageItemTests.cs            # Verify weather card integration
│
└── Phi4WeatherAgent.E2E.Tests/            # Playwright E2E tests
    ├── WeatherQueryTests.cs               # User Story 1: "What's the weather in Seattle?"
    ├── AllergenQueryTests.cs              # User Story 2: "What are the pollen levels?"
    ├── MultiDayPlanningTests.cs           # User Story 3: "Plan my weekend in Denver"
    └── AccessibilityTests.cs              # User Story 4: Keyboard nav + screen reader
```

**Structure Decision**: Web application with 4 projects (Web UI, Agent logic, Aspire orchestration, shared defaults). Follows aichatweb template structure but removes vector store/ingestion/semantic search. Agent project separates MCP tool logic from UI for testability. Tests mirror source structure (unit for Agent, component for Web, E2E for full workflows).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

**N/A** - All 11 constitution principles pass. No violations requiring justification.
