<!--
Sync Impact Report:
- Version: 1.0.0 → 1.2.0 (MINOR bump: expanded principle XII with project scope)
- Principles Modified:
  • III. Agent Framework Only → Exception added for custom Phi-4 functools parser
  • V. Model Context Protocol → Expanded to include invocation layer integration
  • XII. Custom Invocation Layer → Expanded with project scope, non-goals, success criteria
- Principles Added:
  • XII. Custom Invocation Layer → New principle for Phi-4-mini functools parsing (v1.1.0)
  • Project-specific guidance added: correctness-first, schema-validated, secure-by-default
- Principles Renamed: None
- Sections Removed: None
- Templates Status:
  ✅ plan-template.md - Compatible (reviewed v1.1.0)
  ✅ spec-template.md - Compatible (reviewed v1.1.0)
  ✅ tasks-template.md - Compatible (reviewed v1.1.0)
  ✅ README.md - Updated (Phi-4 references restored)
  ✅ Setup scripts - Updated (Phi-4 model download restored)
- Completed Actions:
  ✅ README.md reverted from Mistral to Phi-4-mini
  ✅ Setup-Environment.ps1 model download updated to phi-4-mini
  ✅ Start-AspireHost.ps1 header updated to Phi-4
  ✅ Program.cs model ID reverted to Phi-4-mini-instruct-generic-cpu:5
  ✅ Mistral-7B tested: confirmed NO function calling support
- Follow-up TODOs:
  • Create detailed architecture docs (parser, dispatcher, registry, MCP adapter)
  • Implement acceptance tests for invocation layer
  • Add performance benchmarks (parser <10ms, dispatcher <5ms, total <50ms)
  • Document MCP server integration patterns
-->

# Phi-4 Weather Assistant Constitution

## Core Principles

### I. Local-First AI
**All AI inference MUST run locally** on developer hardware. Cloud AI services are forbidden.

**Platform-Specific Model Hosting:**
- **Windows**: Foundry Local (via `aspire-ai` workload)
- **macOS**: Foundry Local (via `aspire-ai` workload)
- **Linux**: Ollama (manual installation documented in setup scripts)

**Model**: Microsoft Phi-4-mini-instruct (3.8B parameters, generic-cpu ONNX quantization)

**Rationale**: Ensures zero runtime costs, complete data privacy, and offline capability. Developers maintain full control over inference without external dependencies. Phi-4-mini provides optimal balance of quality and CPU performance.

### II. .NET 10 Requirement (NON-NEGOTIABLE)
**.NET 10 SDK is MANDATORY** for this project. No exceptions.

- SDK Version: `10.0.100` or later (pinned in `global.json`)
- Target Framework: `net10.0` in all projects
- Rollforward policy: `latestFeature` with preview support enabled
- **Forbidden**: .NET 9 or earlier in production code

**Rationale**: .NET 10 provides native Agent Framework support, latest C# language features, and Aspire 13 compatibility. Earlier versions lack required abstractions.

### III. Agent Framework Only
**Use Microsoft.Extensions.AI Agent Framework exclusively.** Semantic Kernel is forbidden.

**Permitted Packages:**
- `Microsoft.Extensions.AI` (version 10.0.0-preview.1.25071.7+)
- `Microsoft.Extensions.AI.Abstractions`
- `Microsoft.Extensions.AI.Ollama`

**Forbidden Packages:**
- `Microsoft.SemanticKernel`
- `Microsoft.SemanticKernel.Agents`
- Any Semantic Kernel extensions

**Exception for Phi-4-mini**:
Phi-4-mini outputs function calls in custom `functools[...]` text format instead of OpenAI's structured `tool_calls`. A **custom invocation layer** is permitted to:
1. Parse `functools[{"name": "...", "arguments": {...}}]` from model responses
2. Dispatch to registered MCP tools and local C# methods
3. Format tool results as `tool` role messages for re-prompting

This layer MUST integrate with Agent Framework's IChatClient abstraction and maintain observability through structured logging.

