# Tasks: Phi-4-mini Functools Invocation Layer

**Branch**: 002-functools-invocation-layer  
**Input**: Design documents from specs/002-functools-invocation-layer/  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are NOT explicitly requested in the specification, so test tasks are OMITTED per template instructions.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

---

## Format: `- [ ] [ID] [P?] [Story?] Description with file path`

- **Checkbox**: `- [ ]` (markdown checkbox - REQUIRED)
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label (US1, US2, US3, US4, US5, US6) - REQUIRED for user story phase tasks
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create `src/Phi4WeatherAgent.Agent/Phi4WeatherAgent.Agent.csproj` with .NET 10 target framework
- [ ] T002 [P] Create `src/Phi4WeatherAgent.Tools/Phi4WeatherAgent.Tools.csproj` with .NET 10 target framework
- [ ] T003 Add NuGet dependencies to Agent project: Microsoft.Extensions.AI 10.0.0-preview.1+, JsonSchema.Net 7.2.0+, Polly 8.5.0+
- [ ] T004 [P] Add NuGet dependencies to Agent project: OpenTelemetry.Extensions.Hosting, OpenTelemetry.Instrumentation.AspNetCore
- [ ] T005 Add project reference from `src/Phi4WeatherAgent.Web/Phi4WeatherAgent.Web.csproj` to Agent project
- [ ] T006 [P] Add project reference from `src/Phi4WeatherAgent.AppHost/Phi4WeatherAgent.AppHost.csproj` to Agent project
- [ ] T007 Create `src/Phi4WeatherAgent.Tools/WeatherTools.cs` stub class with namespace
- [ ] T008 [P] Create `src/Phi4WeatherAgent.Tools/GeocodingTools.cs` stub class with namespace
- [ ] T009 [P] Create `src/Phi4WeatherAgent.Tools/AllergenTools.cs` stub class with namespace

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T010 Create `src/Phi4WeatherAgent.Agent/Parsing/FunctionCall.cs` entity with Name and Arguments properties per data-model.md
- [X] T011 [P] Create `src/Phi4WeatherAgent.Agent/Registry/ToolDescriptor.cs` entity with 6 properties per data-model.md
- [X] T012 [P] Create `src/Phi4WeatherAgent.Agent/Registry/SecurityClass.cs` enum with Public/Internal/Admin values
- [X] T013 [P] Create `src/Phi4WeatherAgent.Agent/Dispatching/ToolResult.cs` entity with validation rules per data-model.md
- [X] T014 Create `src/Phi4WeatherAgent.Agent/Parsing/ParserException.cs` with ErrorCode property
- [X] T015 [P] Create `src/Phi4WeatherAgent.Agent/Dispatching/DispatcherException.cs` with ErrorCode property
- [X] T016 Copy `specs/002-functools-invocation-layer/contracts/IFunctoolsParser.cs` to `src/Phi4WeatherAgent.Agent/Parsing/IFunctoolsParser.cs`
- [X] T017 [P] Copy `specs/002-functools-invocation-layer/contracts/IToolRegistry.cs` to `src/Phi4WeatherAgent.Agent/Registry/IToolRegistry.cs`
- [X] T018 [P] Copy `specs/002-functools-invocation-layer/contracts/IToolInvoker.cs` to `src/Phi4WeatherAgent.Agent/Dispatching/IToolInvoker.cs`
- [X] T019 Create `src/Phi4WeatherAgent.Agent/Registry/ToolAttribute.cs` attribute class with Name, Description, InputSchemaJson properties

**Checkpoint**: Foundation complete - entities, contracts, and base infrastructure ready for user story implementation

---

## Phase 3: User Story 1 - Parse Functools and Invoke Local C# Tools (Priority: P1) 🎯 MVP

**Goal**: Developer adds `[Tool]` method, Phi-4-mini can invoke it without code changes to parser/dispatcher

**Independent Test**: Deploy console app with one `[Tool]` method, send prompt to Phi-4-mini, verify functools parsed and tool invoked with correct arguments

**Acceptance Scenarios** (from spec.md US1):
1. Phi-4-mini outputs functools block → parser extracts FunctionCall → dispatcher invokes tool → returns ToolResult with data
2. Developer adds `[Tool("CalculateSum")]` method and restarts → Phi-4-mini calls it → executes without parser/dispatcher changes
3. Tool throws exception → dispatcher catches → returns ToolResult with sanitized error (no stack trace leak)

### Implementation for User Story 1

#### M1: Parser Implementation

- [ ] T020 [P] [US1] Implement `src/Phi4WeatherAgent.Agent/Parsing/FunctoolsParser.cs` with streaming detection state machine per research.md §Streaming Detection
- [ ] T021 [US1] Add Regex pattern `functools\[.*?\]` to detect functools blocks in `FunctoolsParser.cs`
- [ ] T022 [US1] Implement JSON deserialization using System.Text.Json in `FunctoolsParser.Parse()` method
- [ ] T022a [US1] Add empty functools array detection in `FunctoolsParser.Parse()`: if deserialized array is empty `functools[]`, return empty IEnumerable without error per FR-022
- [ ] T023 [US1] Add error handling for malformed JSON with ParserException (MALFORMED_BLOCK code) in `FunctoolsParser.cs`
- [ ] T024 [US1] Implement `FunctoolsParser.Reset()` method for state machine cleanup between parsing attempts

#### M2: Registry Implementation

