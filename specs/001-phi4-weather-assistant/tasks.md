# Tasks: Phi-4 Weather Assistant

**Input**: Design documents from `/specs/001-phi4-weather-assistant/`

## Phase 1: Setup (Shared Infrastructure)

Purpose: Establish repo scaffolding, documentation, and base projects so all platforms share the same starting point.

- [X] T001 Finalize template customization scope in `specs/001-phi4-weather-assistant/template-customization.md`
- [X] T002 Add `.NET 10` pin via `global.json` at repo root
- [X] T003 Centralize package versions in `Directory.Build.props`
- [X] T004 Commit C# formatting guidance in `.editorconfig`
- [X] T005 Draft project overview + architecture diagram in `README.md`
- [X] T006 Add Windows bootstrapper `scripts/setup-windows.ps1`
- [X] T007 Add macOS bootstrapper `scripts/setup-macos.sh`
- [X] T008 Add Linux bootstrapper `scripts/setup-linux.sh`
- [X] T009 Capture Phase 0 research findings in `specs/001-phi4-weather-assistant/research.md` (5 sections: Agent Framework patterns, Aspire 13 orchestration, OpenMeteo API contracts, Polly resilience policies, WCAG 2.1 AA guidelines)
- [X] T010 Define domain entities in `specs/001-phi4-weather-assistant/data-model.md` (Location, WeatherData with CurrentConditions/DailyForecast, AllergenData with pollen levels, ChatMessage with structured data)
- [X] T011 Document MCP + HTTP contracts in `specs/001-phi4-weather-assistant/contracts/` (GeocodeTool signature with locationName param, WeatherTool with lat/lon/forecastDays, AllergenTool with lat/lon for Air Quality API pollen data)
- [X] T012 Write developer quick start in `specs/001-phi4-weather-assistant/quickstart.md` (include example natural language queries: location-based, time-based, multi-day, allergen, planning patterns)
- [X] T013 Rename template project to `src/Phi4WeatherAgent.Web/Phi4WeatherAgent.Web.csproj` (use `dotnet new aichatweb --name Phi4WeatherAgent.Web` WITHOUT --provider flag to avoid Ollama lock-in; defer IChatClient config to T030)
- [X] T014 Remove search/ingestion artifacts (Services/SemanticSearch.cs, Services/Ingestion/, wwwroot/Data/, wwwroot/lib/pdfjs-dist, pdf_viewer, markdown_viewer, dompurify)
- [X] T015 Create `src/Phi4WeatherAgent.ServiceDefaults/Phi4WeatherAgent.ServiceDefaults.csproj`
- [X] T016 Create `src/Phi4WeatherAgent.AppHost/Phi4WeatherAgent.AppHost.csproj`
- [X] T017 Create `src/Phi4WeatherAgent.Agent/Phi4WeatherAgent.Agent.csproj`
- [X] T018 Create test projects `tests/Phi4WeatherAgent.Agent.Tests`, `tests/Phi4WeatherAgent.Web.Tests`, `tests/Phi4WeatherAgent.E2E.Tests`
- [X] T019 Generate solution file `phi4-weather-agent-dotnet.sln` referencing all projects
- [X] T020 Add CI workflow `.github/workflows/ci.yml` (Windows, macOS, Linux matrix)

---

## Phase 2: Foundational (Blocking Prerequisites)

Purpose: Shared infrastructure that must exist before any user story work.

- [X] T021 Implement Aspire defaults in `src/Phi4WeatherAgent.ServiceDefaults/Extensions.cs`
- [X] T022 Configure AppHost orchestration + Foundry Local/Ollama detection in `src/Phi4WeatherAgent.AppHost/Program.cs`
- [X] T023 Wire Agent backend host in `src/Phi4WeatherAgent.Agent/Program.cs` (Serilog, Swagger, MCP registration placeholders)
- [X] T024 Add `Models/Location.cs`
- [X] T025 Add `Models/WeatherData.cs`
- [X] T026 Add `Models/AllergenData.cs`
- [X] T027 Add Polly-backed HTTP client registrations in `src/Phi4WeatherAgent.Agent/Services/HttpClientRegistration.cs`
- [X] T028 Implement `Services/Options/OpenMeteoOptions.cs`
- [X] T029 Add `src/Phi4WeatherAgent.Agent/Services/AgentService.cs` with conversation context helpers
- [X] T030 Configure Microsoft.Extensions.AI provider (Foundry Local vs Ollama) in `src/Phi4WeatherAgent.Web/Program.cs` (use AppHost platform detection for OS-specific model hosting: Foundry Local for Windows/macOS, Ollama for Linux)
- [X] T031 Add shared UI constants (colors, typography) for WCAG AA in `src/Phi4WeatherAgent.Web/wwwroot/css/app.css` (color contrast ≥4.5:1 for normal text, ≥3:1 for large text, visible focus indicators)
- [X] T032 Scaffold Playwright project config in `tests/Phi4WeatherAgent.E2E.Tests/playwright.config.ts` (target browsers: Chromium/Firefox/WebKit, viewport 1920x1080, trace-on-failure, base URL for AppHost, 30s timeout)