**Rationale**: Agent Framework is .NET 10's native AI abstraction. Mixing it with Semantic Kernel creates architectural confusion, duplicated patterns, and maintenance burden. Custom parsing is unavoidable for Phi-4-mini but must remain minimal and well-tested.

### IV. Aspire 13 Orchestration
**Use .NET Aspire 13 preview** for orchestration, service discovery, and observability.

- Aspire Version: `13.0.0-preview.1` or later
- Required Workloads: `aspire` (all platforms), `aspire-ai` (Windows/macOS only)
- Aspire Dashboard: Auto-launch in development for telemetry visualization
- AppHost project: Platform detection for Foundry Local vs Ollama configuration

**Rationale**: Aspire 13 provides unified orchestration for local AI models, HTTP services, and observability without container overhead.

### V. Model Context Protocol (MCP)
**Weather data retrieval MUST use MCP tools** for structured, agent-friendly data.

**Required MCP Tools:**
- **Geocoding Tool**: Convert location names to coordinates
- **Weather Forecast Tool**: Retrieve forecast data from OpenMeteo API
- **Allergen Data Tool**: Retrieve pollen/allergen information

**HTTP Clients:** OpenMeteo APIs (free, no API keys required)

**Retry Policies:** Polly 8.5+ for transient fault handling

**Invocation Integration**:
- MCP tools MUST be registered in the Tool Registry (see Principle XII)
- Tool schemas (name, description, parameters) drive prompt engineering
- Tool results MUST be formatted as `tool` role messages for conversation continuity
- MCP tool discovery MUST be dynamic (adding MCP servers requires no code changes)

**Rationale**: MCP tools provide type-safe, testable abstractions over raw HTTP calls. The invocation layer dispatches Phi-4-mini's functools calls to MCP tools transparently, maintaining separation of concerns.

### VI. Zero Cloud Runtime Costs
**No paid services or API keys** permitted in production runtime.

**Permitted:**
- Free APIs (OpenMeteo weather/geocoding/allergen services)
- Local model hosting (Foundry Local, Ollama)
- Open-source dependencies (MIT/Apache 2.0 licensed)

**Forbidden:**
- Azure OpenAI Service
- OpenAI API (paid tiers)
- Paid weather APIs (WeatherAPI, AccuWeather, etc.)

**Exception:** Development/CI infrastructure may use cloud resources (GitHub Actions, Azure Pipelines) for build/test automation.

**Rationale**: Guarantees zero recurring costs for end users. Application remains functional offline without subscriptions.

### VII. WCAG 2.1 AA Accessibility
**UI MUST meet WCAG 2.1 Level AA** compliance for inclusive user experience.

**Requirements:**
- Keyboard navigation for all interactive features
- Screen reader compatibility (ARIA labels, semantic HTML)
- Color contrast ratios ≥4.5:1 for normal text, ≥3:1 for large text
- Focus indicators visible for all interactive elements
- Form validation errors announced to screen readers

**Testing:** axe DevTools automated scans + manual validation with NVDA/JAWS screen readers

**Rationale**: Weather information is critical for health/safety decisions. Application must be accessible to users with visual, motor, or cognitive disabilities.

### VIII. Template-Based Architecture
**Leverage Microsoft aichatweb template** to avoid reinventing chat UI.

**Base Template:** `dotnet new aichatweb --provider ollama --vector-store local`

**Template Customizations:**
- ✅ **Keep**: ChatInput.razor, ChatMessageList.razor, ChatMessageItem.razor, ChatHeader.razor, ChatSuggestions.razor, IChatClient integration
- ❌ **Remove**: Vector store (JsonVectorStore/Qdrant), document ingestion (Services/Ingestion/), semantic search (SemanticSearch.cs), PDF viewer libraries, markdown viewer
- ✨ **Customize**: ChatMessageItem.razor for weather card visualizations, system prompt in Chat.razor, IChatClient provider configuration for Foundry Local/Ollama
- ➕ **Add**: AgentService for MCP tool orchestration, weather card Blazor components, OpenMeteo HTTP clients with Polly retry

**Rationale**: Microsoft's template provides production-quality chat UI, SignalR real-time messaging, and IChatClient patterns. Customizing is faster than building from scratch.

