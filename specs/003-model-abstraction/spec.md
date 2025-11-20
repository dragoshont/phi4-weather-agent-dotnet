# Feature Specification: Model Abstraction and Domain-Agnostic Project Naming

**Feature Branch**: `003-model-abstraction`  
**Created**: November 20, 2025  
**Status**: Draft  
**Input**: User description: "Abstract model-specific dependencies from application architecture and rename projects to be domain-agnostic"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Switch AI Models Without Code Changes (Priority: P1)

A developer wants to swap from Phi-4 to GPT-4 (or Claude, Llama, etc.) by only changing configuration, without modifying application code or recompiling the solution.

**Why this priority**: This is the core value proposition - making the system model-agnostic unlocks vendor flexibility, cost optimization, and future-proofing.

**Independent Test**: Can be fully tested by changing a single configuration value (e.g., `"AI:ModelType": "GPT4"`) and verifying the application works with a different model without code changes.

**Acceptance Scenarios**:

1. **Given** application is running with Phi-4, **When** developer changes configuration to `"ModelType": "GPT4"`, **Then** application automatically uses GPT-4's native function calling instead of functools format
2. **Given** application is configured for GPT-4, **When** developer changes to Claude, **Then** system uses Claude's tool format without functools layer
3. **Given** application is running, **When** model provider is switched, **Then** system prompt automatically adapts to new model's capabilities
4. **Given** developer deploys to production, **When** they specify different model in environment variables, **Then** no recompilation is needed

---

### User Story 2 - Model-Specific Prompt Management (Priority: P2)

A developer wants to define and maintain model-specific system prompts separately, so each model gets optimized instructions for its capabilities (functools vs native tools vs other formats).

**Why this priority**: Prompt engineering is model-specific - what works for Phi-4 won't work for GPT-4. Having separate prompts ensures optimal performance per model.

