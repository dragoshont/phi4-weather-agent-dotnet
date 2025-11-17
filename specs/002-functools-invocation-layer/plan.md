# Implementation Plan: Phi-4-mini Functools Invocation Layer

**Branch**: `002-functools-invocation-layer` | **Date**: 2025-11-16 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/002-functools-invocation-layer/spec.md`

## Summary

Build a robust invocation layer to parse Phi-4-mini's custom `functools[...]` text format and execute MCP/local tools with zero-code extensibility. The system provides a correctness-first parser, secure-by-default dispatcher with explicit whitelisting, horizontally scalable tool registry using ConcurrentDictionary, and MCP adapter with Polly retry policies. Observability is achieved through OpenTelemetry integration with Aspire Dashboard. The architecture enables tool addition via `[Tool]` attributes or MCP server URLs without modifying parser/dispatcher code.

**Key Milestones:**
- **M1**: Parser (streaming detection, JSON deserialization, error handling)
- **M2**: Dispatcher + Registry (manifest-based + attribute discovery, validation, allowlist)
- **M3**: Telemetry + Policies (OpenTelemetry traces/metrics, Polly timeouts/retries)
- **M4**: MCP Bridge (ListTools discovery, CallTool translation to ToolDescriptor)
- **M5**: Conversation Glue (IChatClient integration, tool message formatting)
- **M6**: Aspire Wiring + E2E + Benchmarks (DI registration, end-to-end tests, performance validation)

**Risks & Mitigations:**
- **Model Format Drift**: Phi-4-mini changes functools syntax → Mitigation: Comprehensive parser tests with fuzzing, version detection
- **AOT Constraints**: Reflection-based tool discovery incompatible with Native AOT → Mitigation: Optional source generator path (deferred to Phase 2)
- **MCP Outages**: Network failures during discovery/invocation → Mitigation: Local fallback registry, circuit breaker pattern

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 SDK (10.0.100, pinned in global.json)  
**Primary Dependencies**: 
  - Microsoft.Extensions.AI 10.0.0-preview.1.25071.7+ (Agent Framework)
  - Aspire.Hosting 13.0.0-preview.1+ (orchestration)
  - System.Text.Json (built-in, parser/serialization)
  - JsonSchema.Net (argument validation, research needed for version)
  - Polly 8.5.0+ (resilience policies)
  - OpenTelemetry.Extensions.Hosting + OpenTelemetry.Instrumentation.AspNetCore (telemetry)

**Storage**: N/A (in-memory tool registry, no persistent state)  

**Testing**: 
  - xUnit 2.9.2+ (unit/integration tests)
  - FluentAssertions 6.12.1+ (readable assertions)
  - BenchmarkDotNet 0.14.0+ (performance validation)
  - TestContainers (mock MCP servers for integration tests)

**Target Platform**: Cross-platform (Windows/macOS/Linux with platform-specific model hosting)  

**Project Type**: Web application (Blazor Server frontend + Aspire AppHost orchestration)  

**Performance Goals**: 
  - Parser: <50ms for 1MB functools block (NFR-001)
  - Dispatcher validation: <5ms per tool call (NFR-002)
  - Registry lookup: <1μs per tool name (NFR-003)
  - Total invocation overhead: <50ms excluding tool execution (NFR-004)
  - Startup: <5 seconds with 50 registered tools (SC-012)

**Constraints**: 
  - Zero runtime reflection for AOT compatibility (optional Source Generator path, NFR-010)
  - Explicit tool whitelist (security constraint, Principle XII)
  - Rate limiting: Max 10 tool calls per turn (security constraint)
  - Argument validation: Max 10MB per argument (security constraint)
  - Thread-safe registry: Concurrent tool registration/lookup (NFR-006)
  - Memory: Zero leaks over 1000 invocations (NFR-009)

**Scale/Scope**: 
  - 50+ tools at startup (local + MCP discovered)
  - 100 concurrent tool invocations (stress test, SC-011)
  - 1000+ tool invocations per session (memory leak test)
  - 6 user stories, 26 requirements, 18 acceptance scenarios

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I: Local-First AI
**Status**: ✅ PASS  
**Verification**: Feature spec makes no changes to model hosting. Phi-4-mini remains on Foundry Local (Windows/macOS) or Ollama (Linux). Zero cloud AI dependencies introduced.

### Principle II: .NET 10 Requirement
**Status**: ✅ PASS  
**Verification**: All new code targets `net10.0` framework. Dependencies use .NET 10 preview packages (Microsoft.Extensions.AI 10.0.0-preview.1+, Aspire 13.0.0-preview.1+). No .NET 9 or earlier references.

### Principle III: Agent Framework Only
**Status**: ✅ PASS (with approved exception)  
**Verification**: Custom functools parser integrates with `IChatClient` abstraction from Microsoft.Extensions.AI. No Semantic Kernel packages introduced. Exception explicitly documented in Principle III for Phi-4-mini's custom format parsing.

### Principle IV: Aspire 13 Orchestration
**Status**: ✅ PASS  
**Verification**: Tool registry and MCP adapter integrate with Aspire DI container. OpenTelemetry instrumentation integrates with Aspire Dashboard. No changes to orchestration model.

### Principle V: Model Context Protocol (MCP)
**Status**: ✅ PASS  
**Verification**: MCP adapter (M4) implements `ListTools` discovery and tool invocation via MCP protocol. Requirement FR-004 explicitly mandates MCP integration. User Story 2 validates zero-code MCP extensibility.

### Principle VI: Blazor Server UI
**Status**: ✅ PASS  
**Verification**: No changes to UI layer. Feature operates in backend services consumed by existing Blazor components.

### Principle VII: Zero Cost Operations
**Status**: ✅ PASS  
**Verification**: All tools run locally or call free APIs (OpenMeteo). No paid cloud services introduced. MCP servers are self-hosted or free third-party.

### Principle VIII: Weather Domain Focus
**Status**: ✅ PASS  
**Verification**: Feature enables weather tool invocation (GeocodeLocation, GetWeather, GetAllergenData) through robust parsing layer. Domain focus maintained.

### Principle IX: Accessibility First
**Status**: ⚠️ N/A  
**Verification**: Feature is backend infrastructure (parser/dispatcher/registry). No UI components introduced. Accessibility constraints apply to existing Blazor components only.

### Principle X: Cross-Platform Compatibility
**Status**: ✅ PASS  
**Verification**: Parser uses `System.Text.Json` (built-in, cross-platform). Registry uses `ConcurrentDictionary` (standard library). TestContainers provides cross-platform MCP mocking. No platform-specific APIs.

### Principle XI: Testing Requirements
**Status**: ✅ PASS  
**Verification**: Spec mandates unit tests (xUnit + FluentAssertions), integration tests (TestContainers), E2E tests (real Phi-4-mini + MCP), performance benchmarks (BenchmarkDotNet). 18 acceptance scenarios documented. Coverage target >80%.

### Principle XII: Custom Invocation Layer
**Status**: ✅ PASS (this feature implements the principle)  
**Verification**: This feature IS the implementation of Principle XII. Spec requirements directly map to principle's architecture:
- Parser (FR-001, FR-002, US4) → Correctness-first with schema validation
- Dispatcher (FR-005, FR-006, FR-007, US3, US5) → Secure-by-default with whitelist
- Registry (FR-003, FR-008, US1) → Horizontal scalability with ConcurrentDictionary
- MCP Adapter (FR-004, US2) → Minimal coupling with Polly retry
- Observability (FR-011, FR-012, FR-013, US6) → OpenTelemetry integration
- Extensibility (FR-003, FR-004, SC-001, SC-002) → Zero-code tool addition

**GATE RESULT**: ✅ ALL PASS - Proceed to Phase 0 research



## Project Structure

### Documentation (this feature)

```text
specs/002-functools-invocation-layer/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (JSON Schema libs, MCP protocol, Aspire DI patterns)
├── data-model.md        # Phase 1 output (FunctionCall, ToolDescriptor, ToolResult entities)
├── quickstart.md        # Phase 1 output (hello-world: add [Tool] method → invoke via Phi-4-mini)
├── contracts/           # Phase 1 output (C# interfaces: IFunctoolsParser, IToolInvoker, IToolRegistry)
│   ├── IFunctoolsParser.cs
│   ├── IToolInvoker.cs
│   └── IToolRegistry.cs
├── checklists/          # Quality validation (created during /speckit.specify)
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Phi4WeatherAgent.AppHost/           # Aspire orchestration (existing)
│   └── Program.cs                      # Add DI registration for tool services
│
├── Phi4WeatherAgent.ServiceDefaults/   # Shared configuration (existing)
│   └── Extensions.cs                   # Add OpenTelemetry instrumentation
│
├── Phi4WeatherAgent.Agent/             # NEW: Invocation layer implementation
│   ├── Phi4WeatherAgent.Agent.csproj   # Dependencies: Microsoft.Extensions.AI, JsonSchema.Net, Polly
│   │
│   ├── Parsing/                        # M1: Parser
│   │   ├── IFunctoolsParser.cs         # Contract: Parse(ReadOnlySpan<char>) → IEnumerable<FunctionCall>
│   │   ├── FunctoolsParser.cs          # Implementation: Regex detection + System.Text.Json
│   │   ├── FunctionCall.cs             # Data: Name + Arguments (JsonElement)
│   │   └── ParserException.cs          # Error: MALFORMED_BLOCK, INCOMPLETE_STREAM
│   │
│   ├── Registry/                       # M2: Tool Registry
│   │   ├── IToolRegistry.cs            # Contract: TryGet, Register, ListAsync
│   │   ├── ToolRegistry.cs             # Implementation: ConcurrentDictionary<string, ToolDescriptor>
│   │   ├── ToolDescriptor.cs           # Data: Name, Source, ArgsSchema, Invoker, SecurityClass, Timeout
│   │   └── ToolAttribute.cs            # Attribute: [Tool("ToolName")] for discovery
│   │
│   ├── Dispatching/                    # M2: Dispatcher
│   │   ├── IToolInvoker.cs             # Contract: InvokeAsync(name, args, ct) → ToolResult
│   │   ├── ToolInvoker.cs              # Implementation: Validation, whitelist check, timeout
│   │   ├── ToolResult.cs               # Data: Name, Content, Error, Duration, Meta
│   │   └── DispatcherException.cs      # Error: UNKNOWN_TOOL, ARG_VALIDATION_FAILED, TIMEOUT
│   │
│   ├── McpAdapter/                     # M4: MCP Bridge
│   │   ├── IMcpClient.cs               # Contract: ListTools, InvokeTool (HTTP abstraction)
│   │   ├── McpClient.cs                # Implementation: HttpClient + Polly retry policies
│   │   ├── McpToolDiscovery.cs         # Background service: Discovers tools at startup
│   │   └── McpToToolDescriptorMapper.cs # Mapping: MCP tool schema → ToolDescriptor
│   │
│   ├── Observability/                  # M3: Telemetry
│   │   ├── InvocationTelemetry.cs      # OpenTelemetry ActivitySource + Meter
│   │   └── InvocationMetrics.cs        # Metrics: tool.duration, tool.errors, registry.lookup.miss
│   │
│   └── Integration/                    # M5: Conversation Glue
│       ├── FunctoolsChatClient.cs      # Decorator: IChatClient wrapper with functools parsing
│       └── ToolMessageFormatter.cs     # Format ToolResult → ChatMessage (role: tool)
│
├── Phi4WeatherAgent.Web/               # Blazor UI (existing, minimal changes)
│   └── Program.cs                      # Update DI registration to use FunctoolsChatClient
│
└── Phi4WeatherAgent.Tools/             # NEW: Local tool implementations
    ├── Phi4WeatherAgent.Tools.csproj
    ├── WeatherTools.cs                 # [Tool("GetWeather")] methods
    ├── GeocodingTools.cs               # [Tool("GeocodeLocation")] methods
    └── AllergenTools.cs                # [Tool("GetAllergenData")] methods

