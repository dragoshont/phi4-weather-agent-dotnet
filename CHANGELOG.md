# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Modern AI Terminology**: Refactored project naming from "LocalAIAgent" to "LocalAIAgent"
  - Updated all namespaces: `LocalAIAgent.*` → `LocalAIAgent.*`
  - Renamed solution file: `local-conversational-agent.sln` → `local-ai-agent.sln`
  - Renamed all project directories and `.csproj` files
  - Updated documentation, scripts, and configuration files
  - Reflects modern AI agent terminology (agentic capabilities, tool use, multi-modal support)

- **Microsoft Agent Framework Integration**: Migrated from direct `IChatClient` usage to `ChatClientAgent` for production-grade agent orchestration
  - `AgentThread` for automatic conversation state management
  - Middleware pipeline architecture for pluggable tool invocation handlers
  - Built-in telemetry and observability

- **Configuration-Driven Model Selection**: Switch AI models without recompilation via `appsettings.json`
  - Multi-model configuration structure (`AI:Models` dictionary)
  - Model-specific parameters (temperature, maxTokens, topP, penalties)
  - Environment variable substitution for API keys (`${ENV_VAR_NAME}` syntax)
  - Fail-fast configuration validation at startup
  - Default model resolution from `AI:DefaultModel`

- **Model Dropdown UI**: Runtime model selection in chat interface
  - Dropdown populated from configured models
  - Format: "Provider: model-name (endpoint-type)"
  - Model locking after first message (preserves conversation context)
  - Re-enable on new chat session
  - Accessibility compliance (ARIA labels, keyboard navigation)

- **Tool Invocation Handler Architecture**: Interface-based middleware for model-specific tool calling formats
  - `IToolInvocationHandler` interface
  - `FunctoolsHandler` for Phi-4, Qwen, Mistral models
  - Native tool calling support for GPT-4o, Claude, Gemini
  - Conditional handler application via `ToolInvocationStrategy` config
  - Keyed DI service registration for handler discovery

- **Configuration-Driven Prompt Management**: Load system prompts from markdown files
  - `IPromptProvider` interface with file system access
  - Prompts stored in `prompts/` directory
  - `weather-assistant.md` default prompt
  - Updates take effect on application restart

- **Comprehensive Model Configuration Tests**: Unit tests for Options pattern validation
  - `ModelConfigurationTests.cs` with 25 test cases
  - Data annotation validation (endpoint URL, temperature ranges, token limits)
  - Options pattern integration tests
  - Environment variable substitution verification
  - All provider types tested (Ollama, FoundryLocal, AzureOpenAI, OpenAI, Gemini)

- **LocalAIAgent.OpenMeteo Assembly**: Extracted weather tools to separate library
  - Encapsulates `openmeteo_sdk` dependency (v1.23.0)
  - Clean namespace: `LocalAIAgent.OpenMeteo.Tools`
  - Reusable in other .NET projects
  - Explicit assembly loading in `ToolDiscoveryService`
  - Scaffolded test project: `LocalAIAgent.OpenMeteo.Tests`

- **Handler Resolution Tests**: Unit tests for `IToolInvocationHandler` keyed DI
  - `HandlerResolutionTests.cs` with 7 test cases
  - Null/empty strategy handling
  - Functools handler mapping validation
  - Invalid strategy key error handling
  - Multi-handler registration scenarios

### Changed

- **BREAKING**: Replaced `IChatClient` with `ChatClientAgent` in all services
  - `ChatAgentService` now uses Agent Framework APIs
  - `RunAsync(userMessage, thread)` replaces `CompleteAsync(messages, options)`
  - Manual conversation history lists replaced with `AgentThread`

- **BREAKING**: Configuration structure changed from single model to multi-model dictionary
  - Old: `Ollama:Endpoint`, `Ollama:ModelName`
  - New: `AI:Models:{model-id}:Endpoint`, `AI:Models:{model-id}:Name`

