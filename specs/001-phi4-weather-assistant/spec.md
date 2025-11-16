# Feature Specification: Phi-4 Weather Assistant

**Feature Branch**: `001-phi4-weather-assistant`  
**Created**: 2025-11-16  
**Status**: Draft  
**Input**: Local-first weather assistant using Phi-4 model, Agent Framework, and Aspire 13 orchestration

## Clarifications

### Session 2025-11-16

- Q: When Phi-4 generates response outside weather domain (e.g., "Who won the World Series?"), what should system behavior be? → A: Trust system prompt only - let Phi-4 handle constraints, no post-processing validation
- Q: When displaying multiple weather cards for multi-day planning, how should they be laid out visually? → A: Horizontal scrollable carousel on mobile, side-by-side grid on desktop (responsive design)
- Q: When OpenMeteo API rate limit is exceeded, what should happen? → A: Display error immediately (no caching/retry - local exercise scope)
- Q: How should conversation context (location) be stored for follow-up queries? → A: In-memory only (session-scoped, resets on page refresh)
- Q: What should be the source and format for weather icons for accessibility? → A: SVG icons with aria-label text (scalable, accessible, no external dependencies)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Basic Weather Query (Priority: P1)

User asks for current weather in natural language and receives formatted weather information with temperature, conditions, and forecast.

**Why this priority**: Core value proposition - users need immediate weather information. This is the MVP that validates the entire architecture (local AI inference, MCP tools, chat UI).

**Independent Test**: Can be fully tested by asking "What's the weather in Seattle?" and verifying structured weather card appears with current conditions and 7-day forecast.

**Acceptance Scenarios**:

1. **Given** user opens the application, **When** they type "What's the weather in Seattle?", **Then** system displays current temperature, conditions, and 7-day forecast in a weather card
2. **Given** user asks "Will it rain tomorrow in Portland?", **When** system processes query, **Then** system shows tomorrow's precipitation probability and conditions
3. **Given** user types ambiguous location "Springfield", **When** system detects multiple matches, **Then** system asks for clarification (state/country)
4. **Given** OpenMeteo API fails with timeout, **When** user clicks "Retry" button in error message, **Then** system re-attempts geocoding and weather API calls with preserved location query

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

1. **Given** user asks "What's the weather this weekend in Denver?", **When** system processes multi-day query, **Then** system displays weather cards in horizontal scrollable carousel (mobile) or side-by-side grid (desktop) for Saturday and Sunday with temperature ranges and conditions
2. **Given** user asks "Best day for hiking next week?", **When** system analyzes 7-day forecast, **Then** system recommends specific days based on precipitation, temperature, and wind
3. **Given** user asks follow-up "What about Monday?", **When** system maintains conversation context in-memory, **Then** system uses previously mentioned location and provides Monday forecast

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

