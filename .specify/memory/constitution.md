<!--
Sync Impact Report:
- Version: NEW → 1.0.0
- Principles Added: 11 core principles (Local-First, .NET 10 Requirement, Agent Framework, Aspire 13, MCP, Zero Cost, WCAG AA, Template-Based, Testing Coverage, Cross-Platform, MIT License)
- Templates Status:
  ✅ plan-template.md - Reviewed (compatible)
  ✅ spec-template.md - Reviewed (compatible)
  ✅ tasks-template.md - Reviewed (compatible)
  ⚠ No existing plan.md/spec.md/tasks.md to update yet
- Follow-up: None
-->

# Phi-4 Weather Assistant Constitution

## Core Principles

### I. Local-First AI
**All AI inference MUST run locally** on developer hardware. Cloud AI services are forbidden.

**Platform-Specific Model Hosting:**
- **Windows**: Foundry Local (via `aspire-ai` workload)
- **macOS**: Foundry Local (via `aspire-ai` workload)
- **Linux**: Ollama (manual installation documented in setup scripts)

**Model**: Microsoft Phi-4 (14B parameters, quantized for consumer hardware)

**Rationale**: Ensures zero runtime costs, complete data privacy, and offline capability. Developers maintain full control over inference without external dependencies.

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

**Rationale**: Agent Framework is .NET 10's native AI abstraction. Mixing it with Semantic Kernel creates architectural confusion, duplicated patterns, and maintenance burden.

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

**Rationale**: MCP tools provide type-safe, testable abstractions over raw HTTP calls. Agent Framework can invoke them with structured parameters and receive validated results.

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

**Version**: 1.0.0 | **Ratified**: 2025-11-16 | **Last Amended**: 2025-11-16