- **BREAKING**: `FunctoolsChatClient` decorator pattern replaced with `FunctoolsHandler` middleware
  - No longer wraps `IChatClient`
  - Implements `IToolInvocationHandler` for Agent Framework integration
  - Applied conditionally via `AddMiddleware()` in `ChatClientAgent` factory

- **Chat.razor**: Updated to use `AgentThread` for state management
  - Removed manual `_conversationHistory` list
  - Thread lifecycle: `CreateThread()` on init, `Dispose()` on cleanup
  - Automatic history tracking via framework

- **Tool Namespace Migration**: Tools extracted to separate assembly
  - Old: `Phi4WeatherAgent.Tools.*` (embedded in Agent assembly)
  - New: `LocalAIAgent.OpenMeteo.Tools.*` (separate library)
  - Tools: `GeocodingTools`, `WeatherTools`, `AirQualityTools`
  - Explicit assembly loading in `ToolDiscoveryService`

### Fixed

- **Empty Web Page**: Fixed Blazor SignalR WebSocket blocking issue
  - Removed blocking `GetAwaiter().GetResult()` call in `ChatClientAgent` factory
  - Changed `ChatAgentService` from Scoped to Singleton lifetime
  - WebSocket handshake now succeeds (HTTP 101 Switching Protocols)

- **Integration Tests**: Updated `ChatClientDITests.cs` to validate Agent Framework
  - Replaced `IChatClient_ShouldBeRegistered` with `ChatClientAgent_ShouldBeRegistered`
  - Fixed async/await issues: `CreateThread()` is synchronous
  - Fixed method naming: `SendMessageAsync()` → `RunAsync()`

### Deprecated

- `FunctoolsChatClient` decorator class (replaced by `FunctoolsHandler` middleware)
- Direct `IChatClient.CompleteAsync()` usage (replaced by `ChatClientAgent.RunAsync()`)
- Manual conversation history management (replaced by `AgentThread`)

### Removed

- None (deprecated items remain for backward compatibility)

### Security

- API keys now support environment variable substitution (prevents committing secrets)
- Configuration validation prevents invalid endpoint URLs at startup
- Tool whitelist validation preserved in `FunctoolsHandler`

## [0.1.0] - 2025-01-15

### Added

- Initial release with Phi-4-mini weather assistant
- OpenMeteo API integration (geocoding, weather, air quality)
- Blazor Server chat UI with SignalR
- Aspire 13 orchestration
- WCAG 2.1 AA accessibility compliance
- Functools tool invocation format support
- MCP tool registration
- Docker Desktop integration

---

## Migration Guide: v0.1.0 → v0.2.0 (Agent Framework)

### Configuration Changes

**Before** (`appsettings.json`):
```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "phi-4-mini"
  }
}
```

**After** (`appsettings.json`):
```json
{
  "AI": {
    "DefaultModel": "phi-4-mini",
    "Models": {
      "phi-4-mini": {
        "Name": "phi-4-mini",
        "Provider": "Ollama",
        "Endpoint": "http://localhost:11434",
        "ToolInvocationStrategy": "Functools"
      }
    }
  }
}
```

### Code Changes

**Before** (Direct `IChatClient` usage):
```csharp
public class ChatService
{
    private readonly IChatClient _chatClient;
    private List<ChatMessage> _history = new();

    public async Task<string> GetResponseAsync(string userInput)
    {
        _history.Add(new ChatMessage(ChatRole.User, userInput));
        var response = await _chatClient.CompleteAsync(_history, options);
        _history.Add(new ChatMessage(ChatRole.Assistant, response.Message.Content));
        return response.Message.Content;
    }
}
```

**After** (Agent Framework):
```csharp
public class ChatService
{
    private readonly ChatClientAgent _agent;
    private AgentThread _thread;

    protected override void OnInitialized()
    {
        _thread = _agent.CreateThread();
    }

    public async Task<string> GetResponseAsync(string userInput)
    {
        var response = await _agent.RunAsync(userInput, _thread);
        return response.Text; // AgentThread automatically updated
    }

    public void Dispose()
    {
        _thread?.Dispose();
    }
}
```