### IX. Comprehensive Testing Coverage
**Maintain testing across unit, integration, and E2E layers** for confidence in changes.

**Test Types:**
- **Unit Tests**: xUnit for business logic (Agent, MCP tools, HTTP clients)
- **Component Tests**: bUnit for Blazor UI components (ChatMessageItem, weather cards)
- **Integration Tests**: Test Agent Framework + MCP tool orchestration
- **E2E Tests**: Playwright for full user workflows (location search → weather display)
- **Benchmarks**: BenchmarkDotNet for Agent Framework initialization performance

**Coverage Target:** >80% for critical paths (weather query processing, MCP tool execution, error handling)

**Test-First Workflow:** Write failing tests before implementation for new features (TDD encouraged but not enforced)

**Rationale**: Local AI introduces non-determinism. Comprehensive tests ensure regressions are caught early, especially for model prompt changes.

### X. Cross-Platform Development
**Support Windows, macOS, and Linux** development environments equally.

**Platform Setup Scripts:**
- **Windows**: PowerShell script (`scripts/setup-windows.ps1`) for .NET 10 + Aspire + Foundry Local
- **macOS**: Bash script (`scripts/setup-macos.sh`) for .NET 10 + Aspire + Foundry Local
- **Linux**: Bash script (`scripts/setup-linux.sh`) for .NET 10 + Aspire + manual Ollama instructions

**CI Matrix:** GitHub Actions must test on `windows-latest`, `macos-latest`, `ubuntu-latest`

**Documentation:** README.md includes platform-specific setup instructions with troubleshooting

**Rationale**: .NET is cross-platform. Developers should be able to contribute from any OS without friction.

### XI. MIT License
**All project code is MIT-licensed.** Dependencies MUST use permissive licenses.

**Permitted Licenses:**
- MIT
- Apache 2.0
- BSD (2-clause, 3-clause)

**Forbidden Licenses:**
- GPL (any version)
- AGPL
- Proprietary/commercial licenses requiring fees

**Enforcement:** CI pipeline scans dependencies for license compatibility (`dotnet list package --include-transitive` + license check tool)

**Rationale**: Maximizes reusability and commercialization options for forks. Avoids viral copyleft obligations.

### XII. Custom Invocation Layer (Phi-4-mini)

**Build a robust invocation layer** to parse Phi-4-mini's `functools[...]` format and execute MCP/local tools.

**Project Scope:**
- **Purpose**: Reliable parsing and execution of `functools[...]` tool calls with MCP extensibility
- **Core Principles**: Correctness-first, schema-validated, secure-by-default, minimal coupling, horizontal scalability, great telemetry
- **Deliverables**: Parser, Dispatcher, Tool Registry, MCP adapter, conversation glue, tests, docs, sample host

