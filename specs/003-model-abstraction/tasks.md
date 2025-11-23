# Implementation Tasks: Model Abstraction and Domain-Agnostic Project Naming

**Feature**: 003-model-abstraction
**Branch**: `003-model-abstraction`
**Generated**: 2025-11-20
**Total Tasks**: 114 (111 original + 3 automated test tasks for SC-012/SC-013)
**Total LOC**: ~1680 lines (1630 original + 50 for automated validation tests)

---

## Phase 1: Setup & Dependencies

**Goal**: Prepare development environment with required packages and validate build.

**Test Criteria**: Solution builds successfully with new Microsoft.Agents.AI package, all existing tests pass.

### Tasks

- [X] T001 Install Microsoft.Agents.AI package in Directory.Build.props
- [X] T002 [P] Verify .NET 10 SDK version in global.json (already pinned to 10.0.100+)
- [X] T003 [P] Add openmeteo_sdk v1.23.0 package reference to future OpenMeteo project placeholder
- [X] T004 Build solution and verify no package conflicts
- [X] T005 Run existing test suite to establish baseline (all tests should pass)

---

## Phase 2: Foundational Interfaces & Models

**Goal**: Create core abstractions that all subsequent phases depend on.

**Test Criteria**: Interfaces compile, can be registered in DI, unit tests verify contract behavior.

### Tasks

- [X] T006 [P] Create IToolInvocationHandler interface in src/LocalAIAgent.Agent/Interfaces/IToolInvocationHandler.cs
- [X] T007 [P] Create IPromptProvider interface in src/LocalAIAgent.Agent/Interfaces/IPromptProvider.cs
- [X] T008 [P] Create ModelConfiguration model in src/LocalAIAgent.Agent/Models/ModelConfiguration.cs
- [X] T009 [P] Create ProviderType enum in src/LocalAIAgent.Agent/Models/ProviderType.cs
- [X] T010 Add Options pattern configuration binding in src/LocalAIAgent.Agent/Program.cs
- [X] T011 [P] Create unit tests for ModelConfiguration validation in tests/LocalAIAgent.Agent.Tests/Models/ModelConfigurationTests.cs
- [X] T012 [P] Create JSON Schema for appsettings.json in specs/003-model-abstraction/contracts/appsettings.schema.json

---

## User Story 1: Switch AI Models Without Code Changes (P1)

**Goal**: Enable configuration-driven model selection with zero recompilation.

**Independent Test**: Change `AI:DefaultModel` in appsettings.json from "phi-4-mini" to "qwen2.5-vl-3b", restart app, verify Qwen model loads without code changes.

### Tasks

- [X] T013 [US1] Create multi-model configuration structure in src/LocalAIAgent.Web/appsettings.json
- [X] T014 [US1] Implement configuration validation with fail-fast behavior in src/LocalAIAgent.Agent/Services/ConfigurationValidator.cs
- [X] T015 [US1] Add environment variable substitution for API keys (${ENV_VAR_NAME} syntax) in src/LocalAIAgent.Agent/Services/ConfigurationProvider.cs
- [X] T016 [US1] Implement configuration precedence logging (env vars > appsettings) in src/LocalAIAgent.Agent/Services/ConfigurationProvider.cs
- [X] T017 [US1] Create ChatClientAgent factory with conditional handler registration in src/LocalAIAgent.Agent/Services/ChatAgentFactory.cs
- [X] T018 [US1] Add handler registration validation with clear error messages in src/LocalAIAgent.Agent/Services/ChatAgentFactory.cs
- [X] T019 [US1] Implement DefaultModel resolution logic in src/LocalAIAgent.Agent/Services/ModelResolver.cs
- [X] T020 [US1] Add invalid DefaultModel reference error handling (list available models) in src/LocalAIAgent.Agent/Services/ModelResolver.cs
- [X] T021 [US1] Create integration test for Phi-4 Mini → Qwen model switching in tests/LocalAIAgent.Agent.Tests/Integration/ModelSwitchingTests.cs (PASS criteria: 1) Configuration loads without errors, 2) Correct IChatClient implementation instantiated, 3) First message returns valid response)
- [X] T022 [US1] Create integration test for cloud model configuration structure (deferred implementation) in tests/LocalAIAgent.Agent.Tests/Integration/CloudModelConfigTests.cs (PASS criteria: 1) Configuration validates successfully, 2) Handler registration skipped for native tool models, 3) No API calls made)