### Tool Registration Changes

**Before** (`AIFunction`):
```csharp
var tools = new List<AIFunction>();
tools.Add(AIFunctionFactory.Create(
    (string location) => GetWeather(location),
    name: "get_weather",
    description: "Get weather for location"
));

var options = new ChatOptions { Tools = tools };
```

**After** (Agent Framework):
```csharp
// Tools registered via method delegates
var agent = chatClient.CreateAIAgent(
    instructions: systemPrompt,
    tools: [
        weatherTools.GetWeather,  // Method delegate
        geocodingTools.Geocode,
        airQualityTools.GetAirQuality
    ]
);
```

### Handler Migration

**Before** (`FunctoolsChatClient` decorator):
```csharp
services.AddSingleton<IChatClient>(sp =>
{
    var innerClient = new OllamaChatClient(endpoint, modelName);
    return new FunctoolsChatClient(innerClient, toolRegistry);
});
```

**After** (`FunctoolsHandler` middleware):
```csharp
services.AddKeyedSingleton<IToolInvocationHandler, FunctoolsHandler>("Functools");

services.AddSingleton<ChatClientAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var config = sp.GetRequiredService<IOptions<AIConfiguration>>().Value;

    var agent = chatClient.CreateAIAgent(instructions, tools);

    if (!string.IsNullOrEmpty(config.ToolInvocationStrategy))
    {
        var handler = sp.GetRequiredKeyedService<IToolInvocationHandler>(
            config.ToolInvocationStrategy
        );
        agent.AddMiddleware(context => handler.InvokeAsync(context, next));
    }

    return agent;
});
```

### Testing Changes

**Before**:
```csharp
[Fact]
public void IChatClient_ShouldBeRegistered()
{
    var chatClient = _factory.Services.GetRequiredService<IChatClient>();
    Assert.NotNull(chatClient);
}
```

**After**:
```csharp
[Fact]
public void ChatClientAgent_ShouldBeRegistered()
{
    var agent = _factory.Services.GetRequiredService<ChatClientAgent>();
    Assert.NotNull(agent);
}

[Fact]
public async Task ChatAgentService_ShouldHandleUserMessage()
{
    var chatService = _factory.Services.GetRequiredService<ChatAgentService>();
    var thread = chatService.CreateThread(); // Synchronous

    var response = await chatService.RunAsync("Hello", thread, CancellationToken.None);

    Assert.NotNull(response);
    Assert.NotEmpty(response);
}
```

### Environment Variables

**Before** (hardcoded or User Secrets):
```json
{
  "AzureOpenAI": {
    "ApiKey": "sk-..." // ❌ Committed to source
  }
}
```

**After** (environment variable substitution):
```json
{
  "AI": {
    "Models": {
      "gpt-4o": {
        "ApiKey": "${AZURE_OPENAI_API_KEY}" // ✓ Resolved at runtime
      }
    }
  }
}
```

```bash
# Set before launching application
export AZURE_OPENAI_API_KEY="sk-..."
dotnet run
```

---

## Upgrade Checklist

- [ ] Update `appsettings.json` to new `AI:Models` structure
- [ ] Add `ToolInvocationStrategy` to models using functools format
- [ ] Set environment variables for cloud model API keys
- [ ] Replace `IChatClient` with `ChatClientAgent` in services
- [ ] Replace manual conversation history with `AgentThread`
- [ ] Update tool registration from `AIFunction` to method delegates
- [ ] Migrate `FunctoolsChatClient` decorator to `FunctoolsHandler` middleware
- [ ] Update integration tests to validate Agent Framework APIs
- [ ] Test model switching via dropdown UI
- [ ] Verify configuration validation catches invalid configs at startup
