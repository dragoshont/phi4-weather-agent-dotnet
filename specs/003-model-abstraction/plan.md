# Implementation Plan: Model Abstraction and Domain-Agnostic Project Naming

**Branch**: `003-model-abstraction` | **Date**: 2025-11-20 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-model-abstraction/spec.md`

## Summary

Migrate from `Microsoft.Extensions.AI` direct usage to **Microsoft Agent Framework** (`Microsoft.Agents.AI`), creating a configuration-driven model abstraction that supports local models (Phi-4 Mini, Qwen 2.5 VL 3B) and cloud-ready architecture (GPT-4o, Gemini). Implement interface-based tool invocation handlers (`IToolInvocationHandler`) for pluggable model-specific parsing. Rename projects from `Phi4WeatherAgent.*` to `LocalConversationalAgent.*` for domain-agnostic reusability. Extract weather tools to dedicated `LocalConversationalAgent.OpenMeteo` assembly. Add UI dropdown for per-conversation model selection. Primary value: Zero-recompilation model switching and extensible architecture.

## Technical Context

**Language/Version**: .NET 10.0.100+ (pinned in `global.json`, `net10.0` target framework)
**Primary Dependencies**: Microsoft.Agents.AI (public preview), Microsoft.Extensions.AI (types only), Aspire 13.0.0-preview.1+, Blazor Server
**Storage**: N/A (stateless agent, no persistence required for model configuration)
**Testing**: xUnit 2.9.2+, bUnit 1.31.3+ (Blazor components), Moq/NSubstitute (mocking), FluentAssertions
**Target Platform**: Cross-platform (.NET 10: Windows, macOS, Linux)
**Project Type**: Web application (Blazor Server frontend + Agent backend + Aspire AppHost orchestration)

**Performance Goals**:

- Removing functools layer for native tool models: 10-20% latency reduction (eliminates parsing overhead)
- Model switching via configuration: <5s startup overhead (lazy loading acceptable)
- Tool invocation handler: <50ms overhead excluding actual tool execution

**Constraints**:

- **Constitution Compliance**: Agent Framework only (no Semantic Kernel), local-first AI (Foundry/Ollama), .NET 10 mandatory
- **Zero recompilation**: Model switching via configuration changes only
- **No mid-conversation switching**: Dropdown disabled after first message (requires new session)
- **Interface-based extensibility**: Add new tool handlers without modifying core framework

**Scale/Scope**:

- **Projects**: 7 projects after changes (Agent, Web, AppHost, ServiceDefaults, Tools, Agent.Tests, **OpenMeteo**)
- **Impact**: ~1630 LOC across all components (500 middleware, 200 agent migration, 100 tools, 150 state, 50 prompts, 50 rename, 100 UI, 110 scripts, 200 docs, 150 OpenMeteo assembly, 20 validation/accessibility, 3 package files)
- **Model Support**: 2 local models initially (Phi-4 Mini default, Qwen 2.5 VL 3B), cloud-ready structure (GPT-4o, Gemini configuration deferred)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Local-First AI** | ✅ **PASS** | Migration maintains Foundry Local (Windows/macOS) and Ollama (Linux) for Phi-4 Mini. Adds Qwen 2.5 VL 3B via Ollama. No cloud dependencies introduced. |
| **II. .NET 10 Requirement** | ✅ **PASS** | Agent Framework requires .NET 10. Existing `global.json` pins SDK 10.0.100+. No version changes needed. |
| **III. Agent Framework Only** | ✅ **PASS** | **This IS the migration to Agent Framework.** Replaces direct `IChatClient` usage with `ChatClientAgent`. Converts `FunctoolsChatClient` decorator to `IToolInvocationHandler` interface + handlers. No Semantic Kernel introduced. |
| **IV. Aspire 13 Orchestration** | ✅ **PASS** | Existing Aspire AppHost unchanged. Agent Framework integrates via `IChatClient` abstraction already used by Aspire. |
| **V. Model Context Protocol** | ✅ **PASS** | MCP tools (Phase 11+ per constitution) remain unchanged. Tool registration adapts from `AIFunction` to Agent Framework pattern, but MCP integration point preserved. |
| **VI. Zero Cloud Runtime Costs** | ✅ **PASS** | Local models only (Phi-4 Mini, Qwen). Cloud model configuration structure added but implementation **deferred** (no API keys required at runtime). |
| **VII. WCAG 2.1 AA Accessibility** | ⚠️ **REVIEW** | UI dropdown for model selection must meet accessibility requirements (keyboard navigation, ARIA labels, screen reader compatibility). Requires validation in Phase 1 design. |
| **VIII. Template-Based Architecture** | ✅ **PASS** | Maintains existing aichatweb-derived Blazor components. Model dropdown is additive enhancement to `Chat.razor`. |
| **IX. Comprehensive Testing Coverage** | ✅ **PASS** | Migration requires updating existing tests to use `ChatClientAgent` instead of `IChatClient`. Test structure (unit/component/integration) unchanged. |
| **X. Cross-Platform Development** | ✅ **PASS** | Bootstrap scripts updated to download Qwen via Ollama (cross-platform). Start scripts validate model availability. Agent Framework supports all platforms. |
| **XI. MIT License** | ✅ **PASS** | Microsoft.Agents.AI uses MIT license. No new dependency license conflicts. |
| **XII. Custom Invocation Layer** | ✅ **PASS** | **Architecture Enhancement**: Converts `FunctoolsChatClient` decorator to interface-based design (`IToolInvocationHandler` + `FunctoolsHandler`). Maintains functools parsing for Phi-4 Mini/Qwen, enables adding new handlers (ReActJSON, etc.) without core changes. Preserves security audit logging, whitelist, rate limiting. |

**Gate Decision**: ✅ **APPROVED** - All principles pass or have clear mitigation plan (accessibility validation in Phase 1).

**Key Alignment**:

- **Principle III Enhanced**: This feature IS the Agent Framework migration mandated by constitution
- **Principle XII Enhanced**: Interface-based handlers improve extensibility while maintaining security/observability requirements
- **No Violations**: Zero principles require justification or waiver

## Project Structure

### Documentation (this feature)

```text
specs/003-model-abstraction/
├── spec.md              # Feature specification (724 lines, COMPLETE)
├── plan.md              # This file (/speckit.plan command output, IN PROGRESS)
├── research.md          # Phase 0 output (TBD - Agent Framework migration patterns)
├── data-model.md        # Phase 1 output (TBD - IToolInvocationHandler, ModelConfiguration entities)
├── quickstart.md        # Phase 1 output (TBD - Developer setup guide for multi-model config)
├── contracts/           # Phase 1 output (TBD - Configuration JSON schemas)
├── checklists/
│   └── requirements.md  # Quality checklist (COMPLETE, validated)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (Current Structure)

