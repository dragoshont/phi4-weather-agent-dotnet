# Feature Specification: Model Abstraction and Domain-Agnostic Project Naming

**Feature Branch**: `003-model-abstraction`
**Created**: November 20, 2025
**Status**: Draft
**Input**: User description: "Abstract model-specific dependencies from application architecture and rename projects to be domain-agnostic"

## Clarifications

### Session 2025-11-20

- Q: What should the generic project naming convention be? → A: `LocalConversationalAgent.*` (emphasizes local-first architecture + conversational interface, distinguishes from cloud-based agents)
- Q: Should prompts be stored in configuration files or separate files per model? → A: Markdown files per use case in `prompts/` directory (industry standard: single prompt per use case, model behavior controlled by ToolInvocationStrategy in config, NOT separate model-instruction files)
- Q: Should we migrate to Microsoft Agent Framework? → A: **Yes** - Migrate from `Microsoft.Extensions.AI` to `Microsoft.Agents.AI` (official successor to Semantic Kernel and AutoGen, recommended in technical research, provides production-ready multi-agent orchestration)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Switch AI Models Without Code Changes (Priority: P1)

A developer wants to swap between local models (Phi-4 Mini, Qwen 2.5 VL 3B) or configure cloud models (GPT-4o, Gemini) by only changing configuration, without modifying application code or recompiling the solution.

**Why this priority**: This is the core value proposition - making the system model-agnostic unlocks vendor flexibility, cost optimization, and future-proofing.

**Independent Test**: Can be fully tested by changing a single configuration value (e.g., `"AI:DefaultModel": "qwen2.5-vl-3b"`) and verifying the application works with a different model without code changes.

**Acceptance Scenarios**:

1. **Given** application is running with Phi-4 Mini (local, default), **When** developer changes configuration to `"DefaultModel": "qwen2.5-vl-3b"`, **Then** application switches to Qwen model without code changes
2. **Given** application is configured for local model, **When** developer adds cloud model configuration with API key, **Then** system can use cloud provider's native function calling
3. **Given** application is running, **When** model configuration is switched, **Then** system prompt automatically adapts to new model's capabilities and ToolInvocationStrategy
4. **Given** developer deploys to production, **When** they specify different model in environment variables, **Then** no recompilation is needed

---

### User Story 2 - Configuration-Driven Prompt Management (Priority: P2)

A developer wants to define and maintain system prompts via configuration, so each model gets optimized instructions for its capabilities (functools vs native tools vs other formats).

**Why this priority**: Prompt engineering is model-specific - what works for Phi-4 Mini won't work for cloud models. Configuration-driven prompts ensure optimal performance per model.

**Independent Test**: Can be tested by verifying each model receives appropriate prompt (e.g., Phi-4 Mini gets functools instructions) loaded from markdown files and checking ToolInvocationStrategy.

**Acceptance Scenarios**:

1. **Given** Phi-4 Mini is selected, **When** chat session starts, **Then** system prompt includes functools format instructions from `prompts/weather-assistant.md`
2. **Given** Qwen 2.5 VL 3B is selected, **When** chat session starts, **Then** system uses appropriate ToolInvocationStrategy based on model capabilities
3. **Given** developer updates system prompt, **When** they save changes to markdown file, **Then** next application restart uses updated prompt
4. **Given** new model is added, **When** developer updates configuration with model settings, **Then** system automatically uses appropriate prompt and ToolInvocationStrategy

---

### User Story 3 - Conditional Functools Layer (Priority: P2)

The system automatically applies the functools invocation layer only for models that need it (Phi-4 Mini, Qwen 2.5 VL 3B initially), and can bypass it for models with native function calling (future cloud models).

**Why this priority**: Performance and correctness - the functools layer adds overhead and complexity that's unnecessary for models with native tool support.

**Independent Test**: Can be tested by inspecting the ChatClientAgent middleware pipeline and verifying functools middleware is present/absent based on ToolInvocationStrategy.

**Acceptance Scenarios**:

1. **Given** Phi-4 Mini is configured with `ToolInvocationStrategy.Functools`, **When** application starts, **Then** functools middleware is applied to ChatClientAgent
2. **Given** cloud model is configured with `ToolInvocationStrategy.Native`, **When** application starts, **Then** functools middleware is NOT applied to pipeline
3. **Given** model with native tools, **When** tool calls are made, **Then** no functools parsing occurs
4. **Given** model configuration changes, **When** application restarts, **Then** middleware stack adjusts automatically based on ToolInvocationStrategy