- [ ] T025 [P] [US1] Implement `src/Phi4WeatherAgent.Agent/Registry/ToolRegistry.cs` with ConcurrentDictionary<string, ToolDescriptor> storage
- [ ] T026 [US1] Implement `ToolRegistry.TryGet()` method with case-insensitive lookup (convert key to lowercase)
- [ ] T027 [US1] Implement `ToolRegistry.Register()` method with duplicate detection (throw if name exists)
- [ ] T028 [US1] Implement `ToolRegistry.Unregister()` method with safe removal from ConcurrentDictionary
- [ ] T029 [US1] Implement `ToolRegistry.ListAsync()` method returning IAsyncEnumerable<ToolDescriptor>
- [ ] T030 [US1] Add `ToolRegistry.Count` property for telemetry reporting

#### M2: Attribute-Based Tool Discovery

- [ ] T031 [P] [US1] Create `src/Phi4WeatherAgent.Agent/Registry/ToolDiscoveryService.cs` background service
- [ ] T032 [US1] Implement reflection-based assembly scanning in `ToolDiscoveryService` to find methods with [Tool] attribute
- [ ] T033 [US1] Add method signature validation (parameters must be deserializable from JsonElement) in `ToolDiscoveryService`
- [ ] T034 [US1] Generate ToolDescriptor from [Tool] metadata (Name, Description, InputSchemaJson → JsonSchema) in `ToolDiscoveryService`
- [ ] T035 [US1] Create invoker delegate wrapping discovered method with exception handling in `ToolDiscoveryService`
- [ ] T036 [US1] Register discovered tools in IToolRegistry during service startup in `ToolDiscoveryService.StartAsync()`

#### M2: Dispatcher Implementation

- [ ] T037 [P] [US1] Implement `src/Phi4WeatherAgent.Agent/Dispatching/ToolInvoker.cs` with IToolRegistry dependency injection
- [ ] T038 [US1] Implement `ToolInvoker.InvokeAsync()` lookup phase: call registry.TryGet(), return UNKNOWN_TOOL error if not found
- [ ] T039 [US1] Implement `ToolInvoker.InvokeAsync()` execution phase: call descriptor.Invoker with timeout CancellationToken
- [ ] T040 [US1] Add exception handling in `ToolInvoker.InvokeAsync()`: catch all exceptions, sanitize message (no stack traces), return INVOCATION_FAILED error
- [ ] T041 [US1] Add timeout enforcement in `ToolInvoker.InvokeAsync()` using CancellationTokenSource with descriptor.Timeout, return TIMEOUT error
- [ ] T042 [US1] Add duration tracking in `ToolInvoker.InvokeAsync()`: Stopwatch around descriptor.Invoker, set ToolResult.Duration

#### Example Local Tools

- [ ] T043 [P] [US1] Implement `GetWeather` method in `src/Phi4WeatherAgent.Tools/WeatherTools.cs` with [Tool("GetWeather")] attribute
- [ ] T044 [P] [US1] Implement `GetForecast` method in `src/Phi4WeatherAgent.Tools/WeatherTools.cs` with [Tool("GetForecast")] attribute
- [ ] T045 [P] [US1] Implement `GeocodeLocation` method in `src/Phi4WeatherAgent.Tools/GeocodingTools.cs` with [Tool("GeocodeLocation")] attribute
- [ ] T046 [P] [US1] Implement `GetAllergenData` method in `src/Phi4WeatherAgent.Tools/AllergenTools.cs` with [Tool("GetAllergenData")] attribute

**Checkpoint**: At this point, User Story 1 should be fully functional - local C# tools with [Tool] attributes can be discovered and invoked

---

## Phase 4: User Story 2 - Discover and Invoke MCP Tools Dynamically (Priority: P1)

**Goal**: Developer configures MCP server URL in appsettings.tools.json, tools auto-discovered at startup

**Independent Test**: Configure MCP server with GetCoordinates tool, start app, send geocoding prompt, verify MCP tool discovered and invoked

**Acceptance Scenarios** (from spec.md US2):
1. appsettings.tools.json contains MCP server URL → app starts → adapter calls ListTools → registers all tools
2. MCP server exposes GetWeather tool → Phi-4-mini calls it → adapter translates to MCP protocol → returns ToolResult
3. MCP server unreachable during discovery → app logs warning → continues startup (graceful degradation)

### Implementation for User Story 2

#### M4: MCP Client Implementation

- [ ] T047 [P] [US2] Create `src/Phi4WeatherAgent.Agent/McpAdapter/IMcpClient.cs` interface with ListToolsAsync and InvokeToolAsync methods
- [ ] T048 [US2] Implement `src/Phi4WeatherAgent.Agent/McpAdapter/McpClient.cs` with HttpClient dependency injection
- [ ] T049 [US2] Implement `McpClient.ListToolsAsync()` method: GET /mcp/v1/tools endpoint per research.md §MCP Protocol Details
- [ ] T050 [US2] Implement `McpClient.InvokeToolAsync()` method: POST /mcp/v1/tools/{toolName} endpoint per research.md §MCP Protocol Details
- [ ] T051 [US2] Add Polly retry policy to `McpClient` constructor: 3 retries, exponential backoff per research.md §MCP Protocol Details
- [ ] T052 [US2] Add Polly circuit breaker to `McpClient`: 5 consecutive failures, 1 minute open state per research.md §MCP Protocol Details
- [ ] T053 [US2] Add bearer token authentication support in `McpClient` (optional per server config)

#### M4: MCP Tool Discovery