tests/
├── Phi4WeatherAgent.Agent.Tests/       # NEW: Unit + Integration tests
│   ├── Parsing/
│   │   ├── FunctoolsParserTests.cs     # Valid JSON, malformed JSON, streaming chunks
│   │   └── ParserBenchmarks.cs         # BenchmarkDotNet: <50ms for 1MB block
│   │
│   ├── Registry/
│   │   ├── ToolRegistryTests.cs        # Thread-safe registration, concurrent lookups
│   │   └── RegistryBenchmarks.cs       # BenchmarkDotNet: <1μs lookup
│   │
│   ├── Dispatching/
│   │   ├── ToolInvokerTests.cs         # Validation, whitelist, timeout, cancellation
│   │   └── DispatcherBenchmarks.cs     # BenchmarkDotNet: <5ms validation
│   │
│   ├── McpAdapter/
│   │   ├── McpClientTests.cs           # Mock MCP server (TestContainers), retry policies
│   │   └── McpDiscoveryTests.cs        # Startup discovery, graceful degradation
│   │
│   └── E2E/
│       ├── EndToEndTests.cs            # Real Phi-4-mini + MCP tools → full conversation
│       └── StressTests.cs              # 100 concurrent invocations, 1000 sequential calls
│
└── Phi4WeatherAgent.Web.Tests/         # Existing UI tests (no changes required)
```

**Key Additions:**
- **Phi4WeatherAgent.Agent** project: Core invocation layer (M1-M5 implementation)
- **Phi4WeatherAgent.Tools** project: Local tool implementations with `[Tool]` attributes
- **Phi4WeatherAgent.Agent.Tests** project: Comprehensive test suite (unit/integration/E2E/benchmarks)

**Integration Points:**
- `Phi4WeatherAgent.AppHost/Program.cs`: Register `IToolRegistry`, `IToolInvoker`, `IFunctoolsParser`, `IMcpClient` in DI
- `Phi4WeatherAgent.ServiceDefaults/Extensions.cs`: Configure OpenTelemetry ActivitySource for invocation layer
- `Phi4WeatherAgent.Web/Program.cs`: Replace `IChatClient` with `FunctoolsChatClient` decorator

## Complexity Tracking

No constitutional violations require justification. All new code aligns with established principles. Custom functools parser is explicitly approved exception in Principle III.

## Phase 0: Research & Decision Log

**Goal**: Resolve all NEEDS CLARIFICATION items from Technical Context, validate technology choices, document alternatives considered.

### Research Tasks

1. **JSON Schema Library Selection**
   - **Question**: Which library for argument validation (JsonSchema.Net vs alternatives)?
   - **Requirements**: <5ms validation, supports JSON Schema Draft 2020-12, minimal allocations
   - **Candidates**: JsonSchema.Net, NJsonSchema, Manatee.Json
   - **Research Output**: `research.md` section "JSON Schema Libraries" with benchmark comparison

2. **MCP Protocol Specification**
   - **Question**: What is the exact HTTP contract for `ListTools` and tool invocation?
   - **Requirements**: Request/response schemas, error codes, authentication patterns
   - **Sources**: MCP specification docs, example implementations
   - **Research Output**: `research.md` section "MCP Protocol Details" with sample requests/responses

3. **Aspire DI Patterns**
   - **Question**: How to register multiple tool implementations dynamically discovered via reflection?
   - **Requirements**: Support for `[Tool]` attribute scanning, keyed services for tool lookup
   - **Sources**: Aspire docs, Microsoft.Extensions.DependencyInjection patterns
   - **Research Output**: `research.md` section "Tool Discovery Patterns" with code samples

4. **Source Generator Feasibility (Optional)**
   - **Question**: Can we generate tool registry at compile-time for AOT compatibility?
   - **Requirements**: Incremental generator, discovers `[Tool]` attributes, emits registration code
   - **Risk**: Adds complexity, may not be needed if AOT support is low priority
   - **Research Output**: `research.md` section "AOT Source Generator" with decision (implement vs defer)

5. **Streaming Parser Design**
   - **Question**: How to detect functools blocks in streaming responses without false positives?
   - **Requirements**: Handle partial blocks (e.g., "functools[{\"name\": \"GetW" mid-stream)
   - **Approach**: State machine vs regex vs buffer-based detection
   - **Research Output**: `research.md` section "Streaming Detection" with algorithm pseudocode

### Research Deliverable

**File**: `specs/002-functools-invocation-layer/research.md`

**Template Structure**:
```markdown
# Research Log: Functools Invocation Layer