---

### User Story 4 - Generic Project Naming (Priority: P3)

Projects are renamed to be domain-agnostic (not tied to "weather" or specific tools) so the architecture can be reused for any agent application (e.g., code assistant, data analyst, etc.).

**Why this priority**: Enables template/framework reuse - developers can clone this architecture for non-weather agents without misleading project names.

**Independent Test**: Can be tested by verifying all project names, namespaces, and folder names no longer reference "weather" or specific domains.

**Acceptance Scenarios**:

1. **Given** current projects named `Phi4WeatherAgent.*`, **When** renamed, **Then** new names are `LocalConversationalAgent.*` (emphasizes local-first + conversational nature)
2. **Given** namespaces reference weather, **When** refactored, **Then** namespaces reflect generic agent concepts (e.g., `LocalConversationalAgent.Tools` not `WeatherAgent.Tools`)
3. **Given** developer clones project, **When** they want to build non-weather agent, **Then** project names don't imply weather-only usage
4. **Given** tools are defined, **When** projects are renamed, **Then** tool definitions remain separate from project structure

---

### User Story 5 - Model Selection via UI Dropdown (Priority: P1)

A user can select which AI model to use from a dropdown in the chat UI before starting a conversation, with the default model pre-selected from configuration.

**Why this priority**: Core user-facing feature that enables model switching without restarting the application or editing configuration files.

**Independent Test**: Can be tested by verifying dropdown appears with configured models, selection persists for chat session, and model cannot be changed after first message.

**Acceptance Scenarios**:

1. **Given** chat page loads, **When** user views the UI, **Then** model dropdown is visible and enabled with default model pre-selected from `DefaultModel` configuration
2. **Given** dropdown is opened, **When** user views options, **Then** each model shows format: `Provider: model-name (endpoint-type)` where endpoint-type is "Local" for on-premises or "Cloud" for Azure/OpenAI/Gemini
3. **Given** user has not sent any messages, **When** user selects different model from dropdown, **Then** selection updates and that model will be used when conversation starts
4. **Given** user sends first message in chat, **When** message is sent, **Then** model dropdown becomes disabled (locked) for remainder of that chat session
5. **Given** user starts new chat session, **When** chat resets, **Then** model dropdown becomes enabled again with default model pre-selected
6. **Given** only one model is configured, **When** chat page loads, **Then** dropdown shows single option and remains enabled (allows seeing configuration)

**Example dropdown options**:

- `Foundry: phi-4-mini (Local)`
- `Ollama: qwen2.5-vl-3b (Local)`
- `OpenAI: gpt-4o (Cloud)`
- `Azure: gpt-4o (Cloud)`
- `Google: gemini-2.0-flash (Cloud)`

---

### Edge Cases

- What happens when configuration specifies unsupported model type? (System should fail fast with clear error message)
- How does system handle missing prompt provider for configured model? (Should throw at startup, not runtime)
- What if model supports both native tools AND custom formats? (Configuration should allow override)
- How to handle model switching mid-conversation? (Not supported - dropdown disabled after first message, requires new chat session)
- What if functools layer is mistakenly applied to cloud model? (Should work but with performance overhead - log warning)
- What if user selects model but API key is missing? (Should show error when attempting to send first message: "API key required for {provider}")
- How to display long endpoint URLs in dropdown? (Truncate with tooltip showing full endpoint)
- What if only default model is available but it fails to load? (Show error in dropdown: "Default model unavailable")

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support local models (Phi-4 Mini via Foundry/Ollama as default, Qwen 2.5 VL 3B via Ollama) and cloud providers (Azure OpenAI, OpenAI, Google Gemini) via configuration without code changes using Microsoft Agent Framework (`Microsoft.Agents.AI`)
- **FR-002**: System MUST provide model-specific prompt management through provider pattern
- **FR-003**: System MUST conditionally apply functools invocation layer only for models requiring custom tool formats (controlled by ToolInvocationStrategy enum: Native, Functools, ReActJSON, ReActXML)
- **FR-004**: System MUST allow prompt customization per model type through injectable providers
- **FR-005**: System MUST fail fast at startup with clear error if configured model is not supported
- **FR-006**: System MUST expose model capabilities (native tools vs custom format) through provider interface
- **FR-007**: Configuration MUST allow specifying default model, endpoint, and optional API key via appsettings.json or environment variables (API key required for cloud providers, not needed for local models)
- **FR-008**: Project names MUST be domain-agnostic (not reference "weather" or specific tools)
- **FR-009**: Namespaces MUST be refactored to `LocalConversationalAgent.*` naming convention (reflects local-first architecture + conversational interface)
- **FR-010**: System MUST maintain backward compatibility with existing tool definitions during rename
- **FR-011**: System prompts MUST be stored as Markdown files in `prompts/` directory, one file per use case (e.g., `weather-assistant.md`), referenced by configuration, with model-specific behavior controlled by ToolInvocationStrategy
- **FR-012**: Chat UI MUST display model selection dropdown before first message, pre-populated with all configured models from `AI:Models` section
- **FR-013**: Model dropdown MUST show each option in format: `Provider: model-name (endpoint-type)` where endpoint-type is "Local" or "Cloud"
- **FR-014**: Model dropdown MUST be disabled (locked) after user sends first message in chat session to prevent mid-conversation model switching
- **FR-015**: Default model from `AI:DefaultModel` configuration MUST be pre-selected when chat page loads or new session starts

