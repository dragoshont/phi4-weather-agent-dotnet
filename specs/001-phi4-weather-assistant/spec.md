# Feature Specification: Phi-4 Weather Assistant

**Feature Branch**: `001-phi4-weather-assistant`  
**Created**: 2025-11-16  
**Status**: Draft  
**Input**: Local-first weather assistant using Phi-4 model, Agent Framework, and Aspire 13 orchestration

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Basic Weather Query (Priority: P1)

User asks for current weather in natural language and receives formatted weather information with temperature, conditions, and forecast.

**Why this priority**: Core value proposition - users need immediate weather information. This is the MVP that validates the entire architecture (local AI inference, MCP tools, chat UI).

**Independent Test**: Can be fully tested by asking "What's the weather in Seattle?" and verifying structured weather card appears with current conditions and 7-day forecast.

**Acceptance Scenarios**:

1. **Given** user opens the application, **When** they type "What's the weather in Seattle?", **Then** system displays current temperature, conditions, and 7-day forecast in a weather card
2. **Given** user asks "Will it rain tomorrow in Portland?", **When** system processes query, **Then** system shows tomorrow's precipitation probability and conditions
3. **Given** user types ambiguous location "Springfield", **When** system detects multiple matches, **Then** system asks for clarification (state/country)

---

### User Story 2 - Allergen Information (Priority: P2)

User asks about pollen/allergen levels for their location and receives current allergen data with health recommendations.

**Why this priority**: Health-critical feature that differentiates from basic weather apps. Builds on P1 infrastructure (geocoding, MCP tools) but adds new data source.

**Independent Test**: Can be fully tested by asking "What are the pollen levels in Austin?" and verifying allergen card shows grass/tree/weed pollen levels with severity indicators.

**Acceptance Scenarios**:

1. **Given** user asks "What are the pollen levels today?", **When** system has location context, **Then** system displays current pollen levels by category (grass, tree, weed) with severity ratings
2. **Given** user asks "Should I go outside with allergies?", **When** system checks allergen data, **Then** system provides health recommendation based on pollen levels
3. **Given** allergen data unavailable for location, **When** system queries OpenMeteo, **Then** system gracefully handles missing data and suggests nearby locations

---

### User Story 3 - Multi-Day Planning (Priority: P3)

User asks about weather for upcoming events/trips and receives comparative analysis across multiple days.

**Why this priority**: Power user feature that leverages conversation context. Requires more sophisticated prompt engineering but uses same MCP tools as P1/P2.

**Independent Test**: Can be fully tested by asking "Plan my weekend in Denver" and verifying system compares Saturday vs Sunday weather with activity recommendations.

**Acceptance Scenarios**:

1. **Given** user asks "What's the weather this weekend in Denver?", **When** system processes multi-day query, **Then** system displays side-by-side comparison of Saturday and Sunday with temperature ranges and conditions
2. **Given** user asks "Best day for hiking next week?", **When** system analyzes 7-day forecast, **Then** system recommends specific days based on precipitation, temperature, and wind
3. **Given** user asks follow-up "What about Monday?", **When** system maintains conversation context, **Then** system uses previously mentioned location and provides Monday forecast

---

### User Story 4 - Accessibility for Screen Readers (Priority: P2)

Visually impaired users can navigate the application with keyboard shortcuts and receive weather information via screen reader announcements.

**Why this priority**: Constitutional requirement (WCAG 2.1 AA). Critical for inclusive design but can be implemented in parallel with P1.

**Independent Test**: Can be fully tested using NVDA/JAWS screen readers to navigate the chat interface, submit queries, and hear weather card content read aloud.

**Acceptance Scenarios**:

1. **Given** user navigates with Tab key, **When** they reach chat input, **Then** screen reader announces "Message input, edit text"
2. **Given** weather card appears, **When** screen reader focuses on card, **Then** screen reader reads "Weather for Seattle: 52 degrees, partly cloudy, high 58, low 45"
3. **Given** user submits query with Enter key, **When** response arrives, **Then** screen reader announces live region update with weather summary

---

### Edge Cases