---

## User Story 2: Configuration-Driven Prompt Management (P2)

**Goal**: Load system prompts from markdown files with model-specific behavior via ToolInvocationStrategy.

**Independent Test**: Update prompts/weather-assistant.md, restart app, verify new prompt loaded; change ToolInvocationStrategy in config, verify correct handler applied.

### Tasks

- [X] T023 [US2] Create prompts/ directory structure at repository root
- [X] T024 [US2] Migrate hardcoded system prompt to prompts/weather-assistant.md
- [X] T025 [US2] Implement PromptProvider with markdown file loading in src/LocalAIAgent.Agent/Services/PromptProvider.cs
- [X] T026 [US2] Add prompt file path validation with fail-fast behavior in src/LocalAIAgent.Agent/Services/PromptProvider.cs
- [X] T027 [US2] Implement missing prompt file error handling (show attempted path + working directory) in src/LocalAIAgent.Agent/Services/PromptProvider.cs
- [X] T028 [US2] Register IPromptProvider in DI container in src/LocalAIAgent.Agent/Program.cs
- [X] T029 [US2] Update ChatClientAgent initialization to use IPromptProvider in src/LocalAIAgent.Agent/Services/ChatAgentService.cs
- [X] T030 [US2] Create unit tests for PromptProvider file loading in tests/LocalAIAgent.Agent.Tests/Services/PromptProviderTests.cs
- [X] T031 [US2] Create integration test for prompt file updates (restart required) in tests/LocalAIAgent.Agent.Tests/Integration/PromptUpdateTests.cs

---

## User Story 3: Pluggable Tool Invocation Handlers (P2)

**Goal**: Implement interface-based handlers for model-specific tool parsing with conditional application.

**Independent Test**: Inspect ChatClientAgent middleware pipeline, verify FunctoolsHandler present when ToolInvocationStrategy="Functools", absent when null.

### Tasks

- [X] T032 [US3] Convert FunctoolsChatClient decorator to FunctoolsHandler implementing IToolInvocationHandler in src/LocalAIAgent.Agent/Handlers/FunctoolsHandler.cs
- [X] T033 [US3] Preserve existing functools parsing logic in FunctoolsHandler
- [X] T034 [US3] Preserve security audit logging in FunctoolsHandler
- [X] T035 [US3] Preserve tool whitelist validation in FunctoolsHandler
- [X] T036 [US3] Preserve rate limiting in FunctoolsHandler
- [ ] T036a [US3] [OPTIONAL - Extensibility demonstration; SC-003 already validated] Create ReActJSONHandler implementing IToolInvocationHandler for models using ReAct JSON format (example handler) in src/LocalAIAgent.Agent/Handlers/ReActJSONHandler.cs
- [X] T037 [US3] Implement keyed service registration for IToolInvocationHandler in src/LocalAIAgent.Agent/Program.cs
- [X] T038 [US3] Add conditional handler application based on ToolInvocationStrategy in src/LocalAIAgent.Agent/Services/ChatAgentFactory.cs
- [X] T039 [US3] Implement native tool model bypass (no handler when ToolInvocationStrategy null) in src/LocalAIAgent.Agent/Services/ChatAgentFactory.cs
- [X] T040 [US3] Add performance warning when handler applied to native tool model in src/LocalAIAgent.Agent/Services/ChatAgentFactory.cs
- [X] T041 [US3] Create unit tests for FunctoolsHandler parsing logic in tests/LocalAIAgent.Agent.Tests/Handlers/FunctoolsHandlerTests.cs
- [X] T042 [US3] Create unit tests for handler registration failure scenarios in tests/LocalAIAgent.Agent.Tests/Services/ChatAgentFactoryTests.cs
- [X] T043 [US3] Create integration test for handler extensibility (adding ReActJSONHandler example) in tests/LocalAIAgent.Agent.Tests/Integration/HandlerExtensibilityTests.cs
- [X] T043a [US3] Create unit test for ToolInvocationStrategy → IToolInvocationHandler resolution logic in tests/LocalAIAgent.Agent.Tests/Services/HandlerResolutionTests.cs (validates keyed service lookup: null/empty strategy = no handler, "Functools" = FunctoolsHandler, invalid key = exception)

---

## Phase 3: Agent Framework Migration

**Goal**: Migrate from IChatClient to ChatClientAgent with AgentThread state management.

**Test Criteria**: All existing chat functionality works with ChatClientAgent, conversation history managed by AgentThread.