**Non-Goals (Explicitly Out of Scope):**
- ❌ Switching models (Phi-4-mini is the target model)
- ❌ Reusing `.UseFunctionInvocation()` for Phi-4-mini (incompatible with functools format)
- ❌ Supporting OpenAI-standard tool_calls (Phi-4-mini doesn't emit them)
- ❌ Modifying Phi-4-mini's output format (parse what the model produces)

**Success Criteria:**
- ✅ Add new tools/MCP servers without code changes to parser or dispatcher
- ✅ Pass all acceptance tests (correctness, error handling, timeout scenarios)
- ✅ Meet performance targets (parser <10ms, dispatcher <5ms, total overhead <50ms)
- ✅ Zero security violations (explicit whitelist, schema validation, rate limiting)
- ✅ Production-ready observability (structured logs, metrics, distributed tracing)

**Architecture Components:**

**1. Parser (Correctness-First)**
- Detects `functools[...]` blocks in streaming or complete model responses
- Deserializes JSON array: `[{"name": "ToolName", "arguments": {...}}]`
- Handles malformed JSON gracefully (log error, return "parsing failed" message to model)
- MUST support partial parsing for streaming responses
- **Schema-Validated**: JSON structure must match expected functools format before dispatch
- **Minimal Coupling**: Parser has no dependencies on Dispatcher or Tool Registry

**2. Dispatcher (Secure-By-Default)**
- Validates tool name exists in Tool Registry (reject unknown tools)
- **Explicit Whitelist**: Only registered tools are invokable (no reflection-based discovery)
- Validates arguments match tool's parameter schema (JSON Schema validation)
- Invokes tool handler (local C# method or MCP tool proxy)
- Handles timeouts (default 30s per tool call, configurable per tool)
- Catches exceptions and formats as tool error messages (no stack traces leaked to model)
- **Rate Limiting**: Max 10 tool calls per conversation turn (prevent infinite loops)

**3. Tool Registry (Horizontal Scalability)**
- Maintains `ConcurrentDictionary<string, IToolHandler>` (tool name → handler)
- Registers local C# tools via attributes: `[AIFunction("ToolName", "Description")]`
- Discovers MCP tools dynamically from MCP server manifests
- Provides tool schemas for prompt engineering (system message includes tool definitions)
- MUST be thread-safe (concurrent tool registrations during startup, concurrent lookups during execution)
- **Extensibility**: Implement `IToolHandler` for custom tool types beyond C#/MCP

**4. MCP Adapter (Minimal Coupling)**
- Translates MCP tool manifests to `IToolHandler` instances
- Manages HTTP clients for MCP server communication
- Applies Polly retry policies (transient fault handling)
- Isolated from core parser/dispatcher logic (MCP is one handler type among many)

**5. Conversation Loop (Great Telemetry)**
- Sends user message + tool schemas to Phi-4-mini
- Detects `functools[...]` in response → parse + dispatch
- Appends tool result as `{"role": "tool", "content": "...", "tool_call_id": "..."}`
- Re-prompts model with conversation history including tool result
- Repeats until model responds without functools (final answer) or max iterations reached (default 5)
- **Observability**: Every step emits structured logs and spans

**6. Observability (Great Telemetry)**
- **Structured Logs**: Tool call attempts, successes, failures, timeouts (JSON format, searchable)
- **Metrics**: 
  - Tool invocation latency (P50/P95/P99 per tool)
  - Error rates per tool (count, percentage)
  - Parser throughput (functools blocks/sec)
  - Dispatcher queue depth (if async)
- **Distributed Tracing**: OpenTelemetry spans for parse → validate → dispatch → execute
- **Dashboard Integration**: Aspire telemetry exports to Aspire Dashboard (real-time visualization)

**7. Extensibility (Horizontal Scalability)**
- Adding new MCP tools: Update MCP server config, restart app (no code changes to invocation layer)
- Adding new C# tools: Implement method with `[AIFunction]`, register in DI container (no dispatcher changes)
- Custom tool validation: Implement `IToolValidator` interface for domain-specific checks
- Custom tool handlers: Implement `IToolHandler` for non-MCP/non-C# tools (e.g., Python subprocess, gRPC service)

**Testing Requirements (Correctness-First):**

**Unit Tests (xUnit + FluentAssertions):**
- Parser: Valid JSON, malformed JSON, partial streaming blocks, edge cases (empty array, duplicate names)
- Dispatcher: Validation (unknown tool, invalid arguments), whitelisting, timeout handling
- Registry: Thread-safe registration, concurrent lookups, duplicate name handling

**Integration Tests (TestContainers for MCP servers):**
- Full loop: Mock model → functools → tool execution → re-prompt → final answer
- Error propagation: Tool throws exception → dispatcher formats error → model receives error message
- Timeout scenarios: Tool exceeds timeout → dispatcher cancels → model receives timeout error

**E2E Tests (Real Phi-4-mini + Real MCP tools):**
- Weather query: "What's the weather in Seattle?" → geocode → forecast → formatted response
- Allergen query: "Pollen levels in Berlin?" → geocode → allergen data → formatted response
- Multi-tool query: "Weather and pollen in Paris?" → geocode → forecast + allergen → combined response

**Performance Benchmarks (BenchmarkDotNet):**
- Parser latency: <10ms for typical functools blocks (<1KB JSON)
- Dispatcher validation: <5ms per tool call (includes schema validation)
- Total invocation overhead: <50ms excluding actual tool execution time (MCP HTTP call excluded)
- Registry lookup: <1μs per tool name (concurrent dictionary access)

**Security Constraints (Secure-By-Default):**
- **Tool Whitelist**: MUST be explicit (reject unknown tools by default, log attempts)
- **Argument Sanitization**: Validate against JSON Schema, reject oversized inputs (>10MB per argument)
- **No Dynamic Code Execution**: Forbidden: `eval`, reflection for arbitrary types, deserialization to `object`
- **Rate Limiting**: Max 10 tool calls per conversation turn (prevent infinite loops, DoS attacks)
- **Input Validation**: All tool arguments validated before invocation (type, range, format per schema)

**Error Handling Principles:**
- **Fail Fast**: Parser/Dispatcher errors stop execution immediately (don't invoke tools with bad data)
- **Graceful Degradation**: Malformed functools → return error message to model → model can retry or respond without tools
- **No Leakage**: Exception stack traces never sent to model (only sanitized error messages)
- **Audit Trail**: All errors logged with context (tool name, arguments, error type, timestamp)

**Rationale**: Phi-4-mini's custom functools format requires a parsing layer. This principle ensures the implementation is:
1. **Correct**: Schema validation catches errors before invocation
2. **Secure**: Explicit whitelist, input sanitization, rate limiting
3. **Scalable**: Minimal coupling, thread-safe registry, extensible handlers
4. **Observable**: Structured logs, metrics, tracing for production debugging
5. **Testable**: Unit/integration/E2E tests cover happy paths and error scenarios

The architecture supports both local C# tools and MCP-discovered tools without requiring core code changes when tools are added. Success is measured by: zero security violations, passing acceptance tests, meeting performance targets, and enabling tool addition without parser/dispatcher modifications.

## Technology Stack Constraints

**Required Stack:**
- **.NET SDK**: 10.0.100+ (pinned in `global.json`)
- **Aspire**: 13.0.0-preview.1+ (`Aspire.Hosting.AppHost`, `Aspire.Hosting`)
- **Agent Framework**: Microsoft.Extensions.AI 10.0.0-preview.1.25071.7+
- **UI Framework**: Blazor Server (from aichatweb template)
- **Testing**: xUnit 2.9.2+, bUnit 1.31.3+, Playwright 1.49.0+, BenchmarkDotNet 0.14.0+
- **Resilience**: Polly 8.5.0+

**Forbidden Stack:**
- Semantic Kernel (conflicts with Agent Framework principle)
- Azure OpenAI SDK (conflicts with Zero Cost principle)
- Blazor WebAssembly (template uses Server mode)

## Development Workflow

**Pre-Implementation Gates:**
1. Constitution check (all 11 principles verified)
2. Specification review (user stories, functional requirements, acceptance criteria)
3. Plan approval (technical approach, architecture decisions)
4. Test design (write failing tests before implementation)

**Code Review Requirements:**
- All PRs must verify constitution compliance (checklist in PR template)
- Breaking changes require explicit justification in PR description
- Performance regressions >10% require optimization or justification

**Quality Gates:**
- Build succeeds on all three platforms (Windows/macOS/Linux)
- All tests pass (unit + component + integration + E2E)
- Code coverage >80% for new code
- No high-severity accessibility violations (axe DevTools)

## Governance

This constitution is **binding for all code, dependencies, documentation, and architectural decisions**.

**Amendment Process:**
1. Propose change in GitHub issue with rationale
2. Document impact on existing code/templates
3. Update constitution with incremented version (semantic versioning)
4. Create migration plan for affected components
5. Update all affected templates/documentation

**Versioning Policy:**
- **MAJOR**: Backward-incompatible principle removal or redefinition
- **MINOR**: New principle added or material expansion of existing principle
- **PATCH**: Clarifications, wording improvements, typo fixes

**Compliance Verification:**
- `/speckit.analyze` command checks all principles against spec/plan/tasks
- CI pipeline enforces technology stack constraints (dependency scanning)
- Code review checklist includes constitution verification

**Version**: 1.2.0 | **Ratified**: 2025-11-16 | **Last Amended**: 2025-11-16