- What happens when **user enters non-existent location** (e.g., "Weather in Atlantis")? → System uses geocoding MCP tool, detects no results, asks user to verify spelling or try nearby city
- What happens when **OpenMeteo API is temporarily unavailable**? → Polly retry policy attempts 3 retries with exponential backoff, then displays user-friendly error message
- What happens when **user asks for historical weather** (outside 7-day forecast window)? → Agent clarifies that only current conditions and 7-day forecast are available, suggests alternative phrasing
- What happens when **Phi-4 model generates irrelevant response**? → System prompt constrains responses to weather domain; if model hallucinates, user can retry or rephrase
- What happens when **user has no internet connection**? → Application detects network failure, displays offline message, local AI model remains functional but MCP tools (weather APIs) fail gracefully

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST run Phi-4 inference locally using Foundry Local (Windows/macOS) or Ollama (Linux) without cloud dependencies
- **FR-002**: System MUST use Microsoft.Extensions.AI Agent Framework (version 10.0.0-preview.1.25071.7+) for AI orchestration
- **FR-003**: System MUST target .NET 10 SDK (version 10.0.100+) with no .NET 9 or earlier code
- **FR-004**: System MUST use Aspire 13 (version 13.0.0-preview.1+) for service orchestration and observability
- **FR-005**: System MUST provide geocoding via OpenMeteo Geocoding API (free, no API key)
- **FR-006**: System MUST retrieve weather forecasts via OpenMeteo Weather API (free, no API key)
- **FR-007**: System MUST retrieve allergen data via OpenMeteo Air Quality API (free, no API key)
- **FR-008**: System MUST implement MCP tools for geocoding, weather, and allergen data retrieval
- **FR-009**: System MUST use Polly 8.5+ for retry policies on HTTP requests (3 retries, exponential backoff)
- **FR-010**: System MUST display weather data in structured cards (temperature, conditions, forecast, icons)
- **FR-011**: System MUST support natural language queries (no rigid command syntax required)
- **FR-012**: System MUST maintain conversation context for follow-up queries
- **FR-013**: System MUST meet WCAG 2.1 Level AA accessibility standards (keyboard navigation, screen reader support, color contrast ≥4.5:1)
- **FR-014**: System MUST use Blazor Server (from aichatweb template) for real-time chat UI
- **FR-015**: System MUST remove vector store, document ingestion, and semantic search from aichatweb template
- **FR-016**: System MUST auto-launch Aspire Dashboard in development for telemetry visualization
- **FR-017**: System MUST support cross-platform development (Windows, macOS, Linux)
- **FR-018**: System MUST provide platform-specific setup scripts (PowerShell for Windows, Bash for macOS/Linux)
- **FR-019**: System MUST use xUnit for unit tests, bUnit for component tests, Playwright for E2E tests
- **FR-020**: System MUST maintain >80% code coverage for weather query processing and MCP tool execution
- **FR-021**: System MUST use MIT license and only permissively-licensed dependencies (MIT, Apache 2.0, BSD)
- **FR-022**: System MUST NOT use Semantic Kernel (conflicts with Agent Framework)
- **FR-023**: System MUST NOT use paid APIs or cloud AI services (Azure OpenAI, OpenAI API)
- **FR-024**: System MUST handle API failures gracefully (display user-friendly error, suggest retry)
- **FR-025**: System MUST validate location input before calling geocoding API (trim whitespace, check length)
- **FR-026**: System MUST include BenchmarkDotNet tests for Agent Framework initialization performance

### Key Entities

- **Location**: Represents geographic location with name, latitude, longitude, country, state (from geocoding API response)
- **WeatherData**: Current conditions (temperature, feels-like, humidity, wind speed, description) and 7-day forecast (daily high/low, precipitation probability, conditions)
- **AllergenData**: Pollen levels by category (grass, tree, weed) with severity ratings (low/moderate/high/very high)
- **ChatMessage**: User or assistant message with timestamp, content, and optional structured data (weather card, allergen card)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can query weather for any global location and receive response within 5 seconds (includes geocoding + weather API + AI processing)
- **SC-002**: System achieves >80% accuracy in location disambiguation (e.g., correctly identifies "Springfield, IL" vs "Springfield, MA" from context)
- **SC-003**: Application runs successfully on Windows, macOS, and Linux without platform-specific code changes
- **SC-004**: Weather cards meet WCAG 2.1 AA color contrast requirements (verified via axe DevTools)
- **SC-005**: Screen reader users can complete full query workflow (input → submit → hear results) using only keyboard navigation
- **SC-006**: Polly retry policy recovers from transient OpenMeteo API failures (3 retries succeed in >95% of temporary outages)
- **SC-007**: Agent Framework initialization completes in <2 seconds (measured via BenchmarkDotNet)
- **SC-008**: Application uses zero paid services (verified via dependency scan + runtime network traffic inspection)
- **SC-009**: E2E tests cover all 4 user stories (basic weather, allergen info, multi-day planning, accessibility)
- **SC-010**: Code coverage >80% for critical paths (weather query processing, MCP tool invocation, error handling)
- **SC-011**: Aspire Dashboard displays telemetry for all HTTP requests (OpenMeteo APIs) and AI inferences (Phi-4 model calls)
- **SC-012**: Setup scripts succeed on fresh installs of Windows 11, macOS Sonoma, and Ubuntu 22.04 LTS

## Technical Constraints

### Architecture Decisions

**Template Base**: Microsoft `aichatweb` template with customizations:
- ✅ Keep: ChatInput.razor, ChatMessageList.razor, ChatMessageItem.razor, ChatHeader.razor, ChatSuggestions.razor
- ❌ Remove: SemanticSearch.cs, Ingestion/, wwwroot/Data/, PDF viewer libs
- ✨ Customize: ChatMessageItem.razor (weather cards), Chat.razor (system prompt), IChatClient provider (Foundry Local/Ollama)
- ➕ Add: AgentService (MCP orchestration), WeatherCard.razor, AllergenCard.razor, OpenMeteo HTTP clients

**Project Structure**:
```
src/
├── Phi4WeatherAgent.Web/          # Blazor Server UI (from aichatweb template)
├── Phi4WeatherAgent.Agent/        # Agent Framework + MCP tools
├── Phi4WeatherAgent.AppHost/      # Aspire orchestration
└── Phi4WeatherAgent.ServiceDefaults/  # Shared Aspire configuration

tests/
├── Phi4WeatherAgent.Agent.Tests/  # xUnit unit tests
├── Phi4WeatherAgent.Web.Tests/    # bUnit component tests
└── Phi4WeatherAgent.E2E.Tests/    # Playwright E2E tests
```

### Performance Targets

- Weather query end-to-end: <5 seconds (p95)
- Agent Framework initialization: <2 seconds
- UI responsiveness: <100ms for user input handling
- Memory usage: <500MB for Phi-4 model + application (excluding Aspire Dashboard)

### Compliance Requirements

- WCAG 2.1 Level AA (keyboard navigation, screen reader compatibility, color contrast ≥4.5:1)
- MIT License for all project code
- No GPL/AGPL dependencies
- Cross-platform compatibility (Windows/macOS/Linux)