- What happens when **user enters non-existent location** (e.g., "Weather in Atlantis")? → System uses geocoding MCP tool, detects no results, displays empty state: "No locations found for 'Atlantis'. Please check spelling or try nearby city."
- What happens when **geocoding returns zero results**? → Display empty state: "No locations found for '[query]'. Please check spelling or try nearby city" with example suggestions
- What happens when **weather data unavailable for valid location**? → Display: "Weather data unavailable for [location]. This may be due to remote location or temporary API issue."
- What happens when **allergen data missing for location**? → Display: "Pollen data unavailable for [location]. Try nearby city: [suggestions from geocoding]"
- What happens when **OpenMeteo API is temporarily unavailable**? → Polly retry policy attempts 3 retries with exponential backoff, then displays immediate error message (no rate limit caching for local exercise scope)
- What happens when **user wants to retry after API failure**? → Display "Retry" button in error message; clicking retries same query with preserved context
- What happens when **user submits query while previous query processing**? → System cancels previous query via CancellationToken, displays loading state for new query
- What happens when **API responses arrive out of order** (slow query A finishes after fast query B)? → System discards stale responses, displays only most recent query result
- What happens when **weather API returns partial forecast** (e.g., 3 days instead of 7)? → Display available days only; show message "Forecast available for next [N] days"
- What happens when **allergen API returns incomplete pollen data**? → Display available categories only; hide missing categories (no "N/A" placeholders)
- What happens when **current weather missing but forecast available**? → Display forecast only; show "Current conditions unavailable"
- What happens when **OpenMeteo returns non-JSON response** (HTML error page)? → Catch JsonException, display error: "Weather service unavailable. Try again later."
- What happens when **API schema changes unexpectedly**? → Use nullable properties in DTOs, validate required fields, fail gracefully with error message
- What happens when **user provides coordinates** (e.g., "47.6062, -122.3321")? → System detects coordinate pattern (regex), passes directly to weather API, skips geocoding
- What happens when **user asks for landmark/POI** (e.g., "weather at Eiffel Tower")? → Geocoding API resolves to Paris, France; system displays resolved location name
- What happens when **user provides non-English location name** (e.g., "北京")? → OpenMeteo Geocoding API supports Unicode; system handles UTF-8 transparently
- What happens when **location name contains accents** (e.g., "São Paulo")? → System URL-encodes query, OpenMeteo API handles Unicode correctly
- What happens when **user attempts injection** (e.g., "Seattle'; DROP TABLE--")? → Input validation rejects non-standard characters; display error "Invalid location name"
- What happens when **user submits extremely long query** (>2000 chars)? → UI prevents submission; display message "Query too long. Maximum 2000 characters."
- What happens when **combined context exceeds Phi-4 limits** (conversation history + query)? → Truncate oldest conversation turns; always include system prompt and current query
- What happens when **model inference exceeds 30s timeout**? → Cancel inference via CancellationToken, display error: "Query timed out. Try simpler question."
- What happens when **memory usage exceeds 1GB**? → Log critical error, suggest application restart, display: "Application experiencing resource issues. Please refresh page."
- What happens when **OpenMeteo rate limit exceeded** (10K requests/day)? → API returns 429 status; system displays error: "Weather service rate limit reached. Try again tomorrow." No automatic retry
- What happens when **user asks for historical weather** (outside 7-day forecast window)? → Agent clarifies that only current conditions and 7-day forecast are available, suggests alternative phrasing
- What happens when **Phi-4 model generates off-topic response** (e.g., sports, poetry)? → System prompt constrains responses to weather domain only; no post-processing validation layer
- What happens when **user asks follow-up query referencing previous location** (e.g., "What about tomorrow?")? → System maintains location context in-memory (session-scoped per Blazor Server circuit), resets on page refresh
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
- **FR-010**: System MUST display weather data in structured cards (temperature, conditions, forecast, SVG icons with aria-label)
- **FR-011**: System MUST display multiple weather cards using horizontal scrollable carousel on mobile, side-by-side grid on desktop
- **FR-012**: System MUST support natural language queries (no rigid command syntax required)
- **FR-013**: System MUST maintain conversation context per Blazor Server circuit (one context per browser tab/window): Context isolated between tabs (no cross-tab sharing), context cleared on page refresh or tab close, context includes last queried location and conversation history (last 5 messages), no persistent storage (in-memory Dictionary<CircuitId, ConversationContext>)
- **FR-014**: System MUST constrain Phi-4 responses to weather domain via system prompt only (no AI response post-processing or content filtering). Note: This does NOT affect input validation (FR-027) or API response validation (FR-038-039)
- **FR-015**: System MUST meet WCAG 2.1 Level AA accessibility standards (keyboard navigation, screen reader support, color contrast ≥4.5:1)
- **FR-016**: System MUST use Blazor Server (from aichatweb template) for real-time chat UI
- **FR-017**: System MUST remove (delete) vector store, document ingestion, and semantic search components from aichatweb template: Delete files (SemanticSearch.cs, Ingestion/, wwwroot/Data/), remove dependencies (Microsoft.SemanticKernel.Connectors.Memory, vector DB packages), remove UI (document upload components, search bar), verify build succeeds with zero warnings about missing types
- **FR-018**: System MUST export telemetry to Aspire Dashboard with auto-launch in development: Traces (all HTTP requests to OpenMeteo APIs, model inferences via Phi-4, Blazor Server events), Metrics (query latency p50/p95/p99, API success rate, memory usage, token count per query), Logs (structured JSON logging, log levels Debug/Info/Warning/Error, correlation IDs)
- **FR-019**: System MUST support cross-platform development with platform-aware configuration: Source code 100% cross-platform (no #if WINDOWS directives), runtime detection (Aspire AppHost detects OS, configures Foundry Local for Windows/macOS or Ollama for Linux), setup scripts platform-specific (PowerShell for Windows, Bash for macOS/Linux), user experience identical across platforms (IChatClient abstraction)
- **FR-020**: System MUST provide platform-specific setup scripts (PowerShell for Windows, Bash for macOS/Linux)
- **FR-021**: System MUST use xUnit for unit tests, bUnit for component tests, Playwright for E2E tests
- **FR-022**: System MUST maintain >80% code coverage for weather query processing and MCP tool execution
- **FR-023**: System MUST use MIT license and only permissively-licensed dependencies (MIT, Apache 2.0, BSD)
- **FR-024**: System MUST NOT use Semantic Kernel (conflicts with Agent Framework)
- **FR-025**: System MUST NOT use paid APIs or cloud AI services (Azure OpenAI, OpenAI API)
- **FR-026**: System MUST display error message immediately after Polly retry policy exhausts (3 failed attempts): Loading indicator visible during retries (user sees "Loading..." not retry count), error appears after final retry failure, error message "Weather service unavailable. Please try again." with Retry button (FR-034)
- **FR-027**: System MUST validate location input before calling geocoding API: Trim whitespace, check length (3-100 characters), accept Unicode characters (UTF-8), detect coordinate patterns via regex `^\s*-?\d+\.?\d*\s*,\s*-?\d+\.?\d*\s*$`, pass coordinates directly to weather API (skip geocoding), URL-encode before API calls via Uri.EscapeDataString(), reject control characters and SQL injection patterns (basic sanitization)
- **FR-028**: System MUST include BenchmarkDotNet tests for Agent Framework initialization performance
- **FR-029**: System MUST display loading indicators during asynchronous operations: Chat message shows "Thinking..." spinner while Phi-4 processes query, weather card shows skeleton loader while fetching OpenMeteo data, geocoding displays "Searching for location..." text during geocoding API call
- **FR-030**: Loading indicators MUST include ARIA live region announcements: aria-live="polite" for non-critical updates, aria-label="Loading weather data" for screen reader users
- **FR-031**: System MUST distinguish between API errors (network failure, timeout) and empty results (valid response, zero data): Empty results display helpful message with suggestions, API errors display error with retry action (per FR-026)
- **FR-032**: System MUST display initialization status during first startup: Aspire Dashboard link "View telemetry at <https://localhost:15888>", model status "Phi-4 model loaded" (or loading progress if detectable), first query warning "First query may take 5-10 seconds (cold start)"
- **FR-033**: System MUST complete initialization checks before accepting user input: Verify Foundry Local/Ollama endpoint reachable, verify OpenMeteo API connectivity (health check), display error if prerequisites not met "AI model not available. Run setup script."
- **FR-034**: System MUST provide user-initiated retry after errors: Error messages include "Retry" button, retry preserves original query and location context, maximum 3 manual retries before suggesting page refresh
- **FR-035**: System MUST restore conversation context after transient errors: Location from previous successful query retained in-memory, follow-up queries work after error recovery, context cleared only on page refresh or explicit user action
- **FR-036**: System MUST handle concurrent queries gracefully: New query submission cancels in-flight query (CancellationToken), only most recent query response displayed, loading indicator reflects current (not previous) query
- **FR-037**: System SHOULD debounce rapid query submissions: 300ms delay after user stops typing before submitting, submit immediately on Enter key press, cancel previous debounced submission if new query started
- **FR-038**: System MUST gracefully handle partial API responses: Validate required fields (latitude, longitude, timestamp), display available data without failing entire query, log warning for missing optional fields (telemetry only, not user-facing)
- **FR-039**: System MUST validate API responses before processing: Catch JsonException for malformed JSON, validate required fields present (latitude, longitude, temperature, weather_code), log schema validation errors to telemetry for debugging, display user-friendly error "Unable to process weather data. Service may be experiencing issues."
- **FR-040**: System MUST handle browser/tab close gracefully: Cancel in-flight API requests via CancellationToken, Blazor Server detects SignalR disconnect and cleans up resources, in-memory context automatically cleared (session-scoped), no persistent state to clean up (per Constitution VI)
- **FR-041**: System MUST enforce input length limits: Chat input 2000 characters max (UI constraint), display character counter "[X]/2000" below textarea, disable submit button when limit exceeded, Phi-4 context window 131K tokens (no user-facing limit, server-side only)
- **FR-042**: System MUST handle token limit errors gracefully: Catch exceptions from model inference, display error "Query too complex. Please simplify and try again.", suggest shorter rephrasing if detected
- **FR-043**: System MUST prevent resource exhaustion: Model inference timeout 30 seconds (CancellationToken), memory monitoring logs warning if >500MB and error if >1GB, CPU throttling limits concurrent model inferences to 1 (single-user app)
- **FR-044**: System MUST log resource usage to Aspire Dashboard: Memory usage per query (OpenTelemetry metrics), CPU time for model inference, API call latency breakdown (geocoding, weather, allergen)
- **FR-045**: System MUST prevent XSS attacks: Blazor Server escapes all output by default (use @variable, not @Html.Raw), validate location input to reject `<script>`, `<iframe>`, on\* attributes, Content Security Policy `script-src 'self'; object-src 'none';`, sanitize Phi-4 responses by encoding HTML entities before display
- **FR-046**: System MUST implement input sanitization: Location names allow alphanumeric, spaces, hyphens, accents only, query text rejects control characters with 2000 char limit, no SQL/command injection risk (API calls are HTTP GET, no database)
- **FR-047**: System MUST use OpenTelemetry for instrumentation: HTTP client instrumentation automatic via AddHttpClient, custom spans for Agent Framework query processing and MCP tool invocation, correlation with TraceId/SpanId propagated across service boundaries
- **FR-048**: System MUST support local development deployment only: Run via `dotnet run --project Phi4WeatherAgent.AppHost`, Aspire Dashboard auto-launches at <https://localhost:15888>, Web UI accessible at <https://localhost:5001> (or dynamically assigned port), no production deployment (local-first exercise per Constitution)
- **FR-049**: System SHOULD provide convenience scripts for running: Windows `run.ps1` (PowerShell script calling dotnet run), macOS/Linux `run.sh` (Bash script calling dotnet run), scripts detect if setup completed and prompt to run setup if missing
- **FR-050**: Setup scripts MUST check for existing installations: Detect if Phi-4 model already cached (skip re-download), detect .NET 10 SDK version and warn if preview version outdated, provide `--force-update` flag to re-download model/dependencies

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
- **SC-011**: Aspire Dashboard displays HTTP request traces with status codes, latency, retry attempts; model inference spans with token count, temperature, prompt preview; custom spans for geocoding, weather API, allergen API with coordinates
- **SC-012**: Setup scripts succeed on fresh installs of Windows 11, macOS Sonoma, and Ubuntu 22.04 LTS
- **SC-013**: Loading states appear within 200ms of user action and include accessible labels
- **SC-014**: Loading indicators disappear immediately upon data arrival or error
- **SC-015**: Initialization status visible to user within 1 second of application start
- **SC-016**: First query completes within 10 seconds (cold start), subsequent queries <5s (warm)
- **SC-017**: Application passes OWASP ZAP security scan (no high/critical findings)
- **SC-018**: XSS attempts in query input are escaped/rejected (verified via E2E test)
- **SC-019**: Code analysis produces zero warnings (enforce via `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)
- **SC-020**: All public APIs documented with XML comments (verified via docfx or similar)

## Technical Constraints

### Architecture Decisions

**Template Base**: Microsoft `aichatweb` template with customizations:

- ✅ Keep: ChatInput.razor, ChatMessageList.razor, ChatMessageItem.razor, ChatHeader.razor, ChatSuggestions.razor
- ❌ Remove: SemanticSearch.cs, Ingestion/, wwwroot/Data/, PDF viewer libs
- ✨ Customize: ChatMessageItem.razor (responsive weather card layout), Chat.razor (system prompt for weather domain only), IChatClient provider (Foundry Local/Ollama)
- ➕ Add: AgentService (MCP orchestration with in-memory context), WeatherCard.razor (SVG icons with aria-label), AllergenCard.razor, OpenMeteo HTTP clients

**Project Structure**:

```text
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
- Resource cleanup: <1 second after SignalR disconnect
- No memory leaks after 100 query cycles (verified via dotMemory profiler)

### Compliance Requirements

- WCAG 2.1 Level AA (keyboard navigation, screen reader compatibility, color contrast ≥4.5:1)
- MIT License for all project code
- No GPL/AGPL dependencies
- Cross-platform compatibility (Windows/macOS/Linux)

### Code Quality Requirements

- C# coding standards: Follow Microsoft .NET conventions (CA rules enforced)
- XML documentation: Public APIs require XML doc comments (`<summary>`, `<param>`)
- Unit test naming: `[MethodName]_[Scenario]_[ExpectedResult]`
- Component organization: One component per file, max 300 lines per file
- Dependency injection: Constructor injection only (no service locator pattern)

### Upgrade Strategy

- Model updates: Re-run setup script; `foundry model download phi4-mini` pulls latest version
- .NET 10 SDK: Update global.json version, run `dotnet restore`, verify compatibility
- Agent Framework preview → stable: Update package versions, address breaking changes in separate task
- No data migration needed (no persistent state per Constitution VI)

### Explicitly Out of Scope

- Docker containerization (Constitution IV: No containers)
- Azure/cloud deployment (Constitution VI: Zero cloud costs)
- CI/CD pipelines (local-first development only)
- Release binaries (source code distribution only)
- Data backup/recovery (no persistent user data; conversation context in-memory only)
- Disaster recovery (local-first app; users can re-run setup if needed)
- State restoration (session-scoped context resets on page refresh)

### Assumptions

- OpenMeteo free tier (10K requests/day) sufficient for single-user local development
- Rate limit risk: Low (requires 278 queries/hour sustained for 24 hours)
- If rate limit hit: Treated as API error per FR-026, displays error message
- No mitigation beyond error handling (Constitution VI: No paid upgrades, no caching layer)
- OpenMeteo API availability: Assumed reliable with internet connectivity
- Local disk space: 3.8GB available for Phi-4 Mini model
- RAM: Sufficient for <500MB target (Phi-4 model + application)
- Browser compatibility: Modern browsers supporting Blazor Server (Chrome, Edge, Firefox, Safari)