## JSON Schema Libraries
- **Decision**: [Selected library name + version]
- **Rationale**: [Performance, API simplicity, maintenance status]
- **Alternatives Considered**: [Other libraries + why rejected]
- **Benchmark Results**: [Validation time for 10KB schema]

## MCP Protocol Details
- **Decision**: [HTTP contract summary]
- **Rationale**: [Aligned with spec version X.Y]
- **Sample Requests**: [ListTools, InvokeTool cURL examples]
- **Error Handling**: [Status codes, retry logic]

## Tool Discovery Patterns
- **Decision**: [Reflection-based vs Source Generator]
- **Rationale**: [Simplicity vs AOT compatibility tradeoff]
- **Implementation**: [Code sample for attribute scanning]

## AOT Source Generator
- **Decision**: [DEFER to Phase 2 or IMPLEMENT NOW]
- **Rationale**: [User demand, complexity cost]
- **Prototype**: [Link to PoC branch if implemented]

## Streaming Detection
- **Decision**: [State machine algorithm]
- **Rationale**: [Avoids regex backtracking, handles partial blocks]
- **Pseudocode**: [State transitions for "functools[" detection]
```

## Phase 1: Design & Contracts

**Goal**: Define data models, API contracts, and quickstart guide. Update agent context with new technologies.

### Task 1: Data Model Definition

**File**: `specs/002-functools-invocation-layer/data-model.md`

**Content**:
- **FunctionCall**: Name (string, non-null), Arguments (JsonElement, may be empty object)
  - Validation Rules: Name matches `^[a-zA-Z][a-zA-Z0-9_]*$`, max 100 chars
  - State Transitions: None (immutable value object)

- **ToolDescriptor**: Name, Source, ArgsSchema (nullable), Invoker (delegate), SecurityClass (enum), Timeout
  - Validation Rules: Name unique in registry, Invoker non-null, Timeout >0
  - Relationships: One-to-many from Source → Descriptors (e.g., "MCP:weather-api" → multiple tools)
  - State Transitions: Registered → Active (in registry), Unregistered (removed)

- **ToolResult**: Name, Content (nullable), Error (nullable), Duration, Meta
  - Validation Rules: Exactly one of Content or Error must be non-null
  - State Transitions: Pending → Success (Content set) OR Failed (Error set)

### Task 2: API Contracts

**Directory**: `specs/002-functools-invocation-layer/contracts/`

**Files**:

1. **IFunctoolsParser.cs**
```csharp
namespace Phi4WeatherAgent.Agent.Parsing;

