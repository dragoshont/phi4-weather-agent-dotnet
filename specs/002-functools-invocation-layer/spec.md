# Feature Specification: Phi-4-mini Functools Invocation Layer

**Feature Branch**: `002-functools-invocation-layer`  
**Created**: 2025-11-16  
**Status**: Draft  
**Input**: User description: "Build a robust invocation layer to parse Phi-4-mini's functools format and execute MCP/local tools with zero-code extensibility"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Parse Functools and Invoke Local C# Tools (Priority: P1)

Developer adds a new C# tool method decorated with `[Tool("ToolName")]`, and Phi-4-mini can immediately invoke it without modifying parser or dispatcher code.

**Why this priority**: Core capability - the invocation layer must parse functools and execute tools. Without this, the entire system is non-functional.

**Independent Test**: Deploy a simple console app with one `[Tool]` method. Send a prompt to Phi-4-mini requesting tool use. Verify functools block is parsed and tool is invoked successfully with correct arguments.

**Acceptance Scenarios**:

1. **Given** Phi-4-mini responds with `functools[{"name": "GetWeather", "arguments": {"location": "Seattle"}}]`, **When** parser extracts FunctionCall and dispatcher looks up tool in registry, **Then** tool executes and returns `ToolResult` with weather data
2. **Given** developer adds `[Tool("CalculateSum")] int Sum(int a, int b)` to codebase and restarts app, **When** Phi-4-mini calls `functools[{"name": "CalculateSum", "arguments": {"a": 5, "b": 3}}]`, **Then** tool executes without code changes to parser/dispatcher and returns `ToolResult { Content = "8" }`
3. **Given** tool method throws exception during execution, **When** dispatcher catches exception, **Then** returns `ToolResult { Error = "sanitized error message" }` without leaking stack traces

---

### User Story 2 - Discover and Invoke MCP Tools Dynamically (Priority: P1)

Developer configures an MCP server URL in `appsettings.tools.json`, and tools from that server are automatically discovered at startup and invokable by Phi-4-mini.

**Why this priority**: MCP integration is a constitutional requirement (Principle V). Zero-code extensibility for MCP tools is a success criterion.

**Independent Test**: Configure MCP server with `GetCoordinates` tool in appsettings. Start app. Send prompt requesting geocoding. Verify MCP tool is discovered, registered, and invoked successfully.

**Acceptance Scenarios**:

1. **Given** `appsettings.tools.json` contains `{"mcpServers": [{"url": "http://localhost:3000/mcp"}]}`, **When** app starts, **Then** MCP adapter calls `ListTools` and registers all discovered tools in registry
2. **Given** MCP server exposes `GetWeather(location: string)` tool, **When** Phi-4-mini calls `functools[{"name": "GetWeather", "arguments": {"location": "Berlin"}}]`, **Then** MCP adapter translates call to MCP protocol, sends HTTP request, and returns `ToolResult`
3. **Given** MCP server is unreachable during discovery, **When** app starts, **Then** logs warning and continues startup (graceful degradation, MCP tools unavailable)

---

### User Story 3 - Validate Tool Arguments with JSON Schema (Priority: P2)

System validates all tool arguments against declared JSON Schema before invocation, rejecting malformed or oversized inputs.

**Why this priority**: Security constraint (Principle XII) - prevents injection attacks and resource exhaustion. Critical for production deployment.

**Independent Test**: Register tool with schema requiring `location: string (max 100 chars)`. Call with 10MB payload. Verify dispatcher rejects with `ARG_VALIDATION_FAILED` error before invoking tool.

**Acceptance Scenarios**:

1. **Given** tool declares `JsonSchema { "location": { "type": "string", "maxLength": 100 } }`, **When** functools provides `{"location": "A" * 10000}`, **Then** dispatcher rejects with `ToolResult { Error = "ARG_VALIDATION_FAILED: location exceeds maxLength" }`
2. **Given** tool requires `{"temperature": {"type": "number"}}`, **When** functools provides `{"temperature": "hot"}`, **Then** dispatcher rejects with `ToolResult { Error = "ARG_VALIDATION_FAILED: temperature must be number" }`
3. **Given** tool has no schema declared, **When** functools provides any arguments, **Then** dispatcher skips validation and invokes tool (opt-in validation)

---

### User Story 4 - Handle Malformed Functools Gracefully (Priority: P2)

Parser detects malformed functools blocks (invalid JSON, missing fields) and returns error message to model for retry without crashing.

**Why this priority**: Robustness - models occasionally produce malformed output. System must degrade gracefully.

**Independent Test**: Send model response with `functools[{"name": "GetWeather", "arguments": ]` (broken JSON). Verify parser returns `MALFORMED_BLOCK` error, dispatcher does not crash, and error is formatted as tool message for re-prompting.

**Acceptance Scenarios**:

1. **Given** model outputs `functools[invalid json here]`, **When** parser attempts deserialization, **Then** catches exception and returns `ToolResult { Error = "MALFORMED_BLOCK: Invalid JSON in functools block" }`
2. **Given** model outputs `functools[{"name": "GetWeather"}]` (missing `arguments`), **When** parser validates structure, **Then** returns `ToolResult { Error = "MALFORMED_BLOCK: Missing required field 'arguments'" }`
3. **Given** model outputs partial functools block in streaming response, **When** parser accumulates chunks, **Then** waits for complete block before attempting parse (no false positives mid-stream)

---

### User Story 5 - Enforce Tool Allowlist and Rate Limiting (Priority: P2)

System rejects unknown tool names and limits tool calls per conversation turn to prevent abuse.

**Why this priority**: Security constraint (Principle XII) - prevents arbitrary code execution and DoS attacks.

**Independent Test**: Configure allowlist with 2 tools. Call unknown tool. Verify rejection. Make 11 tool calls in one turn. Verify 11th call is rejected with rate limit error.

**Acceptance Scenarios**:

1. **Given** allowlist contains `["GetWeather", "GetCoordinates"]`, **When** functools calls `DeleteAllData`, **Then** dispatcher rejects with `ToolResult { Error = "UNKNOWN_TOOL: DeleteAllData not in allowlist" }` and logs attempt
2. **Given** rate limit is 10 calls/turn, **When** conversation exceeds 10 tool calls, **Then** dispatcher rejects 11th call with `ToolResult { Error = "RATE_LIMIT_EXCEEDED: Max 10 tool calls per turn" }`
3. **Given** environment-specific allowlist (dev vs prod), **When** app loads in production, **Then** only production-approved tools are registered (Aspire parameter binding)

---

### User Story 6 - Emit OpenTelemetry Traces and Metrics (Priority: P3)

Every tool invocation emits structured logs, metrics (P50/P95/P99 latency), and distributed traces visible in Aspire Dashboard.

**Why this priority**: Observability constraint (Principle XII) - production debugging and performance monitoring.

**Independent Test**: Invoke tool via conversation. Open Aspire Dashboard. Verify trace spans (parse → validate → dispatch → execute) appear with correct timing and metadata.

**Acceptance Scenarios**:

1. **Given** tool invocation completes successfully, **When** telemetry is exported, **Then** OpenTelemetry trace includes spans: `functools.parse`, `tool.validate`, `tool.dispatch`, `tool.execute` with parent-child relationships
2. **Given** 100 tool calls complete, **When** metrics are aggregated, **Then** `tool.duration` histogram reports P50/P95/P99 latencies per tool name
3. **Given** tool invocation fails with `TIMEOUT`, **When** error is logged, **Then** structured log includes `{ "level": "error", "error_type": "TIMEOUT", "tool_name": "SlowTool", "correlation_id": "..." }`

---

### Edge Cases

- What happens when **functools block spans multiple streaming chunks** (partial JSON across responses)?
  - Parser must buffer chunks and only attempt parse when block is complete (detect closing `]`)
- What happens when **tool execution exceeds timeout** (default 30s)?
  - Dispatcher cancels task via `CancellationToken` and returns `ToolResult { Error = "TIMEOUT: Tool exceeded 30s limit" }`
- What happens when **MCP server returns 500 error** during tool execution?
  - MCP adapter applies Polly retry policy (exponential backoff), then returns `ToolResult { Error = "INVOCATION_FAILED: MCP server error after retries" }`
- What happens when **multiple tools registered with same name**?
  - Registry rejects duplicate on registration with exception, logs error, and preserves first registration (fail-fast during startup)
- What happens when **tool returns >10MB response**?
  - Dispatcher truncates response to 10MB and logs warning: `ToolResult { Content = "[TRUNCATED]...", Meta = {"truncated": "true", "original_size": "15MB"} }`

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST parse `functools[...]` blocks from Phi-4-mini responses (streaming or complete) and extract array of `FunctionCall { Name, Arguments }`
- **FR-002**: System MUST validate JSON structure of functools blocks before dispatch (fail-fast on malformed JSON)
- **FR-003**: System MUST register local C# tools decorated with `[Tool("ToolName")]` attribute via assembly scanning at startup
- **FR-004**: System MUST discover MCP tools dynamically from configured MCP servers (HTTP endpoints) and register as `ToolDescriptor`
- **FR-005**: System MUST validate tool arguments against declared JSON Schema before invocation (opt-in per tool)
- **FR-006**: System MUST enforce explicit allowlist of tool names (reject unknown tools by default)
- **FR-007**: System MUST rate-limit tool calls to max 10 per conversation turn (configurable via appsettings)
- **FR-008**: System MUST invoke tools with timeout (default 30s, configurable per tool via `ToolDescriptor`)
- **FR-009**: System MUST catch tool exceptions and format as `ToolResult { Error }` without leaking stack traces
- **FR-010**: System MUST append tool results as `{"role": "tool", "content": "...", "tool_call_id": "..."}` messages for re-prompting
- **FR-011**: System MUST emit OpenTelemetry traces with spans: parse → validate → dispatch → execute
- **FR-012**: System MUST emit metrics: `tool.duration` (P50/P95/P99), `tool.errors` (count per tool), `registry.lookup.miss` (unknown tool attempts)
- **FR-013**: System MUST log structured JSON with correlation IDs for all tool invocations (success, failure, timeout)
- **FR-014**: System MUST load tool configuration from `appsettings.tools.json` and Aspire parameters (environment-specific allowlists, MCP server URLs)
- **FR-015**: System MUST provide health check endpoint reporting registry status (number of registered tools, MCP server connectivity)
- **FR-016**: System MUST support Source Generator for compile-time tool discovery in AOT scenarios (optional, no runtime reflection)