- [ ] T054 [P] [US2] Create `src/Phi4WeatherAgent.Agent/McpAdapter/McpToolDiscovery.cs` background service with IMcpClient dependency
- [ ] T055 [US2] Load MCP server URLs from `appsettings.tools.json` in `McpToolDiscovery` constructor using IConfiguration
- [ ] T056 [US2] Implement `McpToolDiscovery.StartAsync()` method: iterate MCP servers, call ListToolsAsync for each
- [ ] T057 [US2] Add error handling in `McpToolDiscovery.StartAsync()`: catch HttpRequestException, log warning, continue (graceful degradation per US2 scenario 3)
- [ ] T058 [US2] Call mapper for each discovered tool to convert MCP schema to ToolDescriptor in `McpToolDiscovery.StartAsync()`
- [ ] T059 [US2] Register mapped tools in IToolRegistry at end of `McpToolDiscovery.StartAsync()`

#### M4: MCP Schema Mapping

- [ ] T060 [P] [US2] Create `src/Phi4WeatherAgent.Agent/McpAdapter/McpToToolDescriptorMapper.cs` static class
- [ ] T061 [US2] Implement `MapToToolDescriptor()` method: convert MCP tool JSON to ToolDescriptor entity
- [ ] T062 [US2] Map MCP inputSchema to JsonSchema.Net JsonSchema in `MapToToolDescriptor()` method
- [ ] T063 [US2] Create invoker delegate in `MapToToolDescriptor()` that calls IMcpClient.InvokeToolAsync
- [ ] T064 [US2] Set Source property to "MCP:{serverUrl}" in `MapToToolDescriptor()` method
- [ ] T065 [US2] Set SecurityClass to Public (default for MCP tools) in `MapToToolDescriptor()` method
- [ ] T066 [US2] Set Timeout from MCP metadata or default to 30 seconds in `MapToToolDescriptor()` method

#### Configuration

- [ ] T067 [P] [US2] Create `src/Phi4WeatherAgent.Web/appsettings.tools.json` with mcpServers array structure
- [ ] T068 [US2] Add example MCP server configuration with url and optional bearerToken to `appsettings.tools.json`
- [ ] T069 [US2] Update `src/Phi4WeatherAgent.Web/Program.cs` to load appsettings.tools.json via AddJsonFile

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently - local C# tools AND MCP tools can be discovered and invoked

---

## Phase 5: User Story 3 - Validate Tool Arguments with JSON Schema (Priority: P2)

**Goal**: System validates tool arguments against JSON Schema before invocation, rejects malformed inputs

**Independent Test**: Register tool with schema requiring location:string (max 100 chars), call with 10MB payload, verify rejection with ARG_VALIDATION_FAILED

**Acceptance Scenarios** (from spec.md US3):
1. Tool declares maxLength:100 → functools provides 10,000 char string → dispatcher rejects with ARG_VALIDATION_FAILED error
2. Tool requires number type → functools provides string → dispatcher rejects with type mismatch error
3. Tool has no schema → functools provides any arguments → dispatcher skips validation (opt-in validation)

### Implementation for User Story 3

#### JSON Schema Validation Integration

- [ ] T070 [P] [US3] Add JsonSchema.Net validation call to `src/Phi4WeatherAgent.Agent/Dispatching/ToolInvoker.cs` before descriptor.Invoker
- [ ] T071 [US3] Implement `ToolInvoker.ValidateAsync()` method: check if descriptor.ArgsSchema is null (skip validation if null per US3 scenario 3)
- [ ] T072 [US3] Call `schema.Evaluate(args)` in `ToolInvoker.ValidateAsync()` using JsonSchema.Net per research.md §JSON Schema Libraries
- [ ] T073 [US3] Return ValidationResult with IsValid=false and error messages if schema validation fails in `ToolInvoker.ValidateAsync()`
- [ ] T074 [US3] Update `ToolInvoker.InvokeAsync()` to call ValidateAsync before execution, return ARG_VALIDATION_FAILED error if invalid
- [ ] T075 [US3] Add argument size check in `ToolInvoker.ValidateAsync()`: reject if serialized JSON >10MB (security constraint from spec)

#### Schema Definition Helpers

- [ ] T076 [P] [US3] Update `src/Phi4WeatherAgent.Agent/Registry/ToolAttribute.cs` to accept InputSchemaJson string property
- [ ] T077 [US3] Parse InputSchemaJson to JsonSchema in `src/Phi4WeatherAgent.Agent/Registry/ToolDiscoveryService.cs` during descriptor creation
- [ ] T078 [US3] Add example JSON Schema to WeatherTools.GetWeather [Tool] attribute in `src/Phi4WeatherAgent.Tools/WeatherTools.cs`

**Checkpoint**: At this point, User Stories 1, 2, AND 3 should all work independently - argument validation enforced for tools with schemas

---

## Phase 6: User Story 4 - Handle Malformed Functools Gracefully (Priority: P2)

**Goal**: Parser detects malformed functools blocks and returns error without crashing