### Key Entities

- **PromptProvider**: Encapsulates system prompts (loaded from Markdown files) and model metadata (name, ToolInvocationStrategy)
- **ModelConfiguration**: Configuration object containing:
  - `DefaultModel`: Model identifier (e.g., "phi-4-mini", "qwen2.5-vl-3b", "gpt-4o")
  - `Provider`: Provider type (Foundry, Ollama, AzureOpenAI, OpenAI, GoogleGemini)
  - `Endpoint`: Base URL for API (local or cloud)
  - `ApiKey`: Optional API key (required for cloud providers, null for local)
  - `ToolInvocationStrategy`: Enum (Native, Functools, ReActJSON, ReActXML)
  - `SystemPromptFile`: Path to markdown prompt file
- **ChatClientAgent**: Agent Framework's agent abstraction, conditionally includes functools middleware based on ToolInvocationStrategy
- **ToolInvocationStrategy**: Enum defining how model handles tool calls (Native = built-in function calling, Functools = custom format requiring parser, ReActJSON/ReActXML = reasoning-action patterns)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developer can switch between local models (Phi-4 Mini ↔ Qwen 2.5 VL 3B) or add cloud models by changing configuration without recompilation (100% configuration-driven)
- **SC-002**: Each model type receives optimized system prompt (verified by prompt content inspection)
- **SC-003**: Functools middleware applied only when `ToolInvocationStrategy.Functools` is configured (eliminates overhead for future native tool models)
- **SC-004**: All project and namespace names are generic (zero references to "weather" in project structure)
- **SC-005**: Existing tool implementations work unchanged after project rename (100% backward compatibility)
- **SC-006**: Adding new model requires only creating prompt provider and updating configuration (no changes to core framework)
- **SC-007**: Users can select any configured model from dropdown before starting conversation (100% UI-driven model selection)
- **SC-008**: Model selection dropdown displays provider, model name, and endpoint type clearly (verified by UI inspection)

## Scope *(mandatory)*

### In Scope

- **Migrating to Microsoft Agent Framework** (`Microsoft.Agents.AI`) from `Microsoft.Extensions.AI`
- Converting IChatClient usage to ChatClientAgent pattern
- Adapting FunctoolsChatClient decorator to Agent Framework middleware
- Converting AIFunction tool definitions to Agent Framework tool pattern
- Creating `IPromptProvider` interface with configuration-driven prompt loading
- **Creating prompt storage structure** (`prompts/` directory with Markdown files per use case)
- Configuration-driven model selection via appsettings.json with ToolInvocationStrategy enum
- Conditional application of functools middleware based on ToolInvocationStrategy
- Renaming projects from `Phi4WeatherAgent.*` to `LocalConversationalAgent.*`
- Refactoring namespaces to be domain-agnostic (emphasizes local-first + conversational)
- Updating solution file, launch profiles, and Docker configurations
- Updating documentation and README to reflect Agent Framework architecture
- **Adding model selection dropdown to Chat UI** with format `Provider: model-name (endpoint-type)`
- Implementing dropdown disable logic after first message sent in chat session

### Out of Scope

