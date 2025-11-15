# Implementation Plan: Phi-4 Weather Assistant

**Branch**: `001-phi4-weather-assistant` | **Date**: 2025-11-16 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-phi4-weather-assistant/spec.md`

## Summary

Build a local-first weather assistant powered by Microsoft Phi-4 model running locally (Foundry Local on Windows/macOS, Ollama on Linux). Uses .NET 10 Agent Framework for AI orchestration, Aspire 13 for service discovery/observability, and MCP tools for structured weather data retrieval from free OpenMeteo APIs. Blazor Server UI (from Microsoft aichatweb template) provides real-time chat interface with weather cards. Zero cloud costs, WCAG 2.1 AA accessible, cross-platform development support.

## Technical Context

**Language/Version**: C# 13 with .NET 10 SDK (10.0.100+)

**Primary Dependencies**:
- **Microsoft.Extensions.AI** 10.0.0-preview.1.25071.7+ (Agent Framework)
- **Aspire.Hosting.AppHost** 13.0.0-preview.1+ (orchestration)
- **Polly** 8.5.0+ (resilience policies)
- **Blazor Server** (from aichatweb template, targets net10.0)

**Storage**: N/A (stateless application, no persistence required)

**Testing**:
- **xUnit** 2.9.2+ for unit tests (Agent, MCP tools, HTTP clients)
- **bUnit** 1.31.3+ for Blazor component tests
- **Playwright** 1.49.0+ for E2E tests
- **BenchmarkDotNet** 0.14.0+ for performance benchmarks

**Target Platform**: Windows 11+, macOS Sonoma+, Ubuntu 22.04+ (cross-platform desktop)

**Project Type**: Aspire web application (Blazor Server frontend + ASP.NET Core backend + AppHost orchestration)

**Performance Goals**:
- Weather query end-to-end: <5 seconds (p95)
- Agent Framework initialization: <2 seconds
- UI responsiveness: <100ms for input handling
- Memory usage: <500MB (Phi-4 model + application overhead)

**Constraints**:
- **Local-only AI**: No cloud inference (Principle I)
- **Zero runtime costs**: Free APIs only (Principle VI)
- **WCAG 2.1 AA**: Accessibility mandatory (Principle VII)
- **Offline-capable**: Application functional without internet (local AI works, weather APIs fail gracefully)

**Scale/Scope**:
- Single-user desktop application
- ~2000 LOC (excluding template boilerplate)
- 4 user stories, 26 functional requirements, 12 success criteria
- 3 MCP tools (Geocoding, Weather, Allergen)
- ~15 Blazor components (template + custom weather cards)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Requirement | Status | Evidence |
|-----------|-------------|--------|----------|
| **I. Local-First AI** | Phi-4 inference runs locally via Foundry Local (Win/Mac) or Ollama (Linux) | ✅ PASS | FR-001: Platform-specific model hosting configured in AppHost |
| **II. .NET 10 Requirement** | .NET 10 SDK 10.0.100+ mandatory, no .NET 9 code | ✅ PASS | FR-003: Target framework net10.0, global.json pins SDK version |
| **III. Agent Framework Only** | Use Microsoft.Extensions.AI, forbid Semantic Kernel | ✅ PASS | FR-002: Agent Framework 10.0.0-preview.1.25071.7+, no Semantic Kernel in dependencies |
| **IV. Aspire 13 Orchestration** | Aspire 13.0.0-preview.1+ for orchestration | ✅ PASS | FR-004: Aspire 13 with Dashboard auto-launch (FR-016) |
| **V. Model Context Protocol** | MCP tools for weather data | ✅ PASS | FR-008: Geocoding, Weather, Allergen MCP tools with OpenMeteo APIs |
| **VI. Zero Cloud Runtime Costs** | No paid APIs or cloud services | ✅ PASS | FR-005/006/007: OpenMeteo free APIs, FR-023: No Azure OpenAI/OpenAI API |
| **VII. WCAG 2.1 AA Accessibility** | Keyboard navigation, screen readers, contrast ratios | ✅ PASS | FR-013: WCAG AA compliance, User Story 4 validates accessibility |
| **VIII. Template-Based Architecture** | Leverage aichatweb template with customizations | ✅ PASS | FR-014/015: Blazor Server from template, remove vector store/ingestion |
| **IX. Comprehensive Testing Coverage** | xUnit, bUnit, Playwright, >80% coverage | ✅ PASS | FR-019/020: Test frameworks specified, coverage target >80% for critical paths |
| **X. Cross-Platform Development** | Support Windows, macOS, Linux | ✅ PASS | FR-017/018: Platform-specific setup scripts, CI matrix requirement |
| **XI. MIT License** | MIT license, permissive dependencies only | ✅ PASS | FR-021: MIT license mandatory, dependency license scanning in CI |

**Verdict**: ✅ **ALL PRINCIPLES SATISFIED** - No constitution violations. Proceed to Phase 0 research.

## Project Structure

### Documentation (this feature)

```text
specs/001-phi4-weather-assistant/
├── constitution.md      # Project constitution (11 principles)
├── spec.md              # Feature specification (4 user stories, 26 FR)
├── plan.md              # This file (implementation plan)
├── research.md          # Phase 0 output (PENDING)
├── data-model.md        # Phase 1 output (PENDING)
├── quickstart.md        # Phase 1 output (PENDING)
├── contracts/           # Phase 1 output (PENDING)
└── tasks.md             # Phase 2 output (PENDING - created by /speckit.tasks)
```

### Source Code (repository root)

```text
phi4-weather-agent-dotnet/
├── .specify/            # SpecKit templates and scripts
├── scripts/             # Platform setup scripts
│   ├── setup-windows.ps1
│   ├── setup-macos.sh
│   └── setup-linux.sh
├── src/
│   ├── Phi4WeatherAgent.AppHost/           # Aspire AppHost (orchestration)
│   │   ├── Program.cs                       # Platform detection, Foundry Local/Ollama config
│   │   └── Phi4WeatherAgent.AppHost.csproj
│   ├── Phi4WeatherAgent.ServiceDefaults/   # Aspire shared configuration
│   │   ├── Extensions.cs                    # AddServiceDefaults, ConfigureOpenTelemetry
│   │   └── Phi4WeatherAgent.ServiceDefaults.csproj
│   ├── Phi4WeatherAgent.Agent/             # ASP.NET Core backend (MCP tools)
│   │   ├── Models/                          # Domain models
│   │   │   ├── Location.cs
│   │   │   ├── WeatherData.cs
│   │   │   └── AllergenData.cs
│   │   ├── Services/                        # HTTP clients with Polly retry
│   │   │   ├── OpenMeteoGeocodeClient.cs
│   │   │   ├── OpenMeteoWeatherClient.cs
│   │   │   └── OpenMeteoAllergenClient.cs
│   │   ├── Tools/                           # MCP tools
│   │   │   ├── GeocodeTool.cs
│   │   │   ├── WeatherTool.cs
│   │   │   └── AllergenTool.cs
│   │   ├── Program.cs
│   │   └── Phi4WeatherAgent.Agent.csproj
│   └── Phi4WeatherAgent.Web/               # Blazor Server UI (aichatweb template)
│       ├── Components/
│       │   ├── Chat/                        # Template components (keep)
│       │   │   ├── ChatInput.razor
│       │   │   ├── ChatMessageList.razor
│       │   │   ├── ChatMessageItem.razor  # ✨ CUSTOMIZE for weather cards
│       │   │   ├── ChatHeader.razor
│       │   │   └── ChatSuggestions.razor
│       │   ├── Weather/                     # ➕ NEW weather card components
│       │   │   ├── WeatherCard.razor
│       │   │   └── AllergenCard.razor
│       │   ├── Layout/
│       │   │   └── MainLayout.razor
│       │   ├── Pages/
│       │   │   ├── Chat.razor              # ✨ CUSTOMIZE system prompt
│       │   │   └── Error.razor
│       │   ├── App.razor
│       │   └── Routes.razor
│       ├── Services/                        # ➕ Agent orchestration
│       │   └── AgentService.cs              # Wraps Agent Framework + MCP tools
│       ├── wwwroot/
│       │   ├── css/
│       │   ├── js/
│       │   └── lib/                         # ❌ REMOVE pdfjs, markdown_viewer, dompurify
│       ├── Program.cs                       # ✨ CUSTOMIZE IChatClient provider
│       ├── appsettings.json
│       └── Phi4WeatherAgent.Web.csproj
├── tests/
│   ├── Phi4WeatherAgent.Agent.Tests/       # xUnit unit tests
│   │   ├── Models/
│   │   ├── Services/
│   │   └── Tools/
│   ├── Phi4WeatherAgent.Web.Tests/         # bUnit component tests
│   │   ├── Components/
│   │   └── Pages/
│   └── Phi4WeatherAgent.E2E.Tests/         # Playwright E2E tests
│       └── WeatherQueryTests.cs
├── global.json                              # .NET 10 SDK pin
├── Directory.Build.props                    # Centralized package versions
├── .editorconfig                            # C# code style
├── .gitignore
├── LICENSE                                  # MIT License
├── README.md
└── phi4-weather-agent-dotnet.sln
```

**Structure Decision**: Aspire web application (multi-project structure) chosen because:

1. **Separation of Concerns**: Agent backend (MCP tools) separate from Blazor UI enables independent testing and potential future API exposure
2. **Aspire Pattern**: AppHost + ServiceDefaults + App projects is standard Aspire 13 pattern for service orchestration
3. **Template Leverage**: Web project preserves aichatweb template structure (Components/, Pages/) with minimal modifications
4. **Test Organization**: Separate test projects for unit (Agent), component (Web), and E2E tests enables parallel execution in CI

**Template Modifications** (Principle VIII compliance):

- ✅ **Keep**: All Chat/ components, IChatClient integration, SignalR real-time messaging
- ❌ **Remove**: Services/SemanticSearch.cs, Services/Ingestion/, wwwroot/Data/, wwwroot/lib/{pdfjs-dist, pdf_viewer, markdown_viewer, dompurify}
- ✨ **Customize**: ChatMessageItem.razor (weather/allergen cards), Chat.razor (system prompt), Program.cs (Foundry Local/Ollama provider)
- ➕ **Add**: Components/Weather/{WeatherCard, AllergenCard}.razor, Services/AgentService.cs, Agent project (MCP tools + HTTP clients)

## Complexity Tracking

> **NO VIOLATIONS** - Constitution check passed all 11 principles. This section intentionally left empty.

## Phase 0: Research & Unknowns Resolution

**Goal**: Resolve all "NEEDS CLARIFICATION" items and establish best practices for key technologies.

**Research Tasks**:

1. **Agent Framework Best Practices** (FR-002)
   - Research: Microsoft.Extensions.AI Agent Framework patterns for tool invocation
   - Questions: How to register MCP tools? How does conversation context work? Error handling patterns?
   - Output: `research.md` section on Agent Framework setup with IChatClient + tool registration examples

2. **Aspire 13 + Foundry Local Integration** (FR-001, FR-004)
   - Research: Aspire 13 AppHost configuration for Foundry Local (Windows/macOS) vs Ollama (Linux)
   - Questions: How to detect platform? How to configure model endpoints? Dashboard auto-launch?
   - Output: `research.md` section on Aspire platform detection code snippets

3. **OpenMeteo API Patterns** (FR-005, FR-006, FR-007)
   - Research: OpenMeteo Geocoding, Weather, Air Quality API endpoints, request/response formats
   - Questions: Rate limits? Error codes? Coordinate precision requirements?
   - Output: `research.md` section on OpenMeteo API contracts with example HTTP requests/responses

4. **Polly Retry Policies for HTTP** (FR-009)
   - Research: Polly 8.5 resilience patterns for HttpClient
   - Questions: Retry count? Exponential backoff configuration? Circuit breaker needed?
   - Output: `research.md` section on Polly policy configuration with code example

5. **Blazor Server WCAG AA Implementation** (FR-013)
   - Research: WCAG 2.1 AA requirements for Blazor components (ARIA labels, keyboard navigation)
   - Questions: How to make Blazor Server real-time updates screen reader friendly? Focus management?
   - Output: `research.md` section on accessibility patterns with Blazor examples

**Deliverable**: `specs/001-phi4-weather-assistant/research.md` with:
- Agent Framework tool registration patterns
- Aspire platform detection code
- OpenMeteo API contracts (endpoints, request/response schemas)
- Polly retry policy configuration
- WCAG AA implementation checklist for Blazor

**Gate**: All NEEDS CLARIFICATION resolved → Proceed to Phase 1

## Phase 1: Design & Contracts

**Goal**: Define data models, API contracts, and architectural decisions before implementation.

**Design Tasks**:

### Task 0: Template Customization Scope (FR-014, FR-015)

Document keep/remove/customize/add decisions for aichatweb template.

**Deliverable**: `specs/001-phi4-weather-assistant/template-customization.md`

```markdown
# aichatweb Template Customization