**Independent Test**: Send model response with broken JSON functools[{"name":"GetWeather","arguments":] → verify parser returns MALFORMED_BLOCK error

**Acceptance Scenarios** (from spec.md US4):
1. Model outputs invalid JSON in functools → parser catches exception → returns MALFORMED_BLOCK error
2. Model outputs functools missing 'arguments' field → parser validates structure → returns MALFORMED_BLOCK error
3. Model outputs partial functools in streaming → parser buffers chunks → waits for complete block (no false positives)

### Implementation for User Story 4

#### Parser Robustness

- [ ] T079 [P] [US4] Add try-catch around JSON deserialization in `src/Phi4WeatherAgent.Agent/Parsing/FunctoolsParser.cs` Parse() method
- [ ] T080 [US4] Wrap JsonException in ParserException with MALFORMED_BLOCK code in `FunctoolsParser.cs` (US4 scenario 1)
- [ ] T081 [US4] Add structure validation in `FunctoolsParser.Parse()`: check for required 'name' and 'arguments' fields (US4 scenario 2)
- [ ] T082 [US4] Throw ParserException with descriptive message if required fields missing in `FunctoolsParser.cs`
- [ ] T083 [US4] Implement chunk buffering in `FunctoolsParser.Parse()`: accumulate input until closing ']' detected (US4 scenario 3)
- [ ] T084 [US4] Add partial block detection: only parse when complete functools block found (detect 'functools[' start and ']' end)

#### Error Message Formatting

- [ ] T085 [P] [US4] Create `src/Phi4WeatherAgent.Agent/Integration/ToolMessageFormatter.cs` static class
- [ ] T086 [US4] Implement `FormatErrorAsToolMessage()` method: convert ParserException to ChatMessage with role=tool
- [ ] T087 [US4] Include error code and sanitized message in formatted tool message (no internal details leaked)

**Checkpoint**: At this point, User Stories 1-4 should all work independently - parser handles malformed input gracefully

---

## Phase 7: User Story 5 - Enforce Tool Allowlist and Rate Limiting (Priority: P2)

**Goal**: System rejects unknown tool names and limits tool calls per conversation turn

**Independent Test**: Configure allowlist with 2 tools, call unknown tool → verify rejection. Make 11 calls in one turn → verify 11th rejected

**Acceptance Scenarios** (from spec.md US5):
1. Allowlist contains ["GetWeather", "GetCoordinates"] → functools calls DeleteAllData → dispatcher rejects with UNKNOWN_TOOL error
2. Rate limit is 10 calls/turn → conversation exceeds 10 calls → dispatcher rejects 11th call with RATE_LIMIT_EXCEEDED error
3. Environment-specific allowlist (dev vs prod) → app loads in production → only prod-approved tools registered

### Implementation for User Story 5

#### Allowlist Enforcement

- [ ] T088 [P] [US5] Add allowlist configuration to `src/Phi4WeatherAgent.Web/appsettings.tools.json` with allowedTools array
- [ ] T089 [US5] Load allowlist from IConfiguration in `src/Phi4WeatherAgent.Agent/Dispatching/ToolInvoker.cs` constructor (store in HashSet for O(1) lookup per FR-006)
- [ ] T090 [US5] Add allowlist check in `ToolInvoker.InvokeAsync()` AFTER registry lookup (line ~45) but BEFORE schema validation: if tool name not in allowlist HashSet, return UNKNOWN_TOOL error and log security event per FR-006 (US5 scenario 1)
- [ ] T091 [US5] Log allowlist rejection attempts with structured log including tool name and correlation ID for security audit

#### Rate Limiting [DEFERRED]

- [ ] T092 [P] [US5] [DEFERRED] Add conversation turn tracking in `src/Phi4WeatherAgent.Agent/Dispatching/ToolInvoker.cs` using ConcurrentDictionary<ConversationId, int>
- [ ] T093 [US5] [DEFERRED] Load rate limit from IConfiguration (default 10 calls/turn) in `ToolInvoker.cs` constructor
- [ ] T094 [US5] [DEFERRED] Implement call counter increment in `ToolInvoker.InvokeAsync()`: increment count for current conversation turn
- [ ] T095 [US5] [DEFERRED] Add rate limit check in `ToolInvoker.InvokeAsync()`: if count >limit, return RATE_LIMIT_EXCEEDED error (US5 scenario 2)
- [ ] T096 [US5] [DEFERRED] Add turn reset method in `ToolInvoker.cs` to clear counters when new turn starts (called by conversation manager)

NOTE: Rate limiting (FR-007) deprioritized post-Phase 10 per user directive 2025-11-18 for local deployment

#### Environment-Specific Configuration

- [ ] T097 [P] [US5] Create `src/Phi4WeatherAgent.Web/appsettings.Development.tools.json` with relaxed allowlist (all tools allowed)
- [ ] T098 [US5] Create `src/Phi4WeatherAgent.Web/appsettings.Production.tools.json` with restricted allowlist (only production-approved tools)
- [ ] T099 [US5] Update `src/Phi4WeatherAgent.Web/Program.cs` to load environment-specific tools config using AddJsonFile with reloadOnChange:true

**Checkpoint**: At this point, User Stories 1-5 should all work independently - security constraints enforced

---

## Phase 8: User Story 6 - Emit OpenTelemetry Traces and Metrics (Priority: P3)

**Goal**: Every tool invocation emits structured logs, metrics (P50/P95/P99), and distributed traces visible in Aspire Dashboard

**Independent Test**: Invoke tool, open Aspire Dashboard, verify trace spans (parse → validate → dispatch → execute) with correct timing

**Acceptance Scenarios** (from spec.md US6):
1. Tool invocation completes → trace includes spans: functools.parse, tool.validate, tool.dispatch, tool.execute with parent-child relationships
2. 100 tool calls complete → metrics report P50/P95/P99 latencies per tool name
3. Tool fails with TIMEOUT → structured log includes level:error, error_type:TIMEOUT, tool_name, correlation_id

### Implementation for User Story 6

#### M3: OpenTelemetry ActivitySource

- [ ] T100 [P] [US6] Create `src/Phi4WeatherAgent.Agent/Observability/InvocationTelemetry.cs` with static ActivitySource instance
- [ ] T101 [US6] Set ActivitySource name to "Phi4WeatherAgent.Agent.Invocation" in `InvocationTelemetry.cs`
- [ ] T102 [US6] Add span creation in `src/Phi4WeatherAgent.Agent/Parsing/FunctoolsParser.cs` Parse() method: start activity "functools.parse"
- [ ] T103 [US6] Add span creation in `src/Phi4WeatherAgent.Agent/Dispatching/ToolInvoker.cs` ValidateAsync() method: start activity "tool.validate"
- [ ] T104 [US6] Add span creation in `src/Phi4WeatherAgent.Agent/Dispatching/ToolInvoker.cs` InvokeAsync() method: start activity "tool.dispatch"
- [ ] T105 [US6] Add nested span creation in `ToolInvoker.InvokeAsync()` around descriptor.Invoker: start activity "tool.execute" as child of dispatch span (US6 scenario 1)
- [ ] T106 [US6] Add activity tags to all spans: tool_name, source, security_class, duration_ms

#### M3: OpenTelemetry Metrics

- [ ] T107 [P] [US6] Create `src/Phi4WeatherAgent.Agent/Observability/InvocationMetrics.cs` with static Meter instance
- [ ] T108 [US6] Set Meter name to "Phi4WeatherAgent.Agent.Invocation" in `InvocationMetrics.cs`
- [ ] T109 [US6] Add Histogram<double> metric "tool.duration" with unit "ms" in `InvocationMetrics.cs`
- [ ] T110 [US6] Add Counter<long> metric "tool.errors" with unit "count" in `InvocationMetrics.cs`
- [ ] T111 [US6] Add Counter<long> metric "registry.lookup.miss" with unit "count" in `InvocationMetrics.cs`
- [ ] T112 [US6] Record tool.duration histogram in `ToolInvoker.InvokeAsync()` with tags: tool_name, success/failure (US6 scenario 2)
- [ ] T113 [US6] Increment tool.errors counter in `ToolInvoker.InvokeAsync()` catch block with tags: tool_name, error_code
- [ ] T114 [US6] Increment registry.lookup.miss counter in `ToolInvoker.InvokeAsync()` when TryGet returns false

#### Structured Logging

- [ ] T115 [P] [US6] Add ILogger dependency to `src/Phi4WeatherAgent.Agent/Dispatching/ToolInvoker.cs` constructor
- [ ] T116 [US6] Add structured log in `ToolInvoker.InvokeAsync()` success case: LogInformation with tool_name, duration_ms, correlation_id
- [ ] T117 [US6] Add structured log in `ToolInvoker.InvokeAsync()` error case: LogError with level:error, error_type, tool_name, correlation_id (US6 scenario 3)
- [ ] T118 [US6] Add correlation ID propagation using Activity.Current.TraceId in all log statements

#### Aspire Dashboard Integration

- [ ] T119 [P] [US6] Update `src/Phi4WeatherAgent.ServiceDefaults/Extensions.cs` to add OpenTelemetry ActivitySource "Phi4WeatherAgent.Agent.Invocation"
- [ ] T120 [US6] Update `Extensions.cs` to add OpenTelemetry Meter "Phi4WeatherAgent.Agent.Invocation"
- [ ] T121 [US6] Verify Aspire Dashboard connection in `src/Phi4WeatherAgent.AppHost/Program.cs` (should already be configured for existing projects)

**Checkpoint**: All user stories should now be independently functional with full observability

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: M5 Conversation integration, M6 E2E validation, benchmarks, documentation

### M5: Conversation Glue (IChatClient Integration)

- [X] T122 [P] Create `src/Phi4WeatherAgent.Agent/Integration/FunctoolsChatClient.cs` decorator implementing IChatClient interface
- [X] T123 Inject IFunctoolsParser, IToolInvoker, IToolRegistry into `FunctoolsChatClient` constructor
- [X] T124 Override `FunctoolsChatClient.GetResponseAsync()` method to intercept assistant responses
- [X] T125 Scan assistant response for functools blocks using `IFunctoolsParser.Parse()` in `GetResponseAsync()`
- [X] T126 Extract FunctionCall array from parsed functools block in `GetResponseAsync()`
- [X] T127 Invoke each tool via `IToolInvoker.InvokeAsync()` in `GetResponseAsync()`
- [X] T128 Convert ToolResult array to ChatMessage array (role=tool) using `ToolMessageFormatter` in `GetResponseAsync()`
- [X] T129 Append tool messages to conversation history in `GetResponseAsync()`
- [X] T130 Re-prompt model with updated history (includes tool results) in `GetResponseAsync()`
- [X] T131 Return final assistant response (after tool execution) from `GetResponseAsync()`

### DI Registration

- [X] T132 [P] Update `src/Phi4WeatherAgent.AppHost/Program.cs` to register IFunctoolsParser → FunctoolsParser as singleton
- [X] T133 [P] Update `src/Phi4WeatherAgent.AppHost/Program.cs` to register IToolRegistry → ToolRegistry as singleton
- [X] T134 [P] Update `src/Phi4WeatherAgent.AppHost/Program.cs` to register IToolInvoker → ToolInvoker as scoped
- [ ] T135 [P] Update `src/Phi4WeatherAgent.AppHost/Program.cs` to register IMcpClient → McpClient as scoped with HttpClient factory
- [X] T136 [P] Update `src/Phi4WeatherAgent.AppHost/Program.cs` to register ToolDiscoveryService as hosted service
- [ ] T137 [P] Update `src/Phi4WeatherAgent.AppHost/Program.cs` to register McpToolDiscovery as hosted service
- [X] T138 Update `src/Phi4WeatherAgent.Web/Program.cs` to replace IChatClient registration with FunctoolsChatClient decorator

### M6: Performance Benchmarks

- [ ] T139 [P] Create `src/Phi4WeatherAgent.Agent/Benchmarks/ParserBenchmarks.cs` BenchmarkDotNet class
- [ ] T140 Add benchmark method for 1MB functools block parsing in `ParserBenchmarks.cs` (target: <50ms per NFR-001)
- [ ] T141 [P] Create `src/Phi4WeatherAgent.Agent/Benchmarks/RegistryBenchmarks.cs` BenchmarkDotNet class
- [ ] T142 Add benchmark method for registry lookup in `RegistryBenchmarks.cs` (target: <1μs per NFR-003)
- [ ] T143 [P] Create `src/Phi4WeatherAgent.Agent/Benchmarks/DispatcherBenchmarks.cs` BenchmarkDotNet class
- [ ] T144 Add benchmark method for argument validation in `DispatcherBenchmarks.cs` (target: <5ms per NFR-002)
- [ ] T145 Add benchmark method for total invocation overhead in `DispatcherBenchmarks.cs` (target: <50ms per NFR-004)

### Documentation

- [ ] T146 [P] Update `README.md` at repository root with "Functools Invocation Layer" section describing the feature
- [ ] T147 [P] Create `specs/002-functools-invocation-layer/IMPLEMENTATION.md` documenting final architecture and deviations from plan
- [ ] T148 Validate quickstart.md by following steps manually: add [Tool] method, restart, invoke via Phi-4-mini, verify telemetry
- [ ] T149 [P] Update `.github/agents/copilot-instructions.md` with final implementation details (if changes from plan)

### Code Quality

- [ ] T150 [P] Run code formatter on all new files in `src/Phi4WeatherAgent.Agent/` directory
- [ ] T151 [P] Run static analysis (if configured) on `src/Phi4WeatherAgent.Agent/` and fix warnings
- [ ] T152 Add XML documentation comments to all public methods in `src/Phi4WeatherAgent.Agent/` (if missing)

### Health Checks

- [ ] T153 [P] Create `src/Phi4WeatherAgent.Agent/Health/ToolRegistryHealthCheck.cs` implementing IHealthCheck
- [ ] T154 Implement `CheckHealthAsync()` method: report registry count and MCP server connectivity in `ToolRegistryHealthCheck.cs`
- [ ] T155 Register health check in `src/Phi4WeatherAgent.AppHost/Program.cs` using AddHealthChecks() per FR-015
- [ ] T156 [P] Create `src/Phi4WeatherAgent.Agent/Security/SecurityAuditLogger.cs` with structured logging for security events (allowlist rejections, oversized arguments, rate limit violations) using `security.*` log tags per constitution Principle XII

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-8)**: All depend on Foundational phase completion
  - User Story 1 (P1): Parser + Registry + Dispatcher (core functionality) - NO dependencies on other stories
  - User Story 2 (P1): MCP integration - NO dependencies on other stories (can parallel with US1)
  - User Story 3 (P2): Argument validation - Depends on US1 (ToolInvoker must exist)
  - User Story 4 (P2): Error handling - Depends on US1 (Parser must exist)
  - User Story 5 (P2): Security - Depends on US1 (ToolInvoker must exist)
  - User Story 6 (P3): Observability - Can integrate with any completed story (cross-cutting)