### Tasks

- [X] T044 Replace IChatClient with ChatClientAgent in src/Phi4WeatherAgent.Agent/Services/ChatAgentService.cs
- [X] T045 Update service registration for ChatClientAgent with conditional middleware in src/Phi4WeatherAgent.Agent/Program.cs
- [X] T046 Migrate manual conversation history to AgentThread in src/Phi4WeatherAgent.Web/Components/Pages/Chat/Chat.razor
- [X] T047 Implement AgentThread lifecycle management (creation/disposal) in src/Phi4WeatherAgent.Web/Components/Pages/Chat/Chat.razor
- [X] T048 Update tool registration from AIFunction to Agent Framework pattern (already using AIFunctionFactory.Create in Program.cs)
- [X] T049 Adapt GeocodingTools to Agent Framework pattern (Description attributes verified) in src/Phi4WeatherAgent.Tools/GeocodingTools.cs
- [X] T050 Adapt WeatherTools to Agent Framework pattern (Description attributes verified) in src/Phi4WeatherAgent.Tools/WeatherTools.cs
- [X] T051 Adapt AirQualityTools to Agent Framework pattern (Description attributes verified) in src/Phi4WeatherAgent.Tools/AirQualityTools.cs
- [ ] T052 Update all existing tests to use ChatClientAgent instead of IChatClient in tests/LocalAIAgent.Agent.Tests/ (Baseline: 172 tests total = 159 pre-migration + 13 handler tests from T041-T043; all must pass after migration)
- [ ] T053 Update all existing tests to use AgentThread instead of manual history in tests/LocalAIAgent.Agent.Tests/ (Verify conversation history maintained correctly across all 172 tests)
- [ ] T054 Create integration test for IChatClient → ChatClientAgent migration in tests/LocalAIAgent.Agent.Tests/Integration/AgentMigrationTests.cs (Validates behavioral equivalence between old and new patterns)

---

## User Story 5: Model Selection via UI Dropdown (P1)

**Goal**: Add accessible model dropdown to chat UI with locking after first message.

**Independent Test**: Open chat UI, verify dropdown shows all configured models with correct format, select model, send message, verify dropdown disabled.

### Tasks

- [X] T055 [US5] Add model dropdown component to Chat.razor in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T056 [US5] Implement dropdown population from AI:Models configuration in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T057 [US5] Format dropdown options as "Provider: model-name (endpoint-type)" in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T058 [US5] Implement default model pre-selection from DefaultModel configuration in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T059 [US5] Implement dropdown disable logic after first message sent in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T060 [US5] Implement dropdown re-enable on new session in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T061 [US5] Handle single-model configuration (dropdown shows one option but remains enabled) in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T062 [US5] Add ARIA labels (aria-label="Select AI model") in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T063 [US5] Add aria-describedby pointing to help text for dropdown behavior in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T064 [US5] Implement keyboard navigation (Tab, Enter, Arrow keys, Escape) in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T065 [US5] Add visible focus indicator with 3:1 contrast ratio minimum in src/LocalAIAgent.Web/wwwroot/css/site.css
- [X] T066 [US5] Implement multiple visual cues for disabled state (color + lock icon) in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T067 [US5] Add error state with role="alert" for dropdown population failures in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [X] T068 [US5] Handle long endpoint URLs with truncation (max 50 chars) and tooltip in src/LocalAIAgent.Web/Components/Pages/Chat.razor
- [ ] T069 [US5] Create bUnit component tests for dropdown behavior in tests/LocalAIAgent.Web.Tests/Components/ChatDropdownTests.cs
- [ ] T070 [US5] Create bUnit tests for accessibility compliance (keyboard navigation) in tests/LocalAIAgent.Web.Tests/Components/ChatAccessibilityTests.cs
- [ ] T071 [US5] Validate accessibility with axe DevTools (zero violations target) - manual test documented in specs/003-model-abstraction/quickstart.md

---

## User Story 4: Generic Project Naming (P3)

**Goal**: Rename projects and namespaces to domain-agnostic names for reusability.

**Independent Test**: Grep entire codebase for "weather" references (excluding OpenMeteo assembly), verify zero results in project/namespace names.

### Tasks