## Keep (Principle VIII)
- Components/Chat/ChatInput.razor
- Components/Chat/ChatMessageList.razor
- Components/Chat/ChatMessageItem.razor
- Components/Chat/ChatHeader.razor
- Components/Chat/ChatSuggestions.razor
- IChatClient integration in Program.cs
- SignalR real-time messaging

## Remove (FR-015)
- Services/SemanticSearch.cs
- Services/Ingestion/ (entire folder)
- wwwroot/Data/ (sample documents)
- wwwroot/lib/pdfjs-dist/
- wwwroot/lib/pdf_viewer/
- wwwroot/lib/markdown_viewer/
- wwwroot/lib/dompurify/

## Customize
- Components/Chat/ChatMessageItem.razor → Add weather/allergen card rendering
- Components/Pages/Chat.razor → Update system prompt for weather domain
- Program.cs → Replace vector store with Foundry Local/Ollama IChatClient provider

## Add
- Components/Weather/WeatherCard.razor
- Components/Weather/AllergenCard.razor
- Services/AgentService.cs (orchestrates Agent Framework + MCP tools)
- src/Phi4WeatherAgent.Agent/ (entire backend project)
```

### Task 1: Data Model Design

Define domain models for weather data entities (FR-005, FR-006, FR-007).

**Deliverable**: `specs/001-phi4-weather-assistant/data-model.md`

```csharp
// Location.cs
public record Location(
    string Name,
    double Latitude,
    double Longitude,
    string? Country,
    string? State
);