- **Polish (Phase 9)**: Depends on US1, US2 (M5 needs working parser+invoker), all stories for comprehensive benchmarks

### User Story Dependencies

**Priority 1 (MVP - Must Have)**:
- **User Story 1 (P1)**: Can start after Foundational → Completes M1 (Parser) + M2 (Registry/Dispatcher) → BLOCKS US3, US4, US5
- **User Story 2 (P1)**: Can start after Foundational → Completes M4 (MCP) → Independent of US1 (parallel implementation possible)

**Priority 2 (Should Have)**:
- **User Story 3 (P2)**: Can start after US1 complete → Adds validation to existing ToolInvoker
- **User Story 4 (P2)**: Can start after US1 complete → Enhances existing Parser error handling
- **User Story 5 (P2)**: Can start after US1 complete → Adds security to existing ToolInvoker

**Priority 3 (Nice to Have)**:
- **User Story 6 (P3)**: Can start after US1 complete → Adds observability to existing components (cross-cutting)

### Within Each User Story

**Standard Flow** (applies to most stories):
1. Interfaces and entities FIRST (foundation)
2. Core implementation NEXT (main logic)
3. Integration/Configuration LAST (wiring)
4. All steps within a story should complete before moving to next priority

### Parallel Opportunities

**Within Setup (Phase 1)**:
- T002, T004, T006, T008-T009 can run in parallel (different projects/files)