### Non-Functional Requirements

- **NFR-001**: Parser MUST process 1MB functools block in <50ms (P95 latency)
- **NFR-002**: Dispatcher validation MUST complete in <5ms per tool call (P95 latency, excludes actual tool execution)
- **NFR-003**: Dispatcher P95 latency MUST be <30ms excluding external tool invocation time
- **NFR-004**: Registry lookup MUST complete in <1μs per tool name (concurrent dictionary access)
- **NFR-005**: Total invocation overhead MUST be <50ms (parse + validate + dispatch, excludes tool execution and MCP HTTP calls)
- **NFR-006**: System MUST handle 100 concurrent tool invocations without degradation (thread-safe registry)
- **NFR-007**: System MUST start up in <5 seconds with 50 registered tools (assembly scanning + MCP discovery)
- **NFR-008**: System MUST gracefully degrade if MCP servers are unreachable (log warning, continue with local tools)
- **NFR-009**: System MUST maintain zero memory leaks during 1000 tool invocations (dispose resources properly)
- **NFR-010**: System MUST be compatible with .NET 10 Native AOT (optional Source Generator path, no runtime reflection)

### Key Entities

- **FunctionCall**: Represents a parsed tool invocation from functools block
  - `Name` (string): Tool name exactly as Phi-4-mini outputs it
  - `Arguments` (JsonElement): Unvalidated JSON arguments from model
  
- **ToolDescriptor**: Registry entry for a discovered/registered tool
  - `Name` (string): Canonical tool name (case-insensitive lookup)
  - `Source` (string): Origin of tool ("Local", "MCP:<server-url>", "Generated")
  - `ArgsSchema` (JsonSchema?): Optional JSON Schema for argument validation
  - `Invoker` (Func<JsonElement, ValueTask<ToolResult>>): Async function to execute tool
  - `SecurityClass` (enum): Classification (Public, Internal, Admin) for allowlist filtering
  - `Timeout` (TimeSpan): Per-tool timeout override (defaults to 30s)
  
- **ToolResult**: Output of tool invocation returned to model
  - `Name` (string): Tool name (matches FunctionCall.Name)
  - `Content` (string): Successful result content (may be JSON, text, etc.)
  - `Error` (string?): Error message if invocation failed (MALFORMED_BLOCK, UNKNOWN_TOOL, ARG_VALIDATION_FAILED, INVOCATION_FAILED, TIMEOUT, CANCELLED)
  - `Duration` (TimeSpan?): Actual tool execution time (for telemetry)
  - `Meta` (IDictionary<string, string>?): Additional metadata (truncation flags, retry counts, etc.)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developer can add new C# tool with `[Tool]` attribute and Phi-4-mini invokes it without modifying parser/dispatcher (zero-code extensibility verified via manual test)
- **SC-002**: Developer can configure new MCP server URL in appsettings and tools are discovered automatically without recompilation (zero-code MCP extensibility verified via manual test)
- **SC-003**: All acceptance tests pass (18 scenarios across 6 user stories) with 100% success rate
- **SC-004**: Parser processes 1MB functools block in <50ms (verified via BenchmarkDotNet)
- **SC-005**: Dispatcher validation completes in <5ms per tool call (verified via BenchmarkDotNet)
- **SC-006**: Total invocation overhead <50ms excluding tool execution (verified via BenchmarkDotNet)
- **SC-007**: Registry lookup <1μs per tool name (verified via BenchmarkDotNet)
- **SC-008**: Zero security violations in acceptance tests (unknown tool rejection, allowlist enforcement, argument validation all pass)
- **SC-009**: OpenTelemetry traces appear in Aspire Dashboard with correct spans and timing (verified via manual inspection during E2E tests)
- **SC-010**: Structured logs include correlation IDs and error types for all tool invocations (verified via log aggregation query)
- **SC-011**: System handles 100 concurrent tool invocations without race conditions or deadlocks (verified via stress test with `Parallel.ForEach`)
- **SC-012**: System startup completes in <5 seconds with 50 registered tools (verified via stopwatch measurement)