```text
src/
├── Phi4WeatherAgent.Agent/           # Agent backend (to be renamed)
│   ├── Services/
│   │   ├── FunctoolsChatClient.cs   # Decorator (to be converted to handler interface)
│   │   └── ...
│   └── Program.cs
├── Phi4WeatherAgent.Web/             # Blazor Server UI (to be renamed)
│   ├── Components/
│   │   └── Pages/
│   │       └── Chat.razor            # Will add model dropdown
│   └── Program.cs
├── Phi4WeatherAgent.Tools/           # Tool implementations (to be renamed)
│   ├── GeocodingTools.cs
│   ├── WeatherTools.cs
│   └── AirQualityTools.cs
├── Phi4WeatherAgent.AppHost/         # Aspire orchestration (to be renamed)
│   └── Program.cs
├── Phi4WeatherAgent.ServiceDefaults/ # Shared defaults (to be renamed)
│   └── Extensions.cs
└── Phi4WeatherAgent.Agent.Tests/    # Agent tests (to be renamed)
    └── ...

tests/
└── [additional test projects if any]

scripts/
├── Setup-Environment.ps1             # Bootstrap script (will add Qwen download)
├── Start-AspireHost.ps1              # Start script (will add model validation)
└── ...

prompts/                              # NEW DIRECTORY
└── weather-assistant.md              # System prompt (migrated from hardcoded)
```

### Source Code (Target Structure After Rename)