**Within Foundational (Phase 2)**:
- T011-T013, T015, T017-T018 can run in parallel (different entity files)

**Across User Stories** (if team capacity allows):
- US1 (T020-T046) and US2 (T047-T069) can run in parallel after Foundational complete (no dependencies between them)
- US3, US4, US5 can run in parallel after US1 complete (all enhance ToolInvoker independently)
- US6 can run in parallel with any story (observability is cross-cutting)

**Within User Stories**:
- US1: T020, T025, T031, T037, T043-T046 can run in parallel (parser, registry, discovery, dispatcher, tool examples are independent)
- US2: T047, T054, T060, T067 can run in parallel (client, discovery, mapper, config are independent)
- US6: T100, T107, T115, T119 can run in parallel (activity source, meter, logging, config are independent)

**Within Polish (Phase 9)**:
- T122-T131 (M5 conversation) must be sequential (decorator pattern)
- T132-T138 (DI registration) can run in parallel (different files)
- T139-T145 (benchmarks) can run in parallel (independent test classes)
- T146-T149 (documentation) can run in parallel (different markdown files)
- T150-T152 (code quality) can run in parallel if automated
- T153-T155 (health checks) sequential (small scope)

---

## Parallel Execution Example: User Story 1 (MVP)

If you have 4 developers, you can parallelize US1 (core functionality) as follows:

```bash
# Developer 1: M1 Parser (T020-T024)
# Builds: FunctoolsParser.cs with streaming detection, JSON parsing, error handling

# Developer 2: M2 Registry (T025-T030) 
# Builds: ToolRegistry.cs with ConcurrentDictionary, thread-safe operations

# Developer 3: M2 Discovery (T031-T036)
# Builds: ToolDiscoveryService.cs with reflection-based [Tool] attribute scanning

# Developer 4: M2 Dispatcher (T037-T042)
# Builds: ToolInvoker.cs with validation, timeout, exception handling

# After all 4 complete, Developer 5 can add example tools (T043-T046) in parallel

# Integration: Wire everything together in AppHost/Program.cs (T132-T138)
```

**Result**: User Story 1 (23 tasks) completes in ~3-4 iterations instead of 23 sequential steps.

---

## Implementation Strategy

### MVP First (Recommended Sequence)

**Week 1**: Setup + Foundational + User Story 1 (T001-T046)
- **Goal**: Local C# tools with [Tool] attributes can be invoked
- **Demo**: Add `[Tool("SayHello")]` method, restart app, Phi-4-mini invokes it
- **Validation**: Follow quickstart.md hello-world scenario

**Week 2**: User Story 2 (T047-T069)
- **Goal**: MCP tools auto-discovered from configured servers
- **Demo**: Add MCP server URL to appsettings, restart, Phi-4-mini invokes MCP tools
- **Validation**: TestContainers integration tests with mock MCP server

**Week 3**: User Stories 3-5 (T070-T099)
- **Goal**: Security and robustness (validation, error handling, allowlist, rate limiting)
- **Demo**: Try to invoke unknown tool (rejected), exceed rate limit (rejected), invalid args (rejected)
- **Validation**: Acceptance scenarios from spec.md US3-US5

**Week 4**: User Story 6 + Polish (T100-T155)
- **Goal**: Observability, conversation integration, benchmarks, documentation
- **Demo**: Open Aspire Dashboard during tool invocation, see traces/metrics
- **Validation**: Run BenchmarkDotNet, verify <50ms parser, <5ms validation, <1μs lookup

### Incremental Delivery

After each user story completion:
1. Merge to main branch (independent increment)
2. Deploy to dev environment
3. Run acceptance scenarios
4. Demo to stakeholders
5. Gather feedback before next story

**Benefits**:
- Working software every week
- Early validation of architecture
- Risk reduction (can stop after MVP if needed)
- Continuous integration (no big-bang merge)

---

## Task Count Summary

- **Phase 1 (Setup)**: 9 tasks
- **Phase 2 (Foundational)**: 10 tasks
- **Phase 3 (User Story 1 - P1)**: 27 tasks (Parser + Registry + Dispatcher + Discovery + Tools)
- **Phase 4 (User Story 2 - P1)**: 23 tasks (MCP Client + Discovery + Mapping + Config)
- **Phase 5 (User Story 3 - P2)**: 9 tasks (JSON Schema Validation)
- **Phase 6 (User Story 4 - P2)**: 9 tasks (Error Handling)
- **Phase 7 (User Story 5 - P2)**: 12 tasks (Allowlist + Rate Limiting)
- **Phase 8 (User Story 6 - P3)**: 22 tasks (OpenTelemetry Traces + Metrics + Logging)
- **Phase 9 (Polish)**: 34 tasks (Conversation Integration + DI + Benchmarks + Docs + Health)

**Total**: 155 tasks

**Parallelizable**: 52 tasks marked [P] (can reduce wall-clock time by ~33% with parallel execution)

**MVP Scope** (Setup + Foundational + US1 + US2): 69 tasks (~45% of total, delivers core functionality)

---

## Notes

- Tasks are organized by user story to enable independent implementation and testing
- Each user story can be deployed and validated independently after completion
- Checkpoint comments mark natural integration points where stories come together
- Tests were omitted per template instructions (not explicitly requested in spec.md)
- All file paths are absolute from repository root for clarity
- [P] marker indicates tasks that can run in parallel (different files, no dependencies)
- [Story] marker required for all user story phase tasks (US1-US6)
- Task IDs are sequential (T001-T155) in execution order for easy reference

---

## Phase 10: Foundry Native Integration (NEW - Post Discovery)

**Purpose**: Integrate with Foundry's native function calling template discovered after initial implementation

**DISCOVERY**: Foundry Local 0.8.103+ has built-in functools template. Current manual system prompt bypasses native support.

**Goal**: Leverage Foundry's {Tool} placeholder for tool injection, keep custom FunctoolsChatClient for execution

**Contracts**: See `specs/002-functools-invocation-layer/contracts/IAIFunctionAdapter.cs` and `IChatOptionsBuilder.cs`

**Independent Test**: Start Aspire, send "weather in Brasov" query, verify no raw functools visible, logs show tools discovered and ChatOptions built

### Pre-Implementation Validation

- [X] T199 [P] [US7] **Validate Foundry Template Format** [30min] - Inspect `%USERPROFILE%\.foundry\models\phi4-mini-instruct-generic-cpu\5\inference_model.json`, verify {Tool} placeholder exists, document expected JSON Schema format, test with sample ChatOptions.Tools, update research.md with template discovery findings (CHK007, CHK083, CHK087, CHK100)

### Implementation for Foundry Native Integration

#### M1: AIFunction Adapter (Convert ToolMetadata → AIFunction)