// WeatherData.cs
public record CurrentConditions(
    double Temperature,
    double FeelsLike,
    int Humidity,
    double WindSpeed,
    string Description
);

public record DailyForecast(
    DateOnly Date,
    double HighTemp,
    double LowTemp,
    int PrecipitationProbability,
    string Conditions
);

public record WeatherData(
    Location Location,
    CurrentConditions Current,
    IReadOnlyList<DailyForecast> Forecast
);

// AllergenData.cs
public enum PollenSeverity { Low, Moderate, High, VeryHigh }

public record AllergenData(
    Location Location,
    DateTime Timestamp,
    PollenSeverity GrassPollen,
    PollenSeverity TreePollen,
    PollenSeverity WeedPollen
);
```

### Task 2: MCP Tool Contracts

Define interfaces for MCP tools (FR-008).

**Deliverable**: `specs/001-phi4-weather-assistant/contracts/mcp-tools.md`

```csharp
// Tools/GeocodeTool.cs
public class GeocodeTool
{
    [Description("Convert location name to coordinates")]
    public Task<Location[]> GeocodeAsync(
        [Description("Location name (city, address)")] string locationName,
        CancellationToken ct
    );
}

// Tools/WeatherTool.cs
public class WeatherTool
{
    [Description("Get weather forecast for coordinates")]
    public Task<WeatherData> GetForecastAsync(
        [Description("Latitude")] double latitude,
        [Description("Longitude")] double longitude,
        CancellationToken ct
    );
}