**Independent Test**: Can be tested by verifying each model receives its own prompt (e.g., Phi-4 gets functools instructions, GPT-4 doesn't) and checking prompt source location.

**Acceptance Scenarios**:

1. **Given** Phi-4 is selected, **When** chat session starts, **Then** system prompt includes functools format instructions and spelling guidance
2. **Given** GPT-4 is selected, **When** chat session starts, **Then** system prompt excludes functools format (uses native function calling)
3. **Given** developer updates Phi-4 prompt, **When** they save changes, **Then** only Phi-4 conversations are affected, not GPT-4
4. **Given** new model is added, **When** developer creates prompt provider, **Then** system automatically uses appropriate prompt for that model

---

### User Story 3 - Conditional Functools Layer (Priority: P2)

The system automatically applies the functools invocation layer only for models that need it (Phi-4), and bypasses it for models with native function calling (GPT-4, Claude).

**Why this priority**: Performance and correctness - the functools layer adds overhead and complexity that's unnecessary for models with native tool support.

**Independent Test**: Can be tested by inspecting the IChatClient pipeline and verifying functools decorator is present/absent based on model type.

**Acceptance Scenarios**:

1. **Given** Phi-4 is configured, **When** application starts, **Then** FunctoolsChatClient decorator wraps the base chat client
2. **Given** GPT-4 is configured, **When** application starts, **Then** FunctoolsChatClient is NOT applied to pipeline
3. **Given** model with native tools, **When** tool calls are made, **Then** no functools parsing occurs
4. **Given** model configuration changes, **When** application restarts, **Then** functools layer presence adjusts automatically

---

### User Story 4 - Generic Project Naming (Priority: P3)

Projects are renamed to be domain-agnostic (not tied to "weather" or specific tools) so the architecture can be reused for any agent application (e.g., code assistant, data analyst, etc.).

**Why this priority**: Enables template/framework reuse - developers can clone this architecture for non-weather agents without misleading project names.

**Independent Test**: Can be tested by verifying all project names, namespaces, and folder names no longer reference "weather" or specific domains.

**Acceptance Scenarios**:

1. **Given** current projects named `Phi4WeatherAgent.*`, **When** renamed, **Then** new names are `AgentFramework.*` or similar generic names
2. **Given** namespaces reference weather, **When** refactored, **Then** namespaces reflect generic agent concepts (e.g., `AgentFramework.Tools` not `WeatherAgent.Tools`)
3. **Given** developer clones project, **When** they want to build non-weather agent, **Then** project names don't imply weather-only usage
4. **Given** tools are defined, **When** projects are renamed, **Then** tool definitions remain separate from project structure

---

### Edge Cases

- What happens when configuration specifies unsupported model type? (System should fail fast with clear error message)
- How does system handle missing prompt provider for configured model? (Should throw at startup, not runtime)
- What if model supports both native tools AND custom formats? (Configuration should allow override)
- How to handle model switching mid-conversation? (Not supported - requires restart to ensure clean state)
- What if functools layer is mistakenly applied to GPT-4? (Should work but with performance overhead - log warning)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support multiple AI models (Phi-4, GPT-4, Claude, Llama) via configuration without code changes
- **FR-002**: System MUST provide model-specific prompt management through provider pattern
- **FR-003**: System MUST conditionally apply functools invocation layer only for models requiring custom tool formats
- **FR-004**: System MUST allow prompt customization per model type through injectable providers
- **FR-005**: System MUST fail fast at startup with clear error if configured model is not supported
- **FR-006**: System MUST expose model capabilities (native tools vs custom format) through provider interface
- **FR-007**: Configuration MUST allow specifying model type via appsettings.json or environment variables
- **FR-008**: Project names MUST be domain-agnostic (not reference "weather" or specific tools)
- **FR-009**: Namespaces MUST be refactored to generic agent framework naming (e.g., `AgentFramework.*`)
- **FR-010**: System MUST maintain backward compatibility with existing tool definitions during rename

### Key Entities

- **PromptProvider**: Encapsulates model-specific system prompts and model metadata (name, capabilities, requires functools layer)
- **ModelConfiguration**: Configuration object containing model type, endpoint, and provider selection
- **ChatClientPipeline**: Composable pipeline that conditionally includes functools decorator based on model capabilities

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developer can switch from Phi-4 to GPT-4 by changing single configuration value without recompilation (100% configuration-driven)
- **SC-002**: Each model type receives optimized system prompt (verified by prompt content inspection)
- **SC-003**: Functools parsing overhead eliminated for models with native tools (measured by request latency reduction of 10-20%)
- **SC-004**: All project and namespace names are generic (zero references to "weather" in project structure)
- **SC-005**: Existing tool implementations work unchanged after project rename (100% backward compatibility)
- **SC-006**: Adding new model requires only creating prompt provider and updating configuration (no changes to core framework)

## Scope *(mandatory)*

### In Scope

- Creating `IPromptProvider` interface and implementations for Phi-4, GPT-4
- Configuration-driven model selection via appsettings.json
- Conditional application of functools decorator based on model capabilities
- Renaming projects from `Phi4WeatherAgent.*` to generic names (e.g., `AgentFramework.*`)
- Refactoring namespaces to be domain-agnostic
- Updating solution file, launch profiles, and Docker configurations
- Updating documentation and README to reflect new architecture

### Out of Scope

- Supporting runtime model switching (requires application restart)
- Creating prompt providers for all possible models (only Phi-4 and GPT-4 initially)
- Implementing model-agnostic tool definition format (tools remain model-independent already)
- UI changes (application behavior unchanged from user perspective)
- Database schema changes (no data persistence for model configuration)

## Assumptions *(mandatory)*

- Foundry Local/Ollama continue exposing OpenAI-compatible APIs
- Tool implementations remain model-agnostic (return plain text results)
- System prompt is sufficient for model behavior control (no fine-tuning)
- Configuration is set at deployment time (not changed dynamically)
- Developers understand dependency injection pattern used for provider registration

## Dependencies *(mandatory)*

### External Dependencies

- Microsoft.Extensions.AI (IChatClient interface remains stable)
- Foundry Local or alternative model hosting (maintains OpenAI-compatible endpoint)
- Existing tool implementations (GeocodingTools, WeatherTools, AirQualityTools)

### Internal Dependencies

- Existing FunctoolsChatClient decorator pattern
- Tool registry and discovery mechanism
- Aspire AppHost orchestration
- Blazor Web UI chat component

## Non-Functional Requirements *(optional)*

### Performance

- Model switching via configuration should not add startup overhead (lazy loading acceptable)
- Removing functools layer for GPT-4 should reduce latency by 10-20% (eliminates parsing step)

### Maintainability

- Adding new model should require <50 lines of code (just implement IPromptProvider)
- Configuration schema should be self-documenting with JSON schema validation

### Extensibility

- Prompt providers should support composition (e.g., base prompt + model-specific additions)
- System should allow custom prompt providers via DI registration

## Open Questions *(optional)*

1. Should prompts be stored in configuration files (appsettings.json) or separate .txt files per model?
2. What should the generic project naming convention be? Options:
   - `AgentFramework.*` (framework-focused)
   - `MultimodalAgent.*` (capability-focused)
   - `FoundryAgent.*` (platform-focused)
   - `ConversationalAgent.*` (interaction-focused)
3. Should model configuration support fallback chains? (e.g., try GPT-4, fallback to Phi-4)
4. Do we need model-specific validation of tool results? (e.g., GPT-4 expects JSON, Phi-4 accepts plain text)

## Notes *(optional)*

### Design Rationale

**Why IPromptProvider pattern?**
- Type-safe: Compile-time model detection
- Testable: Mock different providers in tests  
- Extensible: Add new models by implementing interface
- Clear: Each provider documents its requirements
- DI-friendly: Fits existing architecture

**Why conditional functools layer?**
- Performance: Eliminates unnecessary parsing for native tool models
- Correctness: Prevents functools format leaking to models that don't expect it
- Simplicity: Models with native support use simpler code path

**Why generic naming?**
- Reusability: Architecture applicable to non-weather agents
- Clarity: Project names reflect purpose (agent framework) not domain (weather)
- Professionalism: Generic names more suitable for open-source template

### Related Specifications

- **001-phi4-weather-assistant**: Original weather assistant implementation
- **002-functools-invocation-layer**: Functools parsing and execution layer

This specification enhances 002 by making it conditional rather than always-on.