---

## Phase 3: User Story 1 – Basic Weather Query (Priority P1)

Goal: MVP weather query path with cards, MCP tools, and automated tests.

**Independent Test**: Ask "What's the weather in Seattle?" and verify weather card renders current + 7-day forecast.

- [X] T033 [P] [US1] Implement `Services/OpenMeteoGeocodeClient.cs`
- [X] T034 [P] [US1] Implement `Services/OpenMeteoWeatherClient.cs`
- [X] T035 [US1] Register typed HTTP clients + resiliency in `src/Phi4WeatherAgent.Agent/Program.cs`
- [X] T036 [US1] Build `Tools/GeocodeTool.cs` using Agent Framework MCP annotations
- [X] T037 [US1] Build `Tools/WeatherTool.cs`
- [X] T038 [US1] Render structured cards via `Components/Weather/WeatherCard.razor`
- [X] T039 [US1] Customize `Components/Chat/ChatMessageItem.razor` to display weather cards + icons
- [X] T040 [US1] Update `Pages/Chat.razor` system prompt + suggestion buttons for weather intents (system prompt: clarify available data - weather/pollen, always ask for location if missing, handle ambiguous locations)
- [X] T041 [US1] Extend `Services/AgentService.cs` with weather query orchestration flow (use in-memory conversation context with List<ChatMessage> per SignalR session, no persistence per zero-cost principle, clear context on disconnect)
- [X] T042 [P] [US1] Add unit tests for Geocode + Weather clients in `tests/Phi4WeatherAgent.Agent.Tests/Services`
- [X] T043 [P] [US1] Add MCP tool tests in `tests/Phi4WeatherAgent.Agent.Tests/Tools`
- [X] T044 [P] [US1] Add bUnit coverage for WeatherCard in `tests/Phi4WeatherAgent.Web.Tests/Components/Weather`
- [X] T045 [US1] Add Playwright scenario "basic weather query" in `tests/Phi4WeatherAgent.E2E.Tests/WeatherQueryTests.cs`

---

## Phase 4: User Story 2 – Allergen Information (Priority P2)

Goal: Surface pollen levels with severity + guidance while reusing MCP tooling.

**Independent Test**: Ask "What are the pollen levels in Austin?" and expect allergen card with severity labels.