public interface IFunctoolsParser
{
    /// <summary>
    /// Parses functools blocks from model response chunk.
    /// </summary>
    /// <param name="chunk">Text chunk from streaming response</param>
    /// <returns>Enumerable of FunctionCall objects (may be empty if no complete blocks)</returns>
    /// <exception cref="ParserException">Thrown for malformed JSON or invalid structure</exception>
    IEnumerable<FunctionCall> Parse(ReadOnlySpan<char> chunk);
}
```

2. **IToolRegistry.cs**
```csharp
namespace Phi4WeatherAgent.Agent.Registry;

public interface IToolRegistry
{
    /// <summary>
    /// Attempts to retrieve tool descriptor by name (case-insensitive).
    /// </summary>
    bool TryGet(string name, [NotNullWhen(true)] out ToolDescriptor? descriptor);
    
    /// <summary>
    /// Registers a new tool. Throws if name already exists.
    /// </summary>
    void Register(ToolDescriptor descriptor);
    
    /// <summary>
    /// Lists all registered tools asynchronously.
    /// </summary>
    IAsyncEnumerable<ToolDescriptor> ListAsync(CancellationToken ct = default);
}
```

3. **IToolInvoker.cs**
```csharp
namespace Phi4WeatherAgent.Agent.Dispatching;