- [X] T072 [US4] Rename Phi4WeatherAgent.Agent → LocalAIAgent.Agent (project file + directory)
- [X] T073 [US4] Rename Phi4WeatherAgent.Web → LocalAIAgent.Web (project file + directory)
- [X] T074 [US4] Rename Phi4WeatherAgent.Tools → LocalAIAgent.Tools (project file + directory)
- [X] T075 [US4] Rename Phi4WeatherAgent.AppHost → LocalAIAgent.AppHost (project file + directory)
- [X] T076 [US4] Rename Phi4WeatherAgent.ServiceDefaults → LocalAIAgent.ServiceDefaults (project file + directory)
- [X] T077 [US4] Rename Phi4WeatherAgent.Agent.Tests → LocalAIAgent.Agent.Tests (project file + directory)
- [X] T078 [US4] Refactor namespaces across all files to match new project names
- [X] T079 [US4] Update solution file Phi4WeatherAgent.sln → LocalAIAgent.sln
- [X] T080 [US4] Update launch profiles in Properties/launchSettings.json
- [X] T081 [US4] Update Docker configurations (if any)
- [X] T082 [US4] Verify backward compatibility - existing tool implementations work unchanged in tests/LocalAIAgent.Agent.Tests/Integration/BackwardCompatibilityTests.cs

---

## User Story 6: Dedicated OpenMeteo Assembly (P3)

**Goal**: Extract weather tools to separate assembly with SDK encapsulation.

**Independent Test**: Verify GeocodingTools, WeatherTools, AirQualityTools in LocalAIAgent.OpenMeteo assembly, SDK types not visible in Agent project, API surface inspection passes.

### Tasks

- [X] T083 [US6] Create new LocalAIAgent.OpenMeteo class library project
- [X] T084 [US6] Add openmeteo_sdk v1.23.0 NuGet package reference to OpenMeteo project
- [X] T085 [US6] Move GeocodingTools from LocalAIAgent.Tools to LocalAIAgent.OpenMeteo/Tools/
- [X] T086 [US6] Move WeatherTools from LocalAIAgent.Tools to LocalAIAgent.OpenMeteo/Tools/
- [X] T087 [US6] Move AirQualityTools from LocalAIAgent.Tools to LocalAIAgent.OpenMeteo/Tools/
- [X] T088 [US6] Wrap openmeteo_sdk in internal service layer (no SDK types in public API) in src/LocalAIAgent.OpenMeteo/Services/OpenMeteoClient.cs
- [X] T089 [US6] Verify SDK encapsulation - tool methods return only primitives/DTOs in src/LocalAIAgent.OpenMeteo/Tools/
- [X] T090 [US6] Add project reference from Agent to OpenMeteo assembly
- [X] T091 [US6] Update tool discovery to include OpenMeteo assembly tools in src/LocalAIAgent.Agent/Services/ToolRegistry.cs
- [X] T092 [US6] Create unit tests for OpenMeteo assembly (only context where SDK types visible) in tests/LocalAIAgent.OpenMeteo.Tests/
- [ ] T093 [US6] Create API surface inspection test to verify no SDK type leakage in tests/LocalAIAgent.Agent.Tests/Integration/OpenMeteoEncapsulationTests.cs (Uses Roslyn-based analyzer to detect public openmeteo_sdk types or manual ILSpy inspection)
- [ ] T093a [US6] Migrate existing weather tool tests from Agent.Tests to OpenMeteo.Tests (Ensures test coverage maintained after extraction; verify all GeocodingTools, WeatherTools, AirQualityTools tests moved and passing)

---

## Phase 4: Bootstrap & Start Scripts

**Goal**: Update scripts to download Qwen model and validate model availability.

**Test Criteria**: Bootstrap script downloads both Phi-4 Mini and Qwen successfully, start script validates default model exists before launch.

### Tasks

- [X] T094 Add Qwen 2.5 VL 3B download to bootstrap script in scripts/Setup-Environment.ps1 (`ollama pull qwen2.5-vl:3b-instruct`)
- [X] T095 Add Phi-4 Mini availability verification to bootstrap script in scripts/Setup-Environment.ps1
- [ ] T096 [P] Implement cross-platform Qwen download in scripts/setup-environment.sh (Linux/macOS)
- [ ] T097 [P] Implement cross-platform Phi-4 Mini verification in scripts/setup-environment.sh
- [X] T098 Add model availability validation to start script in scripts/Start-AspireHost.ps1
- [X] T099 Implement DefaultModel resolution from configuration in start script in scripts/Start-AspireHost.ps1
- [X] T100 Add fail-fast error with clear message if model unavailable in start script in scripts/Start-AspireHost.ps1
- [ ] T101 [P] Implement model validation in scripts/start-aspire-host.sh (Linux/macOS)

