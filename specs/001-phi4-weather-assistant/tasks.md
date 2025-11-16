---
description: "Task decomposition for Phi-4 Weather Assistant implementation"
---

# Tasks: Phi-4 Weather Assistant

**Input**: Design documents from `specs/001-phi4-weather-assistant/`
**Prerequisites**: ✅ plan.md, ✅ spec.md, ✅ research.md

**Tests**: Test tasks included per specification requirements (FR-021: xUnit, bUnit, Playwright; SC-010: >80% coverage)

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- All file paths are absolute from repository root

## Path Conventions

Multi-project Aspire structure:
- **AppHost**: `src/Phi4WeatherAgent.AppHost/`
- **ServiceDefaults**: `src/Phi4WeatherAgent.ServiceDefaults/`
- **Web (Blazor)**: `src/Phi4WeatherAgent.Web/`
- **Agent (Backend)**: `src/Phi4WeatherAgent.Agent/`
- **Tests**: `tests/Phi4WeatherAgent.{Agent|Web|E2E}.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure per Constitution principles

- [ ] T001 Create solution structure with 4 main projects (AppHost, ServiceDefaults, Web, Agent) + 3 test projects in src/phi4-weather-agent-dotnet.sln
- [ ] T002 [P] Create global.json pinning .NET 10 SDK version 10.0.100+ with allowPrerelease=true
- [ ] T003 [P] Create Directory.Build.props with centralized package versions (Microsoft.Extensions.AI 10.0.0-preview.1+, Aspire 13.0.0-preview.1+, Polly 8.5.0+)
- [ ] T004 [P] Create .editorconfig with C# coding standards (CA rules, code style, TreatWarningsAsErrors=true)
- [ ] T005 [P] Copy LICENSE file (MIT) to repository root with correct copyright year
- [ ] T006 [P] Create .gitignore for .NET projects (bin/, obj/, .vs/, *.user)
- [ ] T007 Initialize Phi4WeatherAgent.AppHost project with Aspire.Hosting.AppHost 13.0.0-preview.1+ package
- [ ] T008 Initialize Phi4WeatherAgent.ServiceDefaults project with Aspire.Hosting.ServiceDefaults 13.0.0-preview.1+ package
- [ ] T009 Initialize Phi4WeatherAgent.Web project from aichatweb template (dotnet new aichatweb -o src/Phi4WeatherAgent.Web)
- [ ] T010 Initialize Phi4WeatherAgent.Agent project as ASP.NET Core Web API (dotnet new webapi -o src/Phi4WeatherAgent.Agent)
- [ ] T011 [P] Initialize test projects (xUnit: Agent.Tests, bUnit: Web.Tests, Playwright: E2E.Tests)
- [ ] T012 Configure Playwright in E2E.Tests project (Chromium/Firefox/WebKit, 1920x1080, trace-on-failure)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story implementation

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Aspire Orchestration Configuration

- [ ] T013 Implement platform detection in src/Phi4WeatherAgent.AppHost/Program.cs using OperatingSystem.IsWindows()/IsMacOS()/IsLinux()
- [ ] T014 Configure Foundry Local provider for Windows/macOS with model "phi4-mini" in AppHost Program.cs
- [ ] T015 Configure Ollama provider for Linux with model "phi4" (Ollama alias for mini) in AppHost Program.cs
- [ ] T016 Add ServiceDefaults reference to Web and Agent projects via builder.AddProject().WithReference()
- [ ] T017 [P] Create src/Phi4WeatherAgent.ServiceDefaults/Extensions.cs with AddServiceDefaults() and ConfigureOpenTelemetry() methods

### OpenTelemetry Configuration

- [ ] T018 [P] Configure OpenTelemetry traces in ServiceDefaults (HTTP requests, model inference, Blazor events)
- [ ] T019 [P] Configure OpenTelemetry metrics in ServiceDefaults (query latency p50/p95/p99, API success rate, memory usage, token count)
- [ ] T020 [P] Configure OpenTelemetry logs in ServiceDefaults (structured JSON, Debug/Info/Warning/Error levels, correlation IDs)
- [ ] T021 Verify Aspire Dashboard auto-launches at https://localhost:15888 (or dynamically assigned port)

### Base Domain Models (Shared Across Stories)

- [ ] T022 [P] Create src/Phi4WeatherAgent.Agent/Models/Location.cs record (Name, Latitude, Longitude, Country, State)
- [ ] T023 [P] Create src/Phi4WeatherAgent.Agent/Models/WeatherData.cs with CurrentConditions, DailyForecast records
- [ ] T024 [P] Create src/Phi4WeatherAgent.Agent/Models/AllergenData.cs with PollenSeverity enum and AllergenData record

### HTTP Clients with Polly Retry

- [ ] T025 Create src/Phi4WeatherAgent.Agent/Services/OpenMeteoGeocodeClient.cs with IOpenMeteoGeocodeClient interface
- [ ] T026 Add Polly retry policy to OpenMeteoGeocodeClient (3 retries, exponential backoff, circuit breaker after 5 failures)
- [ ] T027 Create src/Phi4WeatherAgent.Agent/Services/OpenMeteoWeatherClient.cs with IOpenMeteoWeatherClient interface
- [ ] T028 Add Polly retry policy to OpenMeteoWeatherClient (3 retries, exponential backoff, circuit breaker after 5 failures)
- [ ] T029 Create src/Phi4WeatherAgent.Agent/Services/OpenMeteoAllergenClient.cs with IOpenMeteoAllergenClient interface
- [ ] T030 Add Polly retry policy to OpenMeteoAllergenClient (3 retries, exponential backoff, circuit breaker after 5 failures)
- [ ] T031 Register HTTP clients in Agent/Program.cs with AddHttpClient() and base URLs (geocoding-api.open-meteo.com, api.open-meteo.com, air-quality-api.open-meteo.com)

### Agent Framework Configuration

- [ ] T032 Add Microsoft.Extensions.AI 10.0.0-preview.1.25071.7+ package to Phi4WeatherAgent.Agent project
- [ ] T033 Create src/Phi4WeatherAgent.Web/Services/AgentService.cs with ChatClientAgent + FunctionInvokingChatClient setup
- [ ] T033a Display initialization status in Chat.razor: Show Aspire Dashboard link "View telemetry at https://localhost:15888", model status "Phi-4 Mini loaded" or loading indicator, cold start warning "First query may take 5-10 seconds" per FR-032-033
- [ ] T034 Configure IChatClient provider in Web/Program.cs (reference Foundry Local or Ollama endpoint from AppHost)
- [ ] T035 Implement conversation context management in AgentService (in-memory Dictionary<CircuitId, ConversationContext> with last 5 conversation turns - 10 messages: 5 user + 5 assistant)

### Template Cleanup (aichatweb Modifications)

- [ ] T036 [P] Delete src/Phi4WeatherAgent.Web/Services/SemanticSearch.cs (vector store removal per FR-017)
- [ ] T037 [P] Delete src/Phi4WeatherAgent.Web/Services/Ingestion/ folder (document ingestion removal per FR-017)
- [ ] T038 [P] Delete src/Phi4WeatherAgent.Web/wwwroot/Data/ folder (sample documents removal per FR-017)
- [ ] T039 [P] Delete src/Phi4WeatherAgent.Web/wwwroot/lib/pdfjs-dist/ (PDF viewer libraries removal per FR-017)
- [ ] T040 [P] Delete src/Phi4WeatherAgent.Web/wwwroot/lib/pdf_viewer/ (PDF viewer components removal per FR-017)
- [ ] T041 [P] Delete src/Phi4WeatherAgent.Web/wwwroot/lib/markdown_viewer/ (Markdown viewer removal per FR-017)
- [ ] T042 [P] Delete src/Phi4WeatherAgent.Web/wwwroot/lib/dompurify/ (DOMPurify removal per FR-017)
- [ ] T043 Remove vector store packages from Web.csproj (Microsoft.SemanticKernel.Connectors.Memory, vector DB dependencies)
- [ ] T044 Remove document upload UI components from aichatweb template (search for upload-related Blazor components)
- [ ] T045 Verify build succeeds with zero warnings via dotnet build --configuration Release /p:TreatWarningsAsErrors=true

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Basic Weather Query (Priority: P1) 🎯 MVP

**Goal**: Users can query current weather + 7-day forecast for any global location via natural language

**Independent Test**: Ask "What's the weather in Seattle?" and verify structured weather card with current conditions and 7-day forecast

### MCP Tools for User Story 1

- [ ] T046 [P] [US1] Create src/Phi4WeatherAgent.Agent/Tools/GeocodeTool.cs with GeocodeAsync(locationName) method
- [ ] T047 [P] [US1] Add XML doc comments to GeocodeTool with <summary>, <param>, <returns> for AIFunctionFactory binding
- [ ] T048 [P] [US1] Create src/Phi4WeatherAgent.Agent/Tools/WeatherTool.cs with GetForecastAsync(latitude, longitude) method
- [ ] T049 [P] [US1] Add XML doc comments to WeatherTool for AIFunctionFactory binding
- [ ] T050 [US1] Register GeocodeTool and WeatherTool in AgentService via AIFunctionFactory.Create()
- [ ] T051 [US1] Configure FunctionInvokingChatClient middleware in AgentService to wrap ChatClientAgent

### Geocoding Implementation

- [ ] T052 [US1] Implement OpenMeteoGeocodeClient.SearchAsync() with HTTP GET to geocoding-api.open-meteo.com/v1/search
- [ ] T053 [US1] Add input validation in GeocodeTool (trim whitespace, 3-100 char length, UTF-8 support, URL encoding via Uri.EscapeDataString())
- [ ] T054 [US1] Add coordinate pattern detection via regex `^\s*-?\d+\.?\d*\s*,\s*-?\d+\.?\d*\s*$` in GeocodeTool, validate latitude range -90 to +90 and longitude range -180 to +180, reject out-of-range values
- [ ] T055 [US1] Handle zero results gracefully with empty state message "No locations found for '[query]'. Please check spelling or try nearby city."
- [ ] T056 [US1] Add telemetry span for geocoding API call with coordinates as span attributes

### Weather Forecast Implementation

- [ ] T057 [US1] Implement OpenMeteoWeatherClient.GetForecastAsync() with HTTP GET to api.open-meteo.com/v1/forecast
- [ ] T058 [US1] Parse hourly and daily weather data (temperature_2m, precipitation, weather_code) with JSON source generation
- [ ] T059 [US1] Map WMO weather codes (0-99) to human-readable conditions and icons (0=Clear/Sun, 1-3=Cloudy/Cloud, 45-48=Fog, 51-67=Rain/Raindrop, 71-86=Snow/Snowflake, 95-99=Thunderstorm/Lightning) - document complete mapping table
- [ ] T060 [US1] Handle partial forecast data (e.g., 3 days instead of 7) by displaying available days only
- [ ] T061 [US1] Add validation for required fields (latitude, longitude, temperature) with graceful error "Unable to process weather data. Service may be experiencing issues."
- [ ] T062 [US1] Add telemetry span for weather API call with latency breakdown

### Weather Card UI Component

- [ ] T063 [P] [US1] Create src/Phi4WeatherAgent.Web/Components/Weather/WeatherCard.razor with [Parameter] WeatherData Data
- [ ] T064 [P] [US1] Add SVG weather icons (sun, cloud, rain, snow) in wwwroot/icons/ with aria-label attributes
- [ ] T065 [US1] Implement responsive layout in WeatherCard.razor (horizontal scrollable carousel on mobile, side-by-side grid on desktop per FR-011)
- [ ] T066 [US1] Add WCAG AA color contrast tokens to WeatherCard.razor.css (text-primary #1a1a1a, info #0066cc, success #0f7a3e, warning #b35900, error #c41e3a)
- [ ] T067 [US1] Add ARIA live region with aria-live="polite" to WeatherCard for screen reader announcements
- [ ] T068 [US1] Add loading skeleton to WeatherCard.razor displayed while fetching OpenMeteo data

### Chat Integration

- [ ] T069 [US1] Customize src/Phi4WeatherAgent.Web/Components/Chat/ChatMessageItem.razor to detect and render WeatherCard when message contains WeatherData
- [ ] T070 [US1] Update src/Phi4WeatherAgent.Web/Components/Pages/Chat.razor system prompt to "You are a weather assistant. Provide weather and pollen information using only the geocode_location and get_weather_forecast tools. Stay within the weather domain."
- [ ] T071 [US1] Add loading indicator "Thinking..." with spinner in Chat.razor while Phi-4 processes query - include aria-live="polite" announcement for screen readers per FR-029-030
- [ ] T072 [US1] Add loading text "Searching for location..." in Chat.razor during geocoding API call - include aria-label="Loading weather data" for screen readers per FR-030
- [ ] T073 [US1] Implement query cancellation via CancellationToken when new query submitted (cancel in-flight requests per FR-036)
- [ ] T074 [US1] Add error message UI with "Retry" button in ChatMessageItem.razor for API failures (per FR-026, FR-034)

### Tests for User Story 1

- [ ] T075 [P] [US1] Write unit test in tests/Phi4WeatherAgent.Agent.Tests/Services/OpenMeteoGeocodeClientTests.cs for successful geocoding
- [ ] T076 [P] [US1] Write unit test for geocoding zero results handling
- [ ] T077 [P] [US1] Write unit test in OpenMeteoWeatherClientTests.cs for successful weather forecast parsing
- [ ] T078 [P] [US1] Write unit test for partial forecast data handling (missing days)
- [ ] T079 [P] [US1] Write unit test in Tools/GeocodingToolTests.cs for coordinate pattern detection
- [ ] T080 [P] [US1] Write unit test for input validation (length, special characters)
- [ ] T081 [P] [US1] Write bUnit component test in tests/Phi4WeatherAgent.Web.Tests/Components/Weather/WeatherCardTests.cs for rendering current conditions
- [ ] T082 [P] [US1] Write bUnit component test for 7-day forecast rendering
- [ ] T083 [P] [US1] Write bUnit component test for loading skeleton display
- [ ] T084 [US1] Write Playwright E2E test in tests/Phi4WeatherAgent.E2E.Tests/WeatherQueryTests.cs for "What's the weather in Seattle?" query
- [ ] T085 [US1] Write Playwright E2E test for ambiguous location disambiguation ("Springfield")
- [ ] T086 [US1] Write Playwright E2E test for API timeout with retry button click

**Checkpoint**: User Story 1 MVP complete - can query weather, see formatted cards, handle errors

---

## Phase 4: User Story 2 - Allergen Information (Priority: P2)

**Goal**: Users can query pollen/allergen levels with health recommendations

**Independent Test**: Ask "What are the pollen levels in Austin?" and verify allergen card with grass/tree/weed pollen severity

### MCP Tool for User Story 2

- [ ] T087 [P] [US2] Create src/Phi4WeatherAgent.Agent/Tools/AllergenTool.cs with GetAllergenDataAsync(latitude, longitude) method
- [ ] T088 [P] [US2] Add XML doc comments to AllergenTool for AIFunctionFactory binding
- [ ] T089 [US2] Register AllergenTool in AgentService via AIFunctionFactory.Create()

### Allergen API Implementation

- [ ] T090 [US2] Implement OpenMeteoAllergenClient.GetAllergenDataAsync() with HTTP GET to air-quality-api.open-meteo.com/v1/air-quality
- [ ] T091 [US2] Parse pollen data (grass_pollen, birch_pollen, ragweed_pollen) with hourly arrays
- [ ] T092 [US2] Map pollen grains/m³ to PollenSeverity enum (Low 0-20, Moderate 21-50, High 51-100, Very High >100)
- [ ] T093 [US2] Handle Europe-only pollen data constraint (detect non-EU locations, display message "Pollen data unavailable for [location]. Try nearby city: [suggestions]")
- [ ] T094 [US2] Handle seasonal availability (null/zero pollen outside season) by displaying "Pollen data not available for this season"
- [ ] T095 [US2] Handle partial pollen data (missing categories) by displaying available categories only (no "N/A" placeholders per FR-038)
- [ ] T096 [US2] Add telemetry span for allergen API call

### Allergen Card UI Component

- [ ] T097 [P] [US2] Create src/Phi4WeatherAgent.Web/Components/Weather/AllergenCard.razor with [Parameter] AllergenData Data
- [ ] T098 [P] [US2] Add pollen severity color coding (Low=green #0f7a3e, Moderate=yellow #b35900, High=orange #ff6b35, Very High=red #c41e3a) with WCAG AA contrast
- [ ] T099 [US2] Add ARIA labels to AllergenCard severity indicators (aria-label="Grass pollen: High severity")
- [ ] T100 [US2] Add health recommendations based on severity (Low: "Safe for outdoor activities", High: "Limit outdoor exposure", Very High: "Stay indoors if possible")
- [ ] T101 [US2] Add loading skeleton to AllergenCard.razor displayed while fetching allergen data

### Chat Integration for User Story 2

- [ ] T102 [US2] Extend ChatMessageItem.razor to detect and render AllergenCard when message contains AllergenData
- [ ] T103 [US2] Update Chat.razor system prompt to include "get_allergen_levels tool for pollen information"
- [ ] T104 [US2] Add loading text "Fetching pollen data..." in Chat.razor during allergen API call

### Tests for User Story 2

- [ ] T105 [P] [US2] Write unit test in tests/Phi4WeatherAgent.Agent.Tests/Services/OpenMeteoAllergenClientTests.cs for successful pollen data parsing
- [ ] T106 [P] [US2] Write unit test for Europe-only constraint handling (non-EU location)
- [ ] T107 [P] [US2] Write unit test for seasonal availability handling (null/zero data)
- [ ] T108 [P] [US2] Write unit test for partial pollen data (missing categories)
- [ ] T109 [P] [US2] Write unit test in Tools/AllergenToolTests.cs for severity mapping (grains/m³ to enum)
- [ ] T110 [P] [US2] Write bUnit component test in tests/Phi4WeatherAgent.Web.Tests/Components/Weather/AllergenCardTests.cs for severity color coding
- [ ] T111 [P] [US2] Write bUnit component test for health recommendation display
- [ ] T112 [US2] Write Playwright E2E test in WeatherQueryTests.cs for "What are the pollen levels in Austin?" query
- [ ] T113 [US2] Write Playwright E2E test for Europe-only constraint error message

**Checkpoint**: User Stories 1 AND 2 both work independently

---

## Phase 5: User Story 3 - Multi-Day Planning (Priority: P3)

**Goal**: Users can compare weather across multiple days for trip/event planning

**Independent Test**: Ask "Plan my weekend in Denver" and verify Saturday vs Sunday weather comparison

### Enhanced System Prompt

- [ ] T114 [US3] Update Chat.razor system prompt to include multi-day comparative analysis instructions ("When user asks about multiple days, compare conditions and recommend best day for activities")
- [ ] T115 [US3] Add natural language examples to system prompt ("weekend" = Saturday+Sunday, "next week" = next 7 days)

### Conversation Context Enhancement

- [ ] T116 [US3] Enhance AgentService conversation context to persist last queried location across messages (in-memory per Blazor circuit)
- [ ] T117 [US3] Implement follow-up query handling ("What about tomorrow?" uses previous location context)
- [ ] T118 [US3] Add context reset on page refresh (clear Dictionary<CircuitId, ConversationContext>)
- [ ] T119 [US3] Add telemetry for context usage (track follow-up query count, context hit rate)

### Multi-Day UI Enhancement

- [ ] T120 [US3] Update WeatherCard.razor to support multi-card layout (horizontal scrollable carousel on mobile per FR-011)
- [ ] T121 [US3] Update WeatherCard.razor for desktop side-by-side grid layout with CSS Grid (auto-fit columns)
- [ ] T122 [US3] Add comparative analysis text in ChatMessageItem.razor ("Saturday: Clear skies, ideal for hiking. Sunday: Rain expected, indoor activities recommended.")

### Tests for User Story 3

- [ ] T123 [P] [US3] Write unit test in tests/Phi4WeatherAgent.Agent.Tests/Services/AgentServiceTests.cs for conversation context persistence
- [ ] T124 [P] [US3] Write unit test for follow-up query location resolution
- [ ] T125 [P] [US3] Write unit test for context reset on circuit disconnect
- [ ] T126 [P] [US3] Write bUnit component test in tests/Phi4WeatherAgent.Web.Tests/Components/Weather/WeatherCardTests.cs for multi-card responsive layout
- [ ] T127 [US3] Write Playwright E2E test in WeatherQueryTests.cs for "Plan my weekend in Denver" with Saturday+Sunday cards
- [ ] T128 [US3] Write Playwright E2E test for follow-up query "What about Monday?" using previous location

**Checkpoint**: All user stories 1, 2, AND 3 work independently

---

## Phase 6: User Story 4 - Accessibility (Priority: P2)

**Goal**: Screen reader users can complete full query workflow with keyboard-only navigation

**Independent Test**: Use NVDA/JAWS to navigate chat interface, submit query, and hear weather card content

### Keyboard Navigation

- [ ] T129 [P] [US4] Add skip link in src/Phi4WeatherAgent.Web/Components/Layout/MainLayout.razor ("Skip to main content" visible on focus)
- [ ] T130 [P] [US4] Ensure logical tab order (header → chat input → messages → footer) in MainLayout.razor
- [ ] T131 [P] [US4] Add 3px focus indicators to all interactive elements (buttons, inputs) with --color-focus #0056b3 token
- [ ] T132 [P] [US4] Implement Escape key handler to close modals/popovers in Chat.razor
- [ ] T133 [P] [US4] Test keyboard trap prevention (users can Tab out of all components)

### Screen Reader Support

- [ ] T134 [P] [US4] Add role="main" to chat container in Chat.razor
- [ ] T135 [P] [US4] Add aria-label="Message input" to chat input field in ChatInput.razor
- [ ] T136 [P] [US4] Add aria-label="Send message" to submit button in ChatInput.razor
- [ ] T137 [P] [US4] Add aria-live="polite" to WeatherCard and AllergenCard components for dynamic content announcements
- [ ] T138 [P] [US4] Add aria-describedby to error messages linking to retry button
- [ ] T139 [P] [US4] Add aria-label to weather icons ("Sunny icon", "Rainy icon") in WeatherCard.razor

### Color Contrast Validation

- [ ] T140 [P] [US4] Validate all text meets WCAG AA 4.5:1 contrast ratio using axe DevTools (text-primary #1a1a1a on white = 16.94:1)
- [ ] T141 [P] [US4] Validate UI components (buttons, focus indicators) meet WCAG AA 3:1 contrast ratio
- [ ] T142 [P] [US4] Validate error states (color-error #c41e3a on white = 5.95:1) meet contrast requirements

### Accessibility Tests

- [ ] T143 [P] [US4] Write Playwright E2E test in tests/Phi4WeatherAgent.E2E.Tests/AccessibilityTests.cs for keyboard-only workflow (Tab navigation, Enter to submit)
- [ ] T144 [P] [US4] Write Playwright E2E test for screen reader announcements using aria-snapshot
- [ ] T145 [P] [US4] Write Playwright E2E test for focus indicator visibility on all interactive elements
- [ ] T146 [US4] Run axe DevTools audit in Playwright test and assert zero violations (await expect(page).toHaveNoViolations())
- [ ] T147 [US4] Manual test with NVDA (Windows) to verify weather card content read aloud ("Weather for Seattle: 52 degrees, partly cloudy...")
- [ ] T148 [US4] Manual test with JAWS (Windows) to verify live region announcements

**Checkpoint**: All 4 user stories independently functional with WCAG AA compliance

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements affecting multiple user stories, documentation, and final validation

### Error Handling & Resilience

- [ ] T149 [P] Add global error boundary in App.razor with user-friendly error page
- [ ] T150 [P] Implement resource monitoring in AgentService (log warning if >500MB, error if >1GB per FR-043)
- [ ] T151 [P] Add model inference timeout (30s CancellationToken) with error "Query timed out. Try simpler question." per FR-042
- [ ] T152 [P] Add combined context token limit handling (truncate oldest turns if exceeding 131K input context per FR-041)
- [ ] T153 [P] Add input length limit enforcement (2000 char max) in ChatInput.razor with visible character counter "[X]/2000" displayed below textarea, disable submit button when limit exceeded, per FR-041
- [ ] T154 [P] Add debouncing to chat input (300ms delay after typing stops, submit immediately on Enter) per FR-037

### Security Hardening

- [ ] T155 [P] Add Content Security Policy header in Web/Program.cs (`script-src 'self'; object-src 'none';`) per FR-045
- [ ] T156 [P] Add input sanitization in GeocodeTool (reject `<script>`, `<iframe>`, `on*` attributes) per FR-046
- [ ] T157 [P] Verify Blazor Server escapes all output by default (use @variable, not @Html.Raw) per FR-045
- [ ] T158 [P] Add SQL/command injection prevention validation (reject control characters) per FR-046
- [ ] T159 Run OWASP ZAP security scan and verify zero high/critical findings per SC-017

### Performance Optimization

- [ ] T160 [P] Add BenchmarkDotNet test in tests/Phi4WeatherAgent.Agent.Tests/Benchmarks/AgentInitializationBenchmark.cs to measure <2s initialization per SC-007
- [ ] T161 [P] Optimize Blazor bundle size (tree-shaking, remove unused template assets)
- [ ] T162 [P] Add memory profiling test (verify no leaks after 100 query cycles using dotMemory)
- [ ] T163 [P] Measure query latency (ensure <5s p95 end-to-end per SC-001) and add telemetry metrics

### Documentation

- [ ] T164 [P] Create README.md with architecture diagram (Aspire + Blazor + Agent Framework + OpenMeteo) per SC-020
- [ ] T165 [P] Document setup scripts usage in README (setup-windows.ps1, setup-macos.sh, setup-linux.sh)
- [ ] T166 [P] Create quickstart.md in project root with 30-second setup instructions (clone, dotnet run, open browser) and natural language query examples per FR-047
- [ ] T167 [P] Document cross-platform differences (Foundry Local vs Ollama) in README
- [ ] T168 [P] Add XML doc comments to all public APIs in Agent project per SC-020
- [ ] T169 [P] Generate API documentation with docfx (or verify via compiler warnings)

### Convenience Scripts

- [ ] T170 [P] Create scripts/run.ps1 for Windows (checks if setup completed, prompts to run setup-windows.ps1 if missing, then dotnet run --project AppHost) per FR-049
- [ ] T171 [P] Create scripts/run.sh for macOS/Linux (checks if setup completed, prompts to run setup script if missing, then dotnet run --project AppHost) per FR-049
- [ ] T172 [P] Add --force-update flag to setup scripts to re-download Phi-4 model per FR-050
- [ ] T173 [P] Add version checks in setup scripts (detect .NET 10 SDK, warn if preview outdated) per FR-050

### Final Validation

- [ ] T174 Cross-platform build test (verify dotnet build succeeds on Windows, macOS, Linux) per SC-003
- [ ] T175 Run all unit tests (Agent.Tests) and verify >80% code coverage per SC-010
- [ ] T176 Run all bUnit component tests (Web.Tests) and verify coverage
- [ ] T177 Run all Playwright E2E tests (E2E.Tests) covering all 4 user stories per SC-009
- [ ] T178 Verify Aspire Dashboard displays HTTP traces, model inference spans, custom spans per SC-011
- [ ] T179 Run quickstart.md validation on fresh Windows 11 install per SC-012
- [ ] T180 Run quickstart.md validation on fresh macOS Sonoma install per SC-012
- [ ] T181 Run quickstart.md validation on fresh Ubuntu 22.04 LTS install per SC-012
- [ ] T182 Verify zero paid services via dependency scan + runtime network traffic inspection per SC-008
- [ ] T183 Final Constitution check (all 11 principles pass) before feature completion

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - **BLOCKS all user stories**
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - US1 (P1 MVP): Can start immediately after Foundational
  - US2 (P2 Allergen): Can start after Foundational (independent of US1 but may integrate)
  - US3 (P3 Multi-Day): Can start after Foundational (independent but uses US1's WeatherCard)
  - US4 (P2 Accessibility): Can start after Foundational (cross-cuts all stories, best parallelized)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - **NO dependencies on other stories**
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - **NO dependencies on other stories** (shares Location model from Foundation)
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Uses WeatherCard from US1 but independently testable
- **User Story 4 (P2)**: Can start after Foundational (Phase 2) - Cross-cuts all stories, can be implemented in parallel with US1-US3

### Within Each User Story

**User Story 1 (Basic Weather)**:
1. Models (T022-T024) → HTTP Clients (T025-T031) → MCP Tools (T046-T051) → Implementations (T052-T062) → UI (T063-T074) → Tests (T075-T086)
2. Parallel: T046-T049 (GeocodeTool + WeatherTool), T063-T064 (WeatherCard + SVG icons), T075-T083 (unit/component tests)

**User Story 2 (Allergen Info)**:
1. MCP Tool (T087-T089) → Implementation (T090-T096) → UI (T097-T101) → Integration (T102-T104) → Tests (T105-T113)
2. Parallel: T087-T088 (AllergenTool creation), T097-T098 (AllergenCard + colors), T105-T111 (tests)

**User Story 3 (Multi-Day)**:
1. Prompts (T114-T115) → Context (T116-T119) → UI (T120-T122) → Tests (T123-T128)
2. Parallel: T114-T115 (prompt updates), T123-T125 (context tests)

**User Story 4 (Accessibility)**:
1. Keyboard (T129-T133) → Screen Reader (T134-T139) → Contrast (T140-T142) → Tests (T143-T148)
2. Parallel: T129-T133 (keyboard nav), T134-T139 (ARIA labels), T140-T142 (contrast validation), T143-T145 (automated tests)

### Parallel Opportunities

**Setup Phase (Phase 1)**:
- T002-T006 (config files) can all run in parallel
- T011-T012 (test projects) can run in parallel

**Foundational Phase (Phase 2)**:
- T018-T020 (OpenTelemetry config) can run in parallel
- T022-T024 (domain models) can run in parallel
- After HTTP clients exist: T036-T044 (template cleanup) can run in parallel

**User Story 1**:
- T046-T049 (MCP tools creation) can run in parallel
- T063-T064 (UI components + icons) can run in parallel
- T075-T083 (unit/component tests) can run in parallel

**User Story 2**:
- T087-T088 (AllergenTool + docs) can run in parallel
- T097-T098 (AllergenCard + colors) can run in parallel
- T105-T111 (tests) can run in parallel

**User Story 4**:
- T129-T133 (keyboard nav) can run in parallel
- T134-T139 (ARIA labels) can run in parallel
- T140-T142 (contrast checks) can run in parallel
- T143-T145 (automated tests) can run in parallel

**Polish Phase (Phase 7)**:
- T149-T154 (error handling) can run in parallel
- T155-T158 (security) can run in parallel
- T160-T163 (performance) can run in parallel
- T164-T169 (documentation) can run in parallel
- T170-T173 (scripts) can run in parallel

---

## Parallel Example: User Story 1

```bash
# After Foundational phase completes, launch all MCP tool tasks together:
Task T046: Create GeocodeTool.cs
Task T047: Add XML docs to GeocodeTool
Task T048: Create WeatherTool.cs
Task T049: Add XML docs to WeatherTool