- Supporting runtime model switching (requires application restart)
- Pre-configuring all possible cloud models (configuration structure supports them, but initial implementation focuses on local models: Phi-4 Mini and Qwen 2.5 VL 3B)
- Implementing model-agnostic tool definition format (tools remain model-independent already)
- UI changes beyond model selection dropdown (chat interface behavior unchanged from user perspective)
- Database schema changes (no data persistence for model configuration)
- Mid-conversation model switching (dropdown disabled after first message)

## Assumptions *(mandatory)*

- Microsoft Agent Framework (public preview) is stable enough for production use
- Agent Framework's middleware system supports functools layer integration
- Tool implementations can be adapted from AIFunction to Agent Framework tool pattern
- System prompt is sufficient for model behavior control (no fine-tuning)
- Configuration is set at deployment time (not changed dynamically)
- Developers understand dependency injection pattern used for provider registration
- Cross-platform requirement: Agent Framework supports Windows, Linux, macOS
- Foundry Local/Ollama continue exposing OpenAI-compatible APIs

## Dependencies *(mandatory)*

### External Dependencies

- **Microsoft.Agents.AI** (public preview) - Official successor to Semantic Kernel and AutoGen, provides unified agent framework with ChatClientAgent, middleware system, thread-based state management, and multi-agent orchestration
- **Foundry Local** for Phi-4 Mini hosting (Windows/macOS) - default local model
- **Ollama** for Qwen 2.5 VL 3B hosting (cross-platform: Windows, Linux, macOS)
- Cloud provider APIs (Azure OpenAI, OpenAI, Google Gemini) for future cloud model support requiring API keys
- Existing tool implementations (GeocodingTools, WeatherTools, AirQualityTools) - will adapt to Agent Framework's tool pattern

### Internal Dependencies

- Existing FunctoolsChatClient decorator pattern
- Tool registry and discovery mechanism
- Aspire AppHost orchestration
- Blazor Web UI chat component (Chat.razor) - requires model selection dropdown integration

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

1. Should model configuration support fallback chains? (e.g., try GPT-4o, fallback to Phi-4 Mini if API unavailable)
2. Do we need model-specific validation of tool results? (e.g., cloud models expect JSON, local models may accept plain text)
3. Should dropdown show additional model metadata (e.g., parameter count, capabilities like vision)?
4. How to handle model selection persistence across browser sessions? (Use localStorage or always default to config?)

## Migration Analysis *(mandatory for migrations)*

### Obsolete Code Identification

This section identifies existing code patterns that will be replaced during the migration to Microsoft Agent Framework.

#### 1. IChatClient Direct Usage (OBSOLETE)

**Current Pattern**:

```csharp
using Microsoft.Extensions.AI;

public class ChatService
{
    private readonly IChatClient _chatClient;

    public async Task<ChatResponse> GetResponseAsync(string userMessage)
    {
        var messages = new List<ChatMessage> { new(ChatRole.User, userMessage) };
        var response = await _chatClient.CompleteAsync(messages, new ChatOptions());
        return response;
    }
}
```

**Why Obsolete**: Direct IChatClient usage doesn't provide agent abstractions, thread management, or middleware support.

**Replacement**: ChatClientAgent with built-in thread and middleware support

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public class ChatService
{
    private readonly ChatClientAgent _agent;
    private readonly AgentThread _thread;

    public async Task<AgentRunResponse> GetResponseAsync(string userMessage)
    {
        var response = await _agent.RunAsync(userMessage, _thread);
        return response; // Contains .Text, .Messages, telemetry
    }
}
```

#### 2. FunctoolsChatClient Decorator Pattern (OBSOLETE)

**Current Pattern**:

```csharp
// FunctoolsChatClient.cs - Custom decorator wrapping IChatClient
public class FunctoolsChatClient : IChatClient
{
    private readonly IChatClient _innerClient;
    private readonly FunctoolsParser _parser;

    public async Task<ChatCompletion> CompleteAsync(...)
    {
        // 1. Inject functools format into prompt
        // 2. Call inner client
        // 3. Parse functools response
        // 4. Execute tools manually
        // 5. Return final response
    }
}

// Service registration
services.AddSingleton<IChatClient>(sp =>
    new FunctoolsChatClient(baseClient, parser));