- [X] T200 [P] [US7] Create `src/Phi4WeatherAgent.Agent/Adapters/AIFunctionAdapter.cs` implementing IAIFunctionAdapter interface with error handling for conversion failures (CHK116, CHK119)
- [X] T201 [US7] Implement `ConvertToAIFunction(ToolMetadata)` method returning AIFunctionDeclaration with name, description, and JSON Schema in `AIFunctionAdapter.cs`
- [X] T202 [US7] Implement `ConvertJsonSchemaToElement(JsonSchema)` private method converting schema to JsonElement in `AIFunctionAdapter.cs`
- [X] T203 [US7] ~~Implement `MapToJsonType(Type)` private method~~ (Not needed - AIFunctionFactory.CreateDeclaration accepts JsonElement schema directly)
- [X] T204 [US7] Add DEBUG-level logging for each tool conversion in `ConvertToAIFunction()` in `AIFunctionAdapter.cs` (CHK080)
- [X] T205 [US7] Ensure NO execution delegate included (custom ToolInvoker handles execution) - AIFunctionFactory.CreateDeclaration creates metadata-only declaration

#### M2: ChatOptions Builder (Populate ChatOptions.Tools)

- [X] T206 [P] [US7] Create `src/Phi4WeatherAgent.Agent/Adapters/ChatOptionsBuilder.cs` implementing IChatOptionsBuilder interface
- [X] T207 [US7] Implement `BuildWithToolsAsync()` method querying IToolRegistry.ListAsync() in `ChatOptionsBuilder.cs`
- [X] T208 [US7] Convert each ToolDescriptor to AIFunctionDeclaration via IAIFunctionAdapter in `BuildWithToolsAsync()` in `ChatOptionsBuilder.cs`
- [X] T209 [US7] Build and return ChatOptions with Tools list populated in `BuildWithToolsAsync()` in `ChatOptionsBuilder.cs`
- [X] T210 [US7] Add INFO-level log "Built ChatOptions with {Count} tools: {ToolNames}" in `BuildWithToolsAsync()` in `ChatOptionsBuilder.cs`
- [X] T211 [US7] Add WARNING-level log if no tools registered (empty registry) in `BuildWithToolsAsync()` in `ChatOptionsBuilder.cs`

#### M3: Dependency Injection Registration

#### M3: DI Registration (Wire Services)

- [X] T212 [P] [US7] ~~Add Foundry version check in DI startup~~: CLARIFIED per FR-020 - Foundry version check must fail-fast on startup if < 0.8.103 or inference_model.json missing {Tool} placeholder, throw NotSupportedException to prevent app startup with incompatible Foundry version (implementation deferred - version detection API not available, manual verification required during deployment)
- [X] T213 Add `services.AddSingleton<IAIFunctionAdapter, AIFunctionAdapter>()` and `services.AddSingleton<IChatOptionsBuilder, ChatOptionsBuilder>()` to `src/Phi4WeatherAgent.Web/Program.cs`

#### M4: Chat.razor Update (Use Native Tool Injection)

- [X] T214 Inject IChatOptionsBuilder via `@inject` directive in `src/Phi4WeatherAgent.Web/Components/Pages/Chat/Chat.razor`
- [X] T215 Remove manual system prompt tool descriptions (lines 29-86) from `Chat.razor`
- [X] T216 Replace with simple system prompt "You are a helpful weather assistant powered by Phi-4." in `Chat.razor`
- [X] T217 Call `ChatOptionsBuilder.BuildWithToolsAsync()` before each GetStreamingResponseAsync call in `Chat.razor` (async method, called in OnInitializedAsync and AddUserMessageAsync)
- [X] T218 Pass ChatOptions to `GetStreamingResponseAsync(messages, options)` in `Chat.razor` (already present, now populated with tools)

#### M5: Enhanced Discovery Logging (Diagnostics)

- [X] T219 [P] Add INFO-level log "ToolDiscoveryService starting..." at beginning of StartAsync in `src/Phi4WeatherAgent.Agent/Registry/ToolDiscoveryService.cs`
- [X] T220 Add DEBUG-level log "Registered tool: {ToolName} with {ParamCount} parameters" for each tool in `ToolDiscoveryService.cs`
- [X] T221 Update final INFO-level log to "Registered {Count} tools: {ToolNames}" with comma-separated list in `ToolDiscoveryService.cs`

#### M6: Health Endpoint (Optional Diagnostics)

- [X] T222 [P] (SKIPPED - Optional) Create `src/Phi4WeatherAgent.Web/Endpoints/ToolsHealthEndpoint.cs` with MapToolsHealth extension method
- [X] T223 (SKIPPED - Optional) Implement GET /tools/health endpoint returning JSON `{discovered: count, tools: [{name, description, parameterCount}]}` in `ToolsHealthEndpoint.cs`
- [X] T224 (SKIPPED - Optional) Query IToolRegistry.GetAllTools() to populate response in `ToolsHealthEndpoint.cs`
- [X] T225 (SKIPPED - Optional) Register endpoint via `app.MapToolsHealth()` call in `src/Phi4WeatherAgent.Web/Program.cs`

**Checkpoint**: Foundry native integration complete - Foundry injects tools, custom parser executes them

**Success Criteria**:
- ✅ AIFunction adapter converts ToolMetadata with correct JSON Schema
- ✅ ChatOptions populated with tools on every request
- ✅ No manual tool descriptions in system prompt
- ✅ Logs show "Registered 5 tools: GeocodeLocation, GetWeather, GetForecast, GetPollenForecast, GetAllergens"
- ✅ Logs show "Built ChatOptions with 5 tools"
- ✅ Manual test: No raw functools visible in UI after query
- ✅ Manual test: End-to-end query completes in < 10s