# Later, launch all UI component tasks together:
Task T063: Create WeatherCard.razor
Task T064: Add SVG weather icons

# Finally, launch all unit/component tests together:
Task T075: Unit test - OpenMeteoGeocodeClient success
Task T076: Unit test - geocoding zero results
Task T077: Unit test - OpenMeteoWeatherClient success
Task T078: Unit test - partial forecast data
Task T079: Unit test - GeocodeTool coordinate detection
Task T080: Unit test - GeocodeTool input validation
Task T081: bUnit test - WeatherCard current conditions
Task T082: bUnit test - WeatherCard 7-day forecast
Task T083: bUnit test - WeatherCard loading skeleton
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete **Phase 1: Setup** (T001-T012)
2. Complete **Phase 2: Foundational** (T013-T045) - **CRITICAL gate**
3. Complete **Phase 3: User Story 1** (T046-T086)
4. **STOP and VALIDATE**: 
   - Run E2E test "What's the weather in Seattle?"
   - Verify weather card displays correctly
   - Test error handling with retry button
   - Check Aspire Dashboard telemetry
5. Deploy/demo if ready

**MVP Scope**: Setup + Foundational + US1 = ~86 tasks (47% of total)

### Incremental Delivery

1. **Foundation** (T001-T045) → Build succeeds, template cleaned, Aspire configured
2. **MVP** (T046-T086) → Weather queries working end-to-end
   - Test independently: "What's the weather in Seattle?"
   - Deploy/Demo: Users can query current weather + 7-day forecast