```text
src/
├── LocalConversationalAgent.Agent/          # Renamed from Phi4WeatherAgent.Agent
│   ├── Interfaces/
│   │   ├── IToolInvocationHandler.cs       # NEW: Interface for pluggable handlers
│   │   └── IPromptProvider.cs               # NEW: Prompt abstraction
│   ├── Handlers/
│   │   ├── FunctoolsHandler.cs             # NEW: Converted from decorator
│   │   └── [ReActJSONHandler.cs]           # FUTURE: Extensibility example
│   ├── Services/
│   │   ├── ChatAgentService.cs             # UPDATED: Uses ChatClientAgent
│   │   └── PromptProvider.cs                # NEW: Loads markdown prompts
│   ├── Models/
│   │   └── ModelConfiguration.cs            # NEW: Configuration model
│   └── Program.cs                           # UPDATED: DI registration for handlers
├── LocalConversationalAgent.Web/            # Renamed from Phi4WeatherAgent.Web
│   ├── Components/
│   │   └── Pages/
│   │       └── Chat.razor                   # UPDATED: Model dropdown + locking logic
│   └── appsettings.json                     # UPDATED: Multi-model configuration
├── LocalConversationalAgent.Tools/          # Renamed from Phi4WeatherAgent.Tools
│   └── [generic tool abstractions]         # NOTE: Weather tools moved to OpenMeteo
├── LocalConversationalAgent.OpenMeteo/      # NEW: Domain-specific weather tools
│   ├── Tools/
│   │   ├── GeocodingTools.cs               # MOVED: From Tools project
│   │   ├── WeatherTools.cs                 # MOVED: From Tools project
│   │   └── AirQualityTools.cs              # MOVED: From Tools project
│   ├── Clients/
│   │   └── OpenMeteoHttpClient.cs          # HTTP client with Polly retry
│   └── Models/
│       └── [OpenMeteo API response models]
├── LocalConversationalAgent.AppHost/        # Renamed from Phi4WeatherAgent.AppHost
│   └── Program.cs
├── LocalConversationalAgent.ServiceDefaults/ # Renamed from Phi4WeatherAgent.ServiceDefaults
│   └── Extensions.cs
└── LocalConversationalAgent.Agent.Tests/    # Renamed from Phi4WeatherAgent.Agent.Tests
    ├── HandlerTests/
    │   └── FunctoolsHandlerTests.cs        # NEW: Unit tests for handler
    └── ...

prompts/                                     # NEW DIRECTORY
└── weather-assistant.md                     # System prompt

scripts/
├── Setup-Environment.ps1                    # UPDATED: Downloads Phi-4 Mini + Qwen
├── Start-AspireHost.ps1                     # UPDATED: Validates default model exists
└── ...

Directory.Build.props                        # UPDATED: Package references (Microsoft.Agents.AI)
LocalConversationalAgent.sln                 # RENAMED: Solution file
global.json                                  # UNCHANGED: Already pins .NET 10
```

**Structure Decision**: Web application with clear separation of concerns. Agent backend handles model orchestration and tool invocation. Blazor Web provides UI. Aspire AppHost orchestrates both. Testing projects mirror source structure. Key changes: (1) Project rename for domain-agnostic reusability, (2) Interface-based handler architecture for extensibility, (3) Configuration-driven prompts in dedicated directory.

## Complexity Tracking

**No violations** - All 12 constitution principles pass. No complexity justification required.

## Phase 0: Outline & Research

**Objective**: Resolve all NEEDS CLARIFICATION items from Technical Context. Research Agent Framework migration patterns, tool invocation handler architecture, and configuration-driven prompt loading.

**Research Tasks**:

1. **Agent Framework Migration Patterns**
   - Research: ChatClientAgent API, middleware system, thread management
   - Research: Converting IChatClient → ChatClientAgent patterns
   - Research: Agent Framework tool registration (compared to AIFunction)
   - Decision: How to structure DI registration for ChatClientAgent with conditional middleware

2. **Tool Invocation Handler Architecture**
   - Research: Interface design for IToolInvocationHandler
   - Research: Middleware vs Handler pattern in Agent Framework context
   - Research: Handler discovery and registration patterns
   - Decision: How to map ToolInvocationStrategy string → IToolInvocationHandler implementation

