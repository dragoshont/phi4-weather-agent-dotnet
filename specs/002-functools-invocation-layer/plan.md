# Implementation Plan: Foundry Native Function Calling Integration

**Branch**: `002-functools-invocation-layer` | **Date**: 2025-11-17 | **Spec**: [spec.md](./spec.md)
**Input**: Leverage Foundry Local's native function calling support with Phi-4-mini

**CRITICAL DISCOVERY**: Foundry Local 0.8.103+ has built-in function calling template for Phi-4-mini using `functools[...]` syntax. Current implementation uses custom parsing which works but bypasses Foundry's native tool injection.

## Summary

**Primary Requirement**: Integrate with Foundry's native function calling by passing tools via Microsoft.Extensions.AI ChatOptions.Tools API, allowing Foundry to inject tool definitions into Phi-4's prompt template automatically.

**Hybrid Approach** (Constitutional Principle III):
1. **Foundry injects tools** → Pass AIFunctions via ChatOptions.Tools, Foundry uses native template with {Tool} placeholder
2. **Custom parser executes tools** → FunctoolsChatClient intercepts streaming chunks, buffers functools blocks, parses via IFunctoolsParser, dispatches via ToolInvoker to registered handlers
3. **Zero-code extensibility** → Add [Tool] attribute or MCP server → automatic discovery and injection

**Technical Approach**:

1. Convert attribute-based `[Tool]` annotations to Microsoft.Extensions.AI `AIFunction` objects
2. Pass AIFunctions via `ChatOptions.Tools` to Foundry
3. Foundry's template injects tools into `{Tool}` placeholder with proper functools format instructions
4. Keep custom FunctoolsChatClient for parsing responses and executing tools (still needed - model generates functools, we parse and execute)
5. Remove manual system prompt tool descriptions (Foundry handles this)

## Technical Context

**Language/Version**: .NET 10.0 (non-negotiable per constitution)
**Primary Dependencies**:

- Microsoft.Extensions.AI v10.0.0-preview (IChatClient, AIFunction, ChatOptions.Tools)
- Microsoft.Extensions.AI.OpenAI v10.0.0-preview (OpenAI client for Foundry)
- Foundry Local 0.8.103+ (native function calling template for Phi-4-mini)

**AI Model**: Phi-4-mini-instruct-generic-cpu:5 via Foundry Local
**Storage**: In-memory ToolRegistry (ConcurrentDictionary), no persistent storage
**Testing**: xUnit 2.9.3, integration tests with mock IChatClient
**Target Platform**: Windows (Foundry), macOS (Foundry), Linux (Ollama - future)
**Project Type**: .NET Aspire distributed application (Web + Agent projects)
**Performance Goals**:

- Tool discovery: < 500ms at startup (50 tools)
- Functools parsing: <10ms typical (1-5KB payload, P95 latency), <50ms worst-case (1MB payload per NFR-001)
- Tool execution: < 5000ms (weather API dependent, external HTTP call)
- End-to-end: < 10s for single tool call (user perception target, includes all phases)

**Constraints**:

- Zero-code extensibility (add [Tool] attribute → auto-discovery)
- Local-first AI (no cloud API calls)
- Aspire telemetry integration (OpenTelemetry traces)

**Scale/Scope**: 5-10 tools initially, extensible to 50+ tools

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ PASS: Local-First AI (Principle I)

- Foundry Local 0.8.103 confirmed running locally
- Phi-4-mini-instruct-generic-cpu:5 model deployed
- No cloud API dependencies

### ✅ PASS: .NET 10 Requirement (Principle II)

- Project uses .NET 10.0 SDK
- Microsoft.Extensions.AI preview packages compatible

### ⚠️ REVIEW: Agent Framework Constraint (Principle III)

- **Current**: Hybrid approach - Foundry injects tools via ChatOptions.Tools, custom parser executes them
- **Justification**: Phi-4 uses custom `functools[...]` syntax - Foundry 0.8.103+ has native template for INJECTION, but we need custom parser for EXECUTION
- **Discovery**: Foundry's {Tool} placeholder expects ChatOptions.Tools → native template injection works perfectly
- **Implementation**: IAIFunctionAdapter converts ToolRegistry → AIFunction, IChatOptionsBuilder populates ChatOptions.Tools
- **Status**: APPROVED - Hybrid approach leverages Foundry native injection while maintaining custom execution layer per Principle III exception

### ✅ PASS: MCP Integration (Principle V)

- ToolDiscoveryService supports attribute-based discovery
- Extensible to MCP adapter pattern (future phase)

### ✅ PASS: Custom Invocation Layer (Principle XII)