3. **Allergen Info** (T087-T113) → Pollen data added
   - Test independently: "What are the pollen levels in Austin?"
   - Deploy/Demo: Health-conscious users get allergen alerts
4. **Multi-Day Planning** (T114-T128) → Comparative analysis added
   - Test independently: "Plan my weekend in Denver"
   - Deploy/Demo: Trip planners compare multiple days
5. **Accessibility** (T129-T148) → WCAG AA compliance
   - Test independently: Keyboard-only workflow with screen reader
   - Deploy/Demo: Inclusive design for visually impaired users
6. **Polish** (T149-T183) → Production-ready hardening
   - Final validation: All tests pass, security scans clean, docs complete

### Parallel Team Strategy

With multiple developers:

1. **Team completes Setup + Foundational together** (T001-T045)
2. **Once Foundational done, work in parallel**:
   - **Developer A**: User Story 1 (T046-T086) - Weather queries MVP
   - **Developer B**: User Story 2 (T087-T113) - Allergen info
   - **Developer C**: User Story 4 (T129-T148) - Accessibility (cross-cuts all)
3. **After US1 + US2 complete**:
   - **Developer A**: User Story 3 (T114-T128) - Multi-day planning (needs US1's WeatherCard)
4. **Finally, team on Polish** (T149-T183) - Security, performance, docs

**Estimated Effort**:
- Setup: 12 tasks (~1 day)
- Foundational: 33 tasks (~3 days) - **CRITICAL PATH**
- US1 (MVP): 41 tasks (~4 days)
- US2: 27 tasks (~2 days)
- US3: 15 tasks (~1.5 days)
- US4: 20 tasks (~2 days)
- Polish: 35 tasks (~3 days)

**Total**: 183 tasks (~16-17 days solo, ~8-9 days with 2 developers after Foundation)

---

## Notes

- **[P] tasks**: Different files, no dependencies, can run in parallel
- **[Story] label**: Maps task to specific user story for traceability
- **Each user story**: Independently completable and testable
- **Tests first**: Write tests (T075-T086, etc.) and verify they fail before implementing
- **Commit frequency**: After each task or logical group (e.g., after T046-T051 MCP tools)
- **Stop at checkpoints**: Validate story independently before moving to next priority
- **Constitution compliance**: All tasks align with 11 principles (validated in plan.md)
- **Coverage target**: >80% code coverage (SC-010) validated in Phase 7
- **WCAG AA**: All UI tasks include accessibility requirements (FR-015, US4)

---

**Success Metrics** (tracked in Phase 7):
- ✅ SC-001: Weather query <5s (p95) measured via telemetry
- ✅ SC-002: Location disambiguation >80% accuracy via E2E tests
- ✅ SC-003: Cross-platform builds (Windows/macOS/Linux CI)
- ✅ SC-004-005: WCAG AA compliance (axe DevTools + manual testing)
- ✅ SC-006: Polly retry recovery >95% (integration tests)
- ✅ SC-007: Agent init <2s (BenchmarkDotNet)
- ✅ SC-008: Zero paid services (dependency scan + traffic inspection)
- ✅ SC-009-010: E2E tests cover all 4 stories + >80% unit coverage
- ✅ SC-011: Aspire Dashboard telemetry (traces, metrics, logs)
- ✅ SC-012: Setup scripts succeed on fresh OS installs