---

## Phase 5: Documentation & Polish

**Goal**: Update all documentation to reflect Agent Framework architecture and new features.

**Test Criteria**: README includes model configuration examples, UI workflow, troubleshooting guide; all diagrams updated.

### Tasks

- [X] T102 [P] Update README with Agent Framework architecture section
- [X] T103 [P] Document multi-model configuration structure in README
- [ ] T104 [P] Document model selection UI workflow in README
- [X] T105 [P] Add troubleshooting guide for model setup issues in README
- [ ] T106 [P] Update architecture diagrams to show ChatClientAgent and AgentThread
- [X] T107 [P] Create quickstart guide in specs/003-model-abstraction/quickstart.md
- [X] T108 [P] Document adding new models (local vs cloud) in quickstart guide
- [X] T109 [P] Document adding new tool handlers in quickstart guide
- [X] T110 [P] Document testing configuration validation in quickstart guide
- [X] T111 [P] Update CHANGELOG.md with feature summary and breaking changes

---

## Dependency Graph (User Story Completion Order)

```
Phase 1 (Setup) → Phase 2 (Interfaces)
                      ↓
                   US2 (Prompts) ← Can start in parallel with US3
                      ↓
                   US3 (Handlers) ← Depends on Phase 2
                      ↓
                   US1 (Model Switching) ← Depends on US2, US3
                      ↓
                Phase 3 (Agent Migration) ← Depends on US1, US2, US3
                      ↓
                   US5 (UI Dropdown) ← Depends on Phase 3, US1
                      ↓
                   US4 (Project Rename) ← Can be done anytime after Phase 1
                      ↓
                   US6 (OpenMeteo Assembly) ← Can be done anytime after Phase 3
                      ↓
                Phase 4 (Scripts) ← Depends on US1
                      ↓
                Phase 5 (Documentation) ← Final phase
```

## Parallel Execution Opportunities

### After Phase 2 Completion:
- **US2 (Prompts)** and **US3 (Handlers)** can be developed in parallel (different files, no dependencies)

### After Phase 3 Completion:
- **US4 (Project Rename)** and **US6 (OpenMeteo Assembly)** can be done in parallel
- **Phase 4 (Scripts)** can be done in parallel with US4/US6

### Within Each User Story:
- Tasks marked with **[P]** can be executed in parallel (different files, no shared state)

## Implementation Strategy

### MVP Scope (User Story 1 Only):
For fastest time-to-value, implement in this order:
1. Phase 1 (Setup) - 5 tasks
2. Phase 2 (Interfaces) - 7 tasks
3. US2 (Prompts) - 9 tasks
4. US3 (Handlers) - 12 tasks
5. US1 (Model Switching) - 10 tasks
6. Phase 3 (Agent Migration) - 11 tasks

**Total MVP**: 54 tasks, ~950 LOC, delivers core configuration-driven model switching.

### Incremental Delivery:
- **Iteration 1 (MVP)**: US1 + dependencies (Phase 1-3) - Model switching works
- **Iteration 2**: Add US5 (UI Dropdown) - User-facing feature
- **Iteration 3**: Add US4 (Project Rename) - Architectural cleanup
- **Iteration 4**: Add US6 (OpenMeteo Assembly) - Domain separation
- **Iteration 5**: Phase 4-5 (Scripts + Docs) - Polish

## Validation Checklist

After all tasks complete, verify:

- [ ] All 13 success criteria (SC-001 through SC-013) pass
- [ ] All 6 user stories fully tested with acceptance scenarios
- [ ] All 15 edge cases handled with documented behavior
- [ ] Zero violations in accessibility audit (axe DevTools)
- [ ] All 92 tasks completed and verified
- [ ] Zero "weather" references in project names (except OpenMeteo assembly)
- [ ] openmeteo_sdk types not exposed in Agent project API surface
- [ ] Configuration validation comprehensive coverage (CHK items resolved)
- [ ] Keyboard-only navigation works for model dropdown
- [ ] Screen readers (NVDA/JAWS) announce all dropdown states correctly

---

**Total Tasks**: 114 (111 original + 3 automated test tasks for SC-012/SC-013)
**Estimated Effort**: 3-5 days for experienced .NET developer
**LOC Impact**: ~1680 lines (1630 original + 50 for automated validation tests)