- FunctoolsChatClient intercepts and parses model output
- ToolInvoker executes discovered tools
- ToolRegistry maintains zero-code extensibility
- **NEW**: Will integrate with Foundry's native tool injection

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Phi4WeatherAgent.Agent/           # Core invocation layer
│   ├── Integration/
│   │   └── FunctoolsChatClient.cs     # IChatClient decorator (parse & execute)
│   ├── Parsing/
│   │   ├── FunctoolsParser.cs         # Parse functools[...] from model output
│   │   ├── FunctionCall.cs            # Parsed function call DTO
│   │   └── ParserException.cs         # Parser-specific errors
│   ├── Registry/
│   │   ├── ToolRegistry.cs            # ConcurrentDictionary<string, ToolDescriptor>
│   │   ├── ToolDescriptor.cs          # Tool schema, method info, validation
│   │   ├── ToolDiscoveryService.cs    # BackgroundService for [Tool] discovery
│   │   └── ToolAttribute.cs           # [Tool("Name")] for zero-code extensibility
│   ├── Dispatching/
│   │   ├── ToolInvoker.cs             # Execute registered tools via reflection
│   │   ├── ToolResult.cs              # Execution result DTO
│   │   └── IToolInvoker.cs            # Interface for DI
│   └── Adapters/                      # NEW: Convert to Microsoft.Extensions.AI
│       ├── AIFunctionAdapter.cs       # Convert ToolMetadata → AIFunction
│       └── ChatOptionsBuilder.cs      # Build ChatOptions with Tools for Foundry
│
├── Phi4WeatherAgent.Tools/            # Weather domain tools
│   ├── GeocodingTools.cs              # [Tool] GeocodeLocation
│   ├── WeatherTools.cs                # [Tool] GetWeather, GetForecast
│   └── AirQualityTools.cs             # [Tool] GetAirQuality, GetPollenForecast
│
└── Phi4WeatherAgent.Web/
    ├── Components/Pages/Chat/
    │   └── Chat.razor                  # UPDATED: Use ChatOptions.Tools, remove manual prompt
    └── Program.cs                      # IChatClient registration

tests/
├── Phi4WeatherAgent.Agent.Tests/
│   ├── Integration/
│   │   └── FunctoolsChatClientIntegrationTests.cs  # Mock streaming tests
│   ├── Parsing/
│   │   └── FunctoolsParserTests.cs
│   ├── Registry/
│   │   └── ToolDiscoveryTests.cs
│   └── Adapters/                      # NEW
│       └── AIFunctionAdapterTests.cs  # Test ToolDescriptor → AIFunction conversion
```

**Structure Decision**: Aspire distributed application with separate Agent (core logic) and Web (UI) projects. Integration tests use mock IChatClient to avoid Foundry dependency.

## Complexity Tracking

> **No violations** - Architecture aligns with constitutional principles.

## Phase 0: Research & Discovery

### Research Tasks

#### R001: Foundry Native Function Calling Template Analysis

**Status**: ✅ COMPLETED

**Findings**:

- Foundry Local 0.8.103+ includes native functools template for Phi-4-mini
- Template location: `~/.foundry/cache/models/Microsoft/Phi-4-mini-instruct-generic-cpu-5/v5/inference_model.json`
- Template structure:

  ```json
  {
    "PromptTemplate": {
      "system": "<|system|>{Content}<|tool|>{Tool}<|/tool|><|end|>",
      "tool": "<|tool|>{Tool}<|/tool|>",
      "prompt": "<|system|> You are a helpful assistant with these tools..."
    }
  }
  ```

- **Key insight**: `{Tool}` placeholder expects JSON schema injection by Foundry when tools are passed via ChatOptions

#### R002: Microsoft.Extensions.AI Tool Registration API

**Status**: ✅ COMPLETED

**Findings**:

- `ChatOptions.Tools` property accepts `IList<AITool>`
- `AIFunction` class represents callable functions with:
  - Name, Description
  - JSON Schema for parameters
  - Delegate for execution (optional - we'll use custom invoker)
- `AIFunctionFactory.Create()` methods for various function signatures
- **Gap**: Need adapter to convert our `ToolDescriptor` → `AIFunction`

#### R003: Test Current Implementation Behavior

**Status**: ✅ COMPLETED via integration tests

**Findings**:

- FunctoolsParser correctly detects `functools[...]` syntax
- JSON sanitization handles extra braces from model
- FunctoolsChatClient buffers streaming responses properly
- **Issue**: Tools not registered in ToolRegistry (ToolDiscoveryService not finding tools)
- **Issue**: Manual system prompt bypasses Foundry's native template

---

## Phase 2: Implementation Tasks

See [tasks.md](./tasks.md) for detailed implementation tasks.

### Summary of Tasks

- **T001**: Create AIFunctionAdapter (P0 - BLOCKING)
- **T002**: Create ChatOptionsBuilder (P0 - BLOCKING)
- **T003**: Register adapters in DI (P0 - BLOCKING)
- **T004**: Update Chat.razor to use ChatOptions.Tools (P0 - BLOCKING)
- **T005**: Verify ToolDiscoveryService registration (P1 - HIGH)
- **T006**: Add health check endpoint (P2 - NICE TO HAVE)

### Critical Path

**Phase 0-2 Foundation** (Old task IDs - planning phase):

```text
T001 (AIFunctionAdapter)
  ↓
T002 (ChatOptionsBuilder)
  ↓
T003 (DI Registration)
  ↓
T004 (Update Chat.razor)
```

**Phase 10 Foundry Native Integration** (Current implementation - see tasks.md T200-T225):

```text
T200 (Create AIFunctionAdapter.cs) [PARALLEL with T206]
  ↓
T201-T205 (Implement AIFunctionAdapter methods)
  ↓
T206 (Create ChatOptionsBuilder.cs)
  ↓
T207-T211 (Implement ChatOptionsBuilder methods)
  ↓
T212-T213 (Register adapters in DI) [BLOCKING for T214]
  ↓
T214-T218 (Update Chat.razor to use ChatOptions.Tools)
  ↓
T219-T225 (Enhanced logging + health endpoint)
```

**Dependencies**: Phase 10 requires Phase 2 completion (ToolRegistry, ToolDescriptor entities exist)

### Success Criteria

-  All unit tests pass
-  Integration tests pass (2/2 existing + new adapter tests)
-  Manual test: No raw functools visible in chat UI
-  Logs show: "Registered 5 tools: GeocodeLocation, GetWeather, ..."
-  Logs show: "Built ChatOptions with 5 tools"
-  End-to-end latency < 10s for weather query