3. **Configuration-Driven Prompt Loading**
   - Research: IPromptProvider interface design
   - Research: Markdown file loading patterns (file system vs embedded resources)
   - Research: Prompt composition strategies (base + model-specific)
   - Decision: File location convention for prompts (prompts/*.md)

4. **Model Configuration Schema**
   - Research: JSON schema for ModelConfiguration
   - Research: Configuration validation patterns (.NET Options pattern)
   - Research: Environment variable substitution for API keys
   - Decision: Configuration section structure (AI:Models:*)

5. **State Management Migration**
   - Research: AgentThread API and lifecycle
   - Research: Thread persistence patterns (if needed for future)
   - Research: Conversation history management
   - Decision: How to integrate AgentThread with Blazor component state

**Output**: `research.md` with all decisions documented and alternatives evaluated.

## Phase 1: Design & Contracts

**Objective**: Generate data model, API contracts, and quickstart guide. Update agent context with new technologies.

**Deliverables**:

### 1. Data Model (`data-model.md`)

Extract entities from feature specification and research:

- **IToolInvocationHandler**: Interface definition, responsibilities, lifecycle
- **FunctoolsHandler**: Implementation details, parsing algorithm, error handling
- **IPromptProvider**: Interface definition, markdown loading, model metadata
- **ModelConfiguration**: Properties (Provider, Endpoint, ApiKey, ToolInvocationStrategy, SystemPromptFile), validation rules
- **ChatClientAgent**: Agent Framework's agent abstraction, middleware pipeline, thread management
- **AgentThread**: State container, conversation history, lifecycle

### 2. API Contracts (`contracts/`)

Generate JSON schemas for configuration:

- **appsettings.schema.json**: AI:Models configuration schema
  - DefaultModel (string, required)
  - Models (object, required)
    - [modelId] (object)
      - Provider (enum: Foundry, Ollama, OpenAI, AzureOpenAI, GoogleGemini)
      - Endpoint (string, URI format)
      - ApiKey (string, nullable)
      - ToolInvocationStrategy (string, nullable)
      - SystemPromptFile (string, file path)

- **tool-invocation-handler.schema.json**: IToolInvocationHandler interface contract
  - InvokeAsync method signature
  - Context parameter structure
  - Return type (AgentRunResponse)

### 3. Quickstart Guide (`quickstart.md`)

Developer setup guide for multi-model configuration:

- Prerequisites (. NET 10, Foundry/Ollama)
- Bootstrap script usage (downloading models)
- Configuration file setup (appsettings.json)
- Adding new models (local vs cloud)
- Adding new tool handlers (implementing IToolInvocationHandler)
- Testing configuration (validation commands)
- Troubleshooting common issues

### 4. Agent Context Update

Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot` to add:

- Microsoft.Agents.AI (Agent Framework)
- IToolInvocationHandler pattern
- Configuration-driven model selection
- Markdown prompt loading

**Output**: `data-model.md`, `contracts/*.schema.json`, `quickstart.md`, updated agent context file.

## Post-Phase 1: Constitution Re-Check

**Objective**: Verify design meets all 12 principles after detailed design decisions.

**Re-Validation**:

- **Principle VII (WCAG 2.1 AA)**: Verify model dropdown design includes ARIA labels, keyboard navigation, screen reader announcements
- **Principle XII (Custom Invocation Layer)**: Confirm IToolInvocationHandler design maintains security audit logging, whitelist, rate limiting requirements

**Gate Decision**: Must pass before proceeding to Phase 2 (task breakdown).

## Implementation Sequence (Phase 2 Preview)

*Note: Detailed tasks created by `/speckit.tasks` command, not `/speckit.plan`*

**Recommended Phase Order** (based on ~1480 LOC impact and dependency analysis):

1. **Package Migration** (~3 files, LOW risk)
   - Update Directory.Build.props with Microsoft.Agents.AI
   - Remove Microsoft.Extensions.AI direct usage (keep for types)
   - Verify build succeeds

2. **Interface Definitions** (~200 LOC, LOW risk)
   - Create IToolInvocationHandler interface
   - Create IPromptProvider interface
   - Create ModelConfiguration model
   - No dependencies, enables parallel work

3. **Prompt Provider Implementation** (~50 LOC, LOW complexity)
   - Implement IPromptProvider with markdown loading
   - Create prompts/ directory structure
   - Migrate hardcoded prompts to weather-assistant.md
   - Register in DI container

4. **FunctoolsHandler Implementation** (~500 LOC, HIGH complexity)
   - Convert FunctoolsChatClient decorator to FunctoolsHandler
   - Implement IToolInvocationHandler interface
   - Preserve parsing logic, error handling, timeout, security audit
   - Unit tests for handler

5. **ChatClientAgent Migration** (~200 LOC, MEDIUM complexity)
   - Replace IChatClient with ChatClientAgent
   - Update service registration with conditional handler application
   - Integrate IPromptProvider for system prompts
   - Update DI container registration

6. **AgentThread Integration** (~150 LOC, MEDIUM complexity)
   - Replace manual conversation history with AgentThread
   - Update Chat.razor to use thread lifecycle
   - Handle thread creation/disposal

7. **Tool Registration Updates** (~100 LOC, LOW complexity)
   - Adapt AIFunction to Agent Framework tool pattern
   - Update GeocodingTools, WeatherTools, AirQualityTools
   - Verify tool discovery

8. **Model Dropdown UI** (~100 LOC, LOW complexity)
   - Add dropdown to Chat.razor
   - Populate from AI:Models configuration
   - Implement locking logic after first message
   - Format: "Provider: model-name (endpoint-type)"
   - ARIA labels, keyboard navigation

9. **Bootstrap Script Updates** (~50 LOC, LOW complexity)
   - Add Qwen download: `ollama pull qwen2.5-vl:3b-instruct`
   - Verify Phi-4 Mini availability
   - Cross-platform testing

10. **Start Script Validation** (~30 LOC, LOW complexity)
    - Read AI:DefaultModel from config
    - Validate model exists (Foundry/Ollama check)
    - Fail fast with clear error message

11. **Project Rename** (~50 files, MEDIUM complexity)
    - Rename projects: `Phi4WeatherAgent.*` → `LocalConversationalAgent.*`
    - Update namespaces across all files
    - Update solution file
    - Update launch profiles, Docker configs

12. **README Documentation** (~200 LOC, LOW complexity)
    - Document model configuration structure
    - Document UI dropdown workflow
    - Document troubleshooting guide
    - Update architecture diagrams

13. **Test Updates** (throughout implementation)
    - Update tests to use ChatClientAgent
    - Update tests to use AgentThread
    - Add handler unit tests
    - Add UI component tests for dropdown
    - Integration tests for multi-model scenarios

**Total Estimated Effort**: ~1630 LOC, 13 phases, estimated 3-5 days for experienced .NET developer.

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Agent Framework public preview API changes | MEDIUM | HIGH | Pin exact version in Directory.Build.props, monitor NuGet for breaking changes, isolate Agent Framework usage behind interfaces |
| FunctoolsHandler complexity during conversion | MEDIUM | MEDIUM | Thorough unit tests before refactoring, preserve existing parsing logic as much as possible, incremental conversion |
| Project rename breaking references | LOW | HIGH | Use IDE refactoring tools (Rename Symbol), verify all namespaces updated, comprehensive build/test after rename |
| Model dropdown accessibility violations | LOW | MEDIUM | Use Blazor accessibility best practices, validate with axe DevTools, test with screen readers |
| Cross-platform script compatibility | MEDIUM | LOW | Test bootstrap/start scripts on all platforms (Windows PowerShell, macOS/Linux bash), document platform-specific commands |
| Configuration validation gaps | LOW | MEDIUM | Implement JSON schema validation, fail fast at startup with clear errors, comprehensive error messages |

**Highest Risk**: Agent Framework API stability. **Mitigation**: Version pinning + interface isolation.

## Success Metrics

Extracted from specification SC-001 through SC-010:

- ✅ **SC-001**: Developer can switch between models (Phi-4 Mini ↔ Qwen ↔ cloud) by changing configuration without recompilation (100% configuration-driven)
- ✅ **SC-002**: Each model type receives optimized system prompt (verified by prompt content inspection)
- ✅ **SC-003**: Custom tool invocation handlers applied only when ToolInvocationStrategy is set (null = native calling, eliminates overhead for cloud models)
- ✅ **SC-004**: All project and namespace names are generic (zero references to "weather" in project structure)
- ✅ **SC-005**: Existing tool implementations work unchanged after project rename (100% backward compatibility)
- ✅ **SC-006**: Adding new model requires only updating configuration and optional prompt file (no core framework changes)
- ✅ **SC-007**: Users can select any configured model from dropdown before starting conversation (100% UI-driven model selection)
- ✅ **SC-008**: Model selection dropdown displays provider, model name, and endpoint type clearly (format: "Provider: model-name (endpoint-type)")
- ✅ **SC-009**: Bootstrap script successfully downloads both Phi-4 Mini and Qwen 2.5 VL 3B models (verified by `ollama list` or Foundry status)
- ✅ **SC-010**: Start script validates model availability before launch (fails fast with clear error if model missing)

**Acceptance Criteria**: All 10 success criteria must pass for feature completion.