// Tools/AllergenTool.cs
public class AllergenTool
{
    [Description("Get pollen/allergen levels for coordinates")]
    public Task<AllergenData> GetAllergenDataAsync(
        [Description("Latitude")] double latitude,
        [Description("Longitude")] double longitude,
        CancellationToken ct
    );
}
```

### Task 3: HTTP Client Interfaces

Define service interfaces for OpenMeteo HTTP clients (FR-005, FR-006, FR-007).

**Deliverable**: `specs/001-phi4-weather-assistant/contracts/http-clients.md`

```csharp
// Services/OpenMeteoGeocodeClient.cs
public interface IOpenMeteoGeocodeClient
{
    Task<Location[]> SearchAsync(string query, CancellationToken ct);
}

// Services/OpenMeteoWeatherClient.cs
public interface IOpenMeteoWeatherClient
{
    Task<WeatherData> GetForecastAsync(double lat, double lon, CancellationToken ct);
}

// Services/OpenMeteoAllergenClient.cs
public interface IOpenMeteoAllergenClient
{
    Task<AllergenData> GetAllergenDataAsync(double lat, double lon, CancellationToken ct);
}
```

### Task 4: Blazor Component Contracts

Define component parameters for weather cards.

**Deliverable**: `specs/001-phi4-weather-assistant/contracts/components.md`

```csharp
// Components/Weather/WeatherCard.razor
@code {
    [Parameter, EditorRequired]
    public WeatherData Data { get; set; } = default!;
    
    [Parameter]
    public EventCallback OnRefresh { get; set; }
}