## Assumptions *(if any)*

- **A-001**: Phi-4-mini consistently outputs functools in format `functools[array of objects]` (based on observed behavior from testing)
- **A-002**: MCP servers implement standard MCP protocol for `ListTools` and tool invocation (per MCP specification)
- **A-003**: Tool methods are thread-safe or explicitly documented as requiring synchronization (responsibility of tool author)
- **A-004**: JSON Schema validation library (e.g., `JsonSchema.Net`) is available and performant (<5ms validation time)
- **A-005**: Aspire Dashboard is running and accessible for telemetry visualization during development
- **A-006**: .NET 10 Native AOT compilation is optional - Source Generator path only needed if user requests AOT support

## Dependencies *(if any)*

- **D-001**: Microsoft.Extensions.AI (IChatClient abstraction for Phi-4-mini integration)
- **D-002**: Aspire 13 hosting and telemetry infrastructure
- **D-003**: OpenTelemetry SDK for traces and metrics
- **D-004**: JSON Schema validation library (e.g., `JsonSchema.Net` or equivalent)
- **D-005**: Polly 8.5+ for MCP adapter retry policies
- **D-006**: xUnit + FluentAssertions for unit/integration tests
- **D-007**: BenchmarkDotNet for performance validation
- **D-008**: TestContainers for MCP server integration tests (mock HTTP servers)
- **D-009**: MCP protocol specification (for MCP adapter implementation)

## Out of Scope

- ❌ Switching to different models (Phi-4-mini is the target, other models may have different formats)
- ❌ Reusing `.UseFunctionInvocation()` extension (incompatible with functools format)
- ❌ Supporting OpenAI-standard `tool_calls` format (Phi-4-mini doesn't emit this)
- ❌ Modifying Phi-4-mini's output format (parse what model produces, don't try to change model)
- ❌ Implementing UI for tool management (config via appsettings.json is sufficient for v1)
- ❌ Tool versioning or A/B testing (single version per tool name in v1)
- ❌ Distributed tool execution across multiple nodes (single-node execution for v1)
- ❌ Tool rollback or undo capabilities (tools execute once, no transaction support)

## Risks & Mitigations

- **R-001**: Phi-4-mini changes functools format in future model updates
  - **Mitigation**: Parser is isolated in `IFunctoolsParser` interface - can swap implementations without touching dispatcher
  - **Detection**: Integration tests with real Phi-4-mini will fail if format changes
  
- **R-002**: MCP servers become unresponsive during production usage
  - **Mitigation**: Polly retry policies + circuit breaker (fail-open after 3 consecutive failures)
  - **Monitoring**: `mcp.server.health` metric tracks connectivity, alerts on failures
  
- **R-003**: Tool execution consumes excessive memory (e.g., tool returns 1GB response)
  - **Mitigation**: Dispatcher enforces 10MB max response size (truncate + log warning)
  - **Monitoring**: `tool.response.size` metric tracks response sizes, alerts on >5MB
  
- **R-004**: Malicious tool arguments attempt injection attacks
  - **Mitigation**: JSON Schema validation rejects oversized/malformed inputs before dispatch
  - **Audit**: All rejected calls logged with `security.validation_failure` tag for SOC review
  
- **R-005**: Performance degrades with >100 registered tools (registry lookup overhead)
  - **Mitigation**: Use `ConcurrentDictionary` with O(1) lookup, Source Generator for AOT scenarios
  - **Monitoring**: `registry.lookup.duration` metric tracks lookup times, benchmark validates <1μs target

## Acceptance Criteria Checklist

- [ ] All 18 acceptance scenarios pass (6 user stories × ~3 scenarios each)
- [ ] Parser benchmark: 1MB block in <50ms
- [ ] Dispatcher benchmark: validation in <5ms per call
- [ ] Total overhead benchmark: <50ms (parse + validate + dispatch)
- [ ] Registry benchmark: lookup in <1μs
- [ ] Security tests: unknown tool rejection, allowlist enforcement, argument validation all pass
- [ ] Observability: OpenTelemetry traces visible in Aspire Dashboard
- [ ] Observability: Structured logs include correlation IDs
- [ ] Stress test: 100 concurrent invocations without errors
- [ ] Startup test: <5 seconds with 50 tools
- [ ] Zero-code extensibility: Add `[Tool]` method → works without code changes
- [ ] Zero-code MCP: Add MCP server URL → tools discovered automatically
- [ ] Constitution compliance: All 12 principles verified (especially XII Custom Invocation Layer)