public interface IToolInvoker
{
    /// <summary>
    /// Invokes a tool by name with provided arguments.
    /// </summary>
    /// <param name="name">Tool name (must exist in registry)</param>
    /// <param name="args">JSON arguments (validated against tool schema)</param>
    /// <param name="ct">Cancellation token (respects tool timeout)</param>
    /// <returns>ToolResult with Content (success) or Error (failure)</returns>
    Task<ToolResult> InvokeAsync(string name, JsonElement args, CancellationToken ct);
}
```

### Task 3: Quickstart Guide

**File**: `specs/002-functools-invocation-layer/quickstart.md`

**Content**:
- **Prerequisites**: .NET 10 SDK, Foundry Local running, Aspire workload installed
- **Step 1**: Create new C# class with `[Tool("HelloWorld")]` attribute
- **Step 2**: Implement method: `string SayHello(string name) => $"Hello, {name}!";`
- **Step 3**: Restart Aspire AppHost (tools auto-discovered at startup)
- **Step 4**: Send prompt to Phi-4-mini: "Say hello to Alice"
- **Expected Output**: Model responds with `functools[{"name": "HelloWorld", "arguments": {"name": "Alice"}}]`, tool executes, model receives result "Hello, Alice!", final response "Hello, Alice! Welcome!"
- **Verification**: Check Aspire Dashboard for OpenTelemetry trace showing parse → dispatch → execute spans

### Task 4: Agent Context Update

**Script**: `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot`

**Action**: Detect Copilot agent, append to `.github/.copilot-instructions.md`:
```markdown
## Functools Invocation Layer (Added 2025-11-16)
- Parser: Detects functools[...] blocks in Phi-4-mini responses (System.Text.Json)
- Registry: Thread-safe tool lookup (ConcurrentDictionary)
- Dispatcher: Validates arguments with JsonSchema.Net, enforces allowlist
- MCP Adapter: Discovers tools via ListTools HTTP endpoint (Polly retry)
- Telemetry: OpenTelemetry ActivitySource "Phi4WeatherAgent.Agent.Invocation"
- Testing: BenchmarkDotNet for parser (<50ms), dispatcher (<5ms), registry (<1μs)
```

**File Modified**: `.github/.copilot-instructions.md` (preserves existing content between markers)

## Phase 1 Deliverables

- ✅ `research.md`: Technology decisions documented with rationale
- ✅ `data-model.md`: Entity definitions with validation rules
- ✅ `contracts/IFunctoolsParser.cs`: Parser interface
- ✅ `contracts/IToolRegistry.cs`: Registry interface  
- ✅ `contracts/IToolInvoker.cs`: Dispatcher interface
- ✅ `quickstart.md`: Hello-world scenario with expected output
- ✅ `.github/.copilot-instructions.md`: Updated with new technologies

## Re-Check Constitution (Post-Design)

All principles remain PASS. No new violations introduced. Zero-code extensibility preserved through `[Tool]` attributes and MCP config files. Performance targets validated feasible through research (JSON Schema validation <5ms confirmed).

## Next Steps

**Command**: `/speckit.tasks`  
**Input**: This plan + spec + research + data-model + contracts  
**Output**: `tasks.md` with granular task breakdown (M1-M6 milestones → subtasks with acceptance tests)

**Expected Task Count**: ~30-40 tasks across milestones:
- M1 Parser: 8-10 tasks (streaming detection, JSON parsing, error handling, tests, benchmarks)
- M2 Dispatcher + Registry: 10-12 tasks (validation, whitelist, registration, attribute discovery, tests)
- M3 Telemetry + Policies: 6-8 tasks (ActivitySource, Meter, Polly policies, Dashboard integration)
- M4 MCP Bridge: 6-8 tasks (HTTP client, discovery service, mapping, retry logic, tests)
- M5 Conversation Glue: 4-5 tasks (IChatClient decorator, message formatting, integration tests)
- M6 Aspire + E2E + Benchmarks: 4-6 tasks (DI registration, E2E scenarios, stress tests, benchmarks)

| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