// Components/Weather/AllergenCard.razor
@code {
    [Parameter, EditorRequired]
    public AllergenData Data { get; set; } = default!;
    
    [Parameter]
    public EventCallback OnRefresh { get; set; }
}
```

### Task 5: Quick Start Guide

User-facing documentation for running the application (FR-017, FR-018).

**Deliverable**: `specs/001-phi4-weather-assistant/quickstart.md`

```markdown
# Quick Start Guide

## Prerequisites
- .NET 10 SDK (10.0.100+)
- Platform-specific AI model hosting:
  - **Windows/macOS**: Foundry Local (aspire-ai workload)
  - **Linux**: Ollama + Phi-4 model

## Setup

### Windows
```powershell
.\scripts\setup-windows.ps1
```

### macOS/Linux
```bash
chmod +x scripts/setup-macos.sh scripts/setup-linux.sh
./scripts/setup-macos.sh  # macOS
./scripts/setup-linux.sh   # Linux
```

## Run

```bash
dotnet run --project src/Phi4WeatherAgent.AppHost
```

Aspire Dashboard opens at `https://localhost:17185` (auto-launched).
Blazor app runs at `https://localhost:7184`.

## Example Queries
- "What's the weather in Seattle?"
- "Will it rain tomorrow in Portland?"
- "What are the pollen levels in Austin?"
- "Plan my weekend in Denver"
```

**Gate**: All design documents complete → Re-run Constitution Check → Proceed to implementation (Phase 2+)

## Implementation Workflow

**Note**: Detailed tasks are generated by `/speckit.tasks` command (Phase 2+). This section provides high-level implementation guidance.

### Phase 2: Foundation (Blocking Infrastructure)

1. Create Aspire AppHost + ServiceDefaults projects
2. Configure platform detection (Foundry Local vs Ollama)
3. Setup global.json, Directory.Build.props, .editorconfig
4. Create CI workflow (.github/workflows/ci.yml) for multi-platform testing

### Phase 3: User Story 1 - Basic Weather Query (P1 MVP)

1. Implement domain models (Location, WeatherData)
2. Implement OpenMeteoGeocodeClient + OpenMeteoWeatherClient with Polly retry
3. Implement GeocodeTool + WeatherTool (MCP)
4. Create WeatherCard.razor component
5. Customize ChatMessageItem.razor for weather card rendering
6. Configure IChatClient provider in Web/Program.cs
7. Update Chat.razor system prompt
8. Write unit tests (Agent.Tests), component tests (Web.Tests), E2E test (Playwright)

### Phase 4: User Story 2 - Allergen Information (P2)

1. Implement AllergenData model
2. Implement OpenMeteoAllergenClient with Polly retry
3. Implement AllergenTool (MCP)
4. Create AllergenCard.razor component
5. Extend ChatMessageItem.razor for allergen card rendering
6. Write tests (unit, component, E2E)

### Phase 5: User Story 3 - Multi-Day Planning (P3)

1. Enhance system prompt for multi-day comparative analysis
2. Update WeatherCard.razor for side-by-side display
3. Enhance conversation context handling in AgentService
4. Write tests (integration, E2E)

### Phase 6: User Story 4 - Accessibility (P2)

1. Add ARIA labels to all interactive components
2. Implement keyboard navigation (Tab, Enter, Escape)
3. Add live regions for screen reader announcements
4. Ensure color contrast ≥4.5:1 for all text
5. Test with axe DevTools + NVDA/JAWS
6. Write accessibility E2E tests (Playwright with screen reader simulation)

### Phase 7: Polish & Performance

1. Add BenchmarkDotNet tests for Agent Framework initialization
2. Optimize bundle size (remove unused template assets)
3. Add error boundaries for graceful failure handling
4. Write README.md with architecture diagram
5. Final accessibility audit
6. Cross-platform testing (Windows, macOS, Linux CI matrix)

## Success Criteria Validation

Each phase must validate relevant Success Criteria (SC-001 to SC-012) from spec.md:

- **Phase 3 (P1)**: SC-001 (query <5s), SC-003 (cross-platform), SC-007 (init <2s)
- **Phase 4 (P2)**: SC-006 (Polly retry recovery), SC-008 (zero paid services)
- **Phase 5 (P3)**: SC-002 (location disambiguation >80%)
- **Phase 6 (P2)**: SC-004 (color contrast), SC-005 (keyboard workflow), SC-009 (E2E coverage)
- **Phase 7**: SC-010 (>80% coverage), SC-011 (Aspire telemetry), SC-012 (setup script success)
