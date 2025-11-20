# Constitution v1.2.0

## Project: Phi-4 Weather Assistant

### Principle I: Local-First AI
**All AI inference MUST run locally** on developer hardware. No cloud AI services permitted.

**Platform-Specific Model Hosting:**
- **Windows**: Foundry Local (via `aspire-ai` workload)
- **macOS**: Foundry Local (via `aspire-ai` workload)  
- **Linux**: Ollama (manual installation, documented in setup guide)

**Model**: Microsoft Phi-4 Mini (3.8GB optimized ONNX for CPU/NPU inference)

### Principle II: .NET 10 Requirement
**.NET 10 SDK is MANDATORY** for this project.

- SDK Version: `10.0.100` or later (pinned in `global.json`)
- Target Framework: `net10.0`
- No .NET 9 or earlier versions permitted in production code

### Principle III: Agent Framework (Not Semantic Kernel)
**Use Microsoft.Extensions.AI Agent Framework exclusively.**

- ✅ Permitted: `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Extensions.AI.Ollama`
- ❌ Forbidden: `Microsoft.SemanticKernel`, `Microsoft.SemanticKernel.Agents`

**Rationale**: Agent Framework is the .NET 10 native abstraction. Semantic Kernel is a separate framework and should not be mixed.

### Principle IV: Aspire 13
**Use .NET Aspire 13 preview** for orchestration and observability.

- Aspire Version: `13.0.0-preview.1` or later
- Required Workloads: `aspire`, `aspire-ai` (Windows/macOS only)
- Aspire Dashboard for telemetry visualization (auto-launch in development)

### Principle V: Model Context Protocol (MCP)
**Weather data MUST use MCP tools** for structured data retrieval.

- MCP Tools: Geocoding, Weather Forecast, Allergen Data
- HTTP Clients: OpenMeteo APIs (free, no API keys)
- Retry Policies: Polly 8.5+ for resilience

### Principle VI: Zero Cloud Runtime Costs
**No paid services or API keys** in production runtime.

- ✅ Permitted: Free APIs (OpenMeteo), local model hosting
- ❌ Forbidden: Azure OpenAI, OpenAI API, paid weather APIs

**Exception**: Development/CI may use cloud resources (Azure Pipelines, GitHub Actions)

### Principle VII: Accessibility (WCAG 2.1 AA)
**UI MUST meet WCAG 2.1 Level AA** compliance.

- Keyboard navigation for all features
- Screen reader compatibility (ARIA labels)
- Color contrast ratios ≥4.5:1 for text
- Testing: axe DevTools + manual screen reader validation

### Principle VIII: Template-Based Architecture
**Leverage Microsoft aichatweb template** to avoid reinventing UI.

- Base Template: `dotnet new aichatweb --provider ollama`
- Template Version: Latest compatible with .NET 10
- Customizations:
  - ✅ Keep: Chat UI components, IChatClient integration
  - ❌ Remove: Vector store, document ingestion, semantic search
  - ✨ Add: Weather card visualizations, MCP tool integration

### Principle IX: Testing Coverage
**Maintain comprehensive test coverage** across unit, integration, and E2E.

- **Unit Tests**: xUnit for business logic (Agent, MCP tools)
- **Component Tests**: bUnit for Blazor UI
- **E2E Tests**: Playwright for full user workflows
- **Benchmarks**: BenchmarkDotNet for Agent Framework initialization (T076a)

**Coverage Target**: >80% for critical paths (weather queries, MCP tool execution)

### Principle X: Cross-Platform Development
**Support Windows, macOS, and Linux** development environments.

- Setup Scripts: PowerShell (Windows), Bash (macOS/Linux)
- CI Matrix: Test on all three platforms
- Documentation: Platform-specific setup instructions in README

### Principle XI: MIT License
**All project code is MIT-licensed.** All dependencies MUST be permissively licensed.

- ✅ Permitted: MIT, Apache 2.0, BSD
- ❌ Forbidden: GPL, AGPL, proprietary licenses

---

## Enforcement

This constitution is **binding for all code, dependencies, and documentation**. Violations should be flagged in code review and specification analysis.

**Version History:**
- v1.2.0 (2025-11-16): Clarified Agent Framework requirement, Aspire 13, platform-specific hosting
- v1.1.0: Added template-based architecture principle
- v1.0.0: Initial constitution