- [X] T046 [US2] Implement `Services/OpenMeteoAllergenClient.cs` (Air Quality API endpoint: https://air-quality-api.open-meteo.com/v1/air-quality with pollen params: alder_pollen, birch_pollen, grass_pollen, mugwort_pollen, olive_pollen, ragweed_pollen; Europe only, 4-day forecast)
- [X] T047 [US2] Add `Tools/AllergenTool.cs` (MCP function for Air Quality API pollen data, grains/m³ units, severity calculation: Low/Moderate/High/VeryHigh)
- [X] T048 [US2] Create `Components/Weather/AllergenCard.razor` with severity badges
- [X] T049 [US2] Update `Components/Chat/ChatMessageItem.razor` to render allergen cards
- [X] T050 [US2] Extend `Services/AgentService.cs` with allergen command handler
- [X] T051 [P] [US2] Add unit tests for allergen client/tool in `tests/Phi4WeatherAgent.Agent.Tests`
- [X] T052 [P] [US2] Add bUnit tests for AllergenCard
- [X] T053 [US2] Add Playwright "allergen advisory" scenario in `tests/Phi4WeatherAgent.E2E.Tests/WeatherQueryTests.cs`

---

## Phase 5: User Story 3 – Multi-Day Planning (Priority P3)

Goal: Provide comparative planning insights for trips/events using existing weather data.

**Independent Test**: Ask "Plan my weekend in Denver" and verify side-by-side day comparison with recommendations.

- [ ] T054 [US3] Create `Components/Weather/WeatherComparison.razor` for multi-day cards
- [ ] T055 [US3] Enhance `Services/AgentService.cs` to aggregate weekend/day-range summaries
- [ ] T056 [US3] Update system prompt in `Pages/Chat.razor` to encourage planning responses
- [ ] T057 [US3] Persist conversation context cues (preferred location, time range) in `Services/AgentService.cs`
- [ ] T058 [P] [US3] Add unit tests for planning heuristics in `tests/Phi4WeatherAgent.Agent.Tests/Services`
- [ ] T059 [US3] Add Playwright "weekend planner" scenario

---

## Phase 6: User Story 4 – Accessibility for Screen Readers (Priority P2)

Goal: WCAG 2.1 AA compliance for chat workflow + weather cards.

**Independent Test**: Navigate entire experience with keyboard + NVDA/JAWS and hear weather summaries.

- [ ] T060 [US4] Add ARIA roles/labels to chat components (`Components/Chat/*.razor`) (role="main", aria-label for inputs, aria-live for message updates)
- [ ] T061 [US4] Implement keyboard focus management and skip links in `Components/Layout/MainLayout.razor` (all interactive elements reachable via Tab, no keyboard traps, visible focus indicators ≥3:1 contrast)
- [ ] T062 [US4] Add live region announcements for new responses in `Pages/Chat.razor` (aria-live="polite" for weather card content, screen reader reads complete summary)
- [ ] T063 [US4] Ensure color contrast tokens meet ≥4.5:1 in `wwwroot/css/app.css` (normal text ≥4.5:1, large text ≥3:1, focus indicators ≥3:1)
- [ ] T064 [P] [US4] Add bUnit + axe automated accessibility tests in `tests/Phi4WeatherAgent.Web.Tests` (zero high/critical violations, ARIA labels present, semantic HTML validation)
- [ ] T065 [US4] Add Playwright keyboard + screen reader workflow test (use `playwright-accessibility` helpers)

---

## Final Phase: Polish & Cross-Cutting

Goal: Production readiness (observability, performance, docs, licensing).

- [ ] T066 Add BenchmarkDotNet harness in `tests/Phi4WeatherAgent.Agent.Tests/Benchmarks/AgentStartupBenchmarks.cs`
- [ ] T067 Wire Aspire OpenTelemetry exporters + dashboards in `src/Phi4WeatherAgent.AppHost/Program.cs`
- [ ] T068 Add dependency license scan step to `.github/workflows/ci.yml` (scan for GPL/AGPL licenses AND forbidden packages: fail build if Microsoft.SemanticKernel* detected in transitive dependencies per Constitution Principle III)
- [ ] T069 Document troubleshooting + example queries in `README.md`
- [ ] T070 Publish platform-specific screenshots/gifs in `README.md`
- [ ] T071 Add manual accessibility checklist results to `specs/001-phi4-weather-assistant/quickstart.md` (NVDA/JAWS screen reader testing: keyboard-only workflow completion <2min, weather card content read aloud, live region announcements working)
- [ ] T072 Verify setup scripts on clean VMs (Windows/macOS/Linux) and record issues in `README.md`

---

## Dependencies

1. Phase 1 must finish before Phase 2 (foundation needs projects + scripts).
2. Phase 2 must finish before any user story (tools need infrastructure).
3. Phase 3 (US1) precedes US2/US3 because allergen + planning rely on base weather flow.
4. Phase 4 (US2) must complete before Phase 5 (US3) for shared components.
5. Phase 6 (US4) can start after Phase 3 (needs weather UI present) and can run parallel with Phases 4–5 after shared components are stable.
6. Final Phase polishes after all user stories deliverables land.

## Parallel-Friendly Tasks

- T033 & T034 (HTTP clients) can be implemented independently once Phase 2 completes.
- T042 & T043 (MCP tool tests) can run parallel to UI work (T038–T040).
- T046–T048 (Allergen client/tool/card) can run in parallel once US1 is merged.
- T060–T063 (accessibility styling/ARIA) can run parallel with P3 multi-day enhancements.
- T066–T068 (benchmarks, telemetry, license scan) can run parallel late in the cycle.

## Implementation Strategy

1. **MVP First**: Deliver Phase 3 (US1) end-to-end to validate Agent Framework + MCP tooling early.
2. **Incremental Enhancements**: Layer allergen data (US2) and planning insights (US3) using the same MCP abstractions to minimize rework.
3. **Accessibility in Parallel**: Start Phase 6 as soon as core UI stabilizes to avoid regressions late in the cycle.
4. **Observability + Performance**: Use Final Phase tasks to capture telemetry + benchmarks once most logic exists, ensuring data reflects real workloads.