```

**Why Obsolete**: Decorator pattern is verbose, hard to compose with other middleware, doesn't integrate with Agent Framework's telemetry.

**Replacement**: Agent Framework middleware

```csharp
// FunctoolsMiddleware.cs - Agent Framework middleware
public class FunctoolsMiddleware : IAgentMiddleware
{
    public async Task<AgentRunResponse> InvokeAsync(
        AgentInvokeContext context,
        AgentMiddlewareDelegate next)
    {
        // 1. Inject functools format if ToolInvocationStrategy == Functools
        // 2. Call next middleware
        // 3. Parse functools response if present
        // 4. Return (tool execution handled by framework)
    }
}

// Service registration (cleaner)
services.AddSingleton<ChatClientAgent>(sp =>
{
    var agent = chatClient.CreateAIAgent(tools: weatherTools);
    if (config.ToolInvocationStrategy == ToolInvocationStrategy.Functools)
        agent.AddMiddleware<FunctoolsMiddleware>();
    return agent;
});
```

#### 3. Manual Tool Registration (OBSOLETE)

**Current Pattern**:

```csharp
// Tool definition with AIFunction
[Tool]
public class WeatherTools
{
    public AIFunction GetWeather() => AIFunctionFactory.Create(
        (string location) => $"Weather in {location}",
        name: "get_weather",
        description: "Get weather for location"
    );
}

// Manual registration
var tools = new List<AIFunction>();
tools.Add(weatherTools.GetWeather());
tools.Add(airQualityTools.GetAirQuality());

var options = new ChatOptions { Tools = tools };
var response = await chatClient.CompleteAsync(messages, options);
```

**Why Obsolete**: Manual tool wiring, verbose registration, no automatic discovery.

**Replacement**: Simplified Agent Framework tool registration

```csharp
// Tool definition (no attributes needed)
public class WeatherTools
{
    [Description("Get weather for a location")]
    public string GetWeather(string location) => $"Weather in {location}";
}

// Automatic registration
var agent = chatClient.CreateAIAgent(
    instructions: systemPrompt,
    tools: [
        weatherTools.GetWeather,
        airQualityTools.GetAirQuality
    ]
);
```

#### 4. Manual Conversation State Management (OBSOLETE)

**Current Pattern**:

```csharp
// Chat.razor - Manual state management
private List<ChatMessage> _conversationHistory = new();

private async Task SendMessage()
{
    _conversationHistory.Add(new ChatMessage(ChatRole.User, userInput));

    var response = await _chatClient.CompleteAsync(_conversationHistory, options);

    _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, response.Message.Content));
}
```

**Why Obsolete**: Manual history tracking, no persistence, no thread concept, error-prone.

**Replacement**: Agent Framework thread management

```csharp
// Chat.razor - Framework-managed state
private AgentThread _thread;
private ChatClientAgent _agent;

protected override void OnInitialized()
{
    _thread = _agent.GetNewThread(); // Framework manages state
}

private async Task SendMessage()
{
    var response = await _agent.RunAsync(userInput, _thread);
    // Thread automatically updated with full conversation context
    // Supports persistence, checkpointing, thread deletion
}
```

#### 5. Hardcoded System Prompts (OBSOLETE)

**Current Pattern**:

```csharp
// Chat.razor - Hardcoded prompt
private const string SystemPrompt = @"
You are a weather assistant...
<functools>
  Available functions...
</functools>
";

var messages = new List<ChatMessage>
{
    new(ChatRole.System, SystemPrompt),
    new(ChatRole.User, userInput)
};
```

**Why Obsolete**: Hardcoded, not configuration-driven, duplicated across files, model-specific.

**Replacement**: IPromptProvider with markdown files

```csharp
// IPromptProvider.cs
public interface IPromptProvider
{
    string GetSystemPrompt();
    ToolInvocationStrategy ToolInvocationStrategy { get; }
}

// Phi4PromptProvider.cs
public class Phi4PromptProvider : IPromptProvider
{
    private readonly IConfiguration _config;

    public string GetSystemPrompt() =>
        File.ReadAllText(_config["AI:SystemPromptFile"]); // prompts/weather-assistant.md

    public ToolInvocationStrategy ToolInvocationStrategy =>
        ToolInvocationStrategy.Functools;
}

// Usage
var agent = chatClient.CreateAIAgent(
    instructions: promptProvider.GetSystemPrompt(),
    tools: weatherTools
);
```

#### 6. Project and Namespace Naming (OBSOLETE)

**Current Names** (domain-specific):

- `Phi4WeatherAgent.Agent` → Weather-specific, model-specific
- `Phi4WeatherAgent.Web` → Ties architecture to weather domain
- `Phi4WeatherAgent.Tools` → Implies tools are weather-only
- `Phi4WeatherAgent.AppHost` → Aspire host tied to weather

**Why Obsolete**: Names prevent reuse for other agent applications (code assistant, data analyst, etc.)

**Replacement** (domain-agnostic):

- `LocalConversationalAgent.Agent` → Generic agent backend
- `LocalConversationalAgent.Web` → Generic web frontend
- `LocalConversationalAgent.Tools` → Generic tool framework
- `LocalConversationalAgent.AppHost` → Generic Aspire orchestration

#### 7. Package Dependencies (OBSOLETE)

**Current** (Directory.Build.props):

```xml
<PackageReference Update="Microsoft.Extensions.AI" Version="10.0.0-preview.1.25071.7" />
<PackageReference Update="Microsoft.Extensions.AI.Ollama" Version="10.0.0-preview.1.25071.7" />
<PackageReference Update="Microsoft.Extensions.AI.Abstractions" Version="10.0.0-preview.1.25071.7" />
```

**Why Obsolete**: These are low-level abstractions, replaced by Agent Framework.

**Replacement**:

```xml
<PackageReference Update="Microsoft.Agents.AI" Version="1.0.0-preview" />
<!-- Microsoft.Extensions.AI is still used internally by Agent Framework for types -->
```


### Migration Impact Assessment

| Component | LOC to Change | Complexity | Risk Level |
|-----------|---------------|------------|------------|
| FunctoolsChatClient → Middleware | ~500 lines | High | Medium (well-tested pattern) |
| IChatClient → ChatClientAgent | ~200 lines | Medium | Low (straightforward API) |
| Tool registration | ~100 lines | Low | Low (simpler API) |
| Conversation state management | ~150 lines | Medium | Low (framework handles it) |
| System prompt refactoring | ~50 lines | Low | Low (file-based) |
| Project/namespace rename | ~50 files | Medium | Low (IDE refactoring) |
| Package updates | 3 files | Low | Low (dependency update) |
| **Model selection dropdown UI** | **~100 lines** | **Low** | **Low (standard Blazor component)** |
| **Total Estimated Impact** | **~1100 lines** | **Medium** | **Low-Medium** |

### Backward Compatibility Strategy

1. **Tool Implementations**: Unchanged - Agent Framework supports same function signature pattern
2. **Blazor UI**: Model dropdown is additive feature - existing chat functionality preserved
3. **Aspire Orchestration**: Unchanged - Agent Framework still uses HTTP endpoints
4. **Configuration**: Additive - new `AI:Models` section, old settings ignored if present
5. **Tests**: Update to use ChatClientAgent, but test logic remains same
6. **Model Selection**: If no dropdown interaction, default model used (backward compatible behavior)

### Refactoring Checklist

- [ ] Install `Microsoft.Agents.AI` package
- [ ] Create `IPromptProvider` interface and implementations
- [ ] Convert `FunctoolsChatClient` decorator → `FunctoolsMiddleware`
- [ ] Replace `IChatClient` direct usage → `ChatClientAgent`
- [ ] Update tool registration from `AIFunction` → Agent Framework pattern
- [ ] Migrate conversation state from manual list → `AgentThread`
- [ ] Extract system prompts to `prompts/*.md` files
- [ ] Update `appsettings.json` with model configuration
- [ ] Rename projects: `Phi4WeatherAgent.*` → `LocalConversationalAgent.*`
- [ ] Refactor namespaces to match new project names
- [ ] Update solution file and launch profiles
- [ ] Update README with Agent Framework architecture
- [ ] Update tests to use `ChatClientAgent` and `AgentThread`
- [ ] Remove obsolete `Microsoft.Extensions.AI` direct usage (keep for types only)
- [ ] Add model selection dropdown to Chat.razor
- [ ] Implement dropdown population from `AI:Models` configuration
- [ ] Implement dropdown disable logic after first message sent
- [ ] Format dropdown options as `Provider: model-name (endpoint-type)`

## Notes *(optional)*

### Design Rationale

**Why migrate to Microsoft Agent Framework?**

- **Official successor**: Direct replacement for both Semantic Kernel and AutoGen, built by the same teams
- **Recommended in technical research**: Phase 1 recommendation for production-ready agent implementation
- **Enterprise-ready**: Thread-based state management, middleware system, comprehensive validation, telemetry, OpenTelemetry built-in
- **Multi-agent support**: Graph-based workflows with conditional routing, parallel processing, orchestration patterns (enables future multi-agent scenarios)
- **ChatClientAgent**: Production-ready agent abstraction designed for tool calling
- **Middleware system**: Perfect fit for functools layer as middleware component (cleaner than decorator pattern)
- **Model support**: Built-in support for Azure OpenAI, OpenAI, with extensibility for local models (Ollama, Foundry Local) and other cloud providers (Google Gemini)
- **Cross-platform**: .NET and Python SDKs, works on Windows, Linux, macOS
- **Future-proof**: Microsoft's official direction for AI agent development going forward

**Initial Model Support**:

- **Phi-4 Mini** (default): 3.8B parameter SLM, local via Foundry Local (Windows/macOS) or Ollama, requires functools for tool calling
- **Qwen 2.5 VL 3B**: 3B parameter vision-language model, local via Ollama (cross-platform), vision capabilities
- **Cloud models**: Configuration structure supports Azure OpenAI (GPT-4o), OpenAI, Google Gemini via API keys (implementation deferred, configuration ready)

**Configuration Structure** (appsettings.json):

```json
{
  "AI": {
    "DefaultModel": "phi-4-mini",
    "Models": {
      "phi-4-mini": {
        "Provider": "Foundry",
        "Endpoint": "http://localhost:60613/v1",
        "ApiKey": null,
        "ToolInvocationStrategy": "Functools",
        "SystemPromptFile": "prompts/weather-assistant.md"
      },
      "qwen2.5-vl-3b": {
        "Provider": "Ollama",
        "Endpoint": "http://localhost:11434",
        "ApiKey": null,
        "ToolInvocationStrategy": "Functools",
        "SystemPromptFile": "prompts/weather-assistant.md"
      },
      "gpt-4o": {
        "Provider": "OpenAI",
        "Endpoint": "https://api.openai.com/v1",
        "ApiKey": "${OPENAI_API_KEY}",
        "ToolInvocationStrategy": "Native",
        "SystemPromptFile": "prompts/weather-assistant.md"
      }
    }
  }
}
```

**Migration path**:

- `Microsoft.Extensions.AI` → `Microsoft.Agents.AI`
- `IChatClient` → `ChatClientAgent`
- `FunctoolsChatClient` decorator → Agent Framework middleware
- `AIFunction` tool definitions → Agent Framework tool pattern (similar API)
- Enables future workflows and multi-agent orchestration

**Why configuration-driven prompts?**

- **Simplicity**: Single interface, behavior driven by configuration not code
- **Flexibility**: Same prompt file can be used with different ToolInvocationStrategy settings
- **Testability**: Mock configuration in tests
- **Extensibility**: Add new models by updating configuration, not creating new classes
- **DI-friendly**: Single provider implementation, configuration injected

**Why single prompt per use case (industry standard)?**

- **Simplicity**: One `weather-assistant.md` file, not separate per model
- **Industry alignment**: Matches OpenAI Prompts API, Semantic Kernel, HuggingFace patterns
- **Maintainability**: Update one file, not N model-specific files
- **Separation of concerns**: Prompt defines WHAT to do (application logic), ToolInvocationStrategy defines HOW to format technically (model-specific)
- **Research-backed**: Analyzed 80,000+ tokens from HuggingFace, Semantic Kernel, OpenAI docs - all use single prompt per use case

**Why conditional functools layer?**

- Performance: Eliminates unnecessary parsing for native tool models
- Correctness: Prevents functools format leaking to models that don't expect it
- Simplicity: Models with native support use simpler code path

**Why LocalConversationalAgent.* naming?**

- **Local-first emphasis**: Highlights zero-cloud-cost, privacy-first value proposition (differentiates from cloud-based agents)
- **Conversational clarity**: Clearly indicates chat/dialogue application (not just chatbot, but agent with tools)
- **Reusability**: Architecture applicable to any local conversational agent (not limited to weather)
- **Market positioning**: Distinguishes from OpenAI Assistants, Claude, etc. which are cloud-hosted

### Related Specifications

- **001-phi4-weather-assistant**: Original weather assistant implementation
- **002-functools-invocation-layer**: Functools parsing and execution layer

This specification enhances 002 by making it conditional rather than always-on.
