# Research Document: Agent Framework Migration and Model Abstraction

**Feature**: 003-model-abstraction  
**Date**: 2025-11-20  
**Status**: Complete

## Executive Summary

This document resolves all NEEDS CLARIFICATION items from the implementation plan's Technical Context. Research covers Agent Framework migration patterns, tool invocation handler architecture, configuration-driven prompts, model configuration schema, and state management with AgentThread.

**Key Decisions**:

- Agent Framework middleware system ideal for IToolInvocationHandler architecture
- Configuration-driven handler discovery via named DI services
- Markdown prompts loaded via IPromptProvider with file system access
- JSON Schema validation for ModelConfiguration with Options pattern
- AgentThread replaces manual conversation history management in Blazor

## Research Task 1: Agent Framework Migration Patterns

### Decision: ChatClientAgent API and Middleware System

**Research Findings**:

Microsoft Agent Framework (`Microsoft.Agents.AI`) provides:

- **ChatClientAgent**: Production-ready agent abstraction built on `IChatClient`
- **AgentMiddleware**: Composable middleware pipeline for cross-cutting concerns
- **AgentThread**: State management with conversation history and thread lifecycle
- **Tool Integration**: Simplified tool registration compared to `AIFunction`

**Migration Pattern**:

```csharp
// BEFORE: Direct IChatClient usage
public class ChatService
{
    private readonly IChatClient _chatClient;
    
    public async Task<ChatCompletion> GetResponseAsync(string userMessage)
    {
        var messages = new List<ChatMessage> { new(ChatRole.User, userMessage) };
        var response = await _chatClient.CompleteAsync(messages, new ChatOptions());
        return response;
    }
}

// AFTER: ChatClientAgent with middleware
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

**DI Registration Pattern**:

```csharp
// Conditional middleware based on configuration
services.AddSingleton<ChatClientAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var config = sp.GetRequiredService<IOptions<ModelConfiguration>>().Value;
    var promptProvider = sp.GetRequiredService<IPromptProvider>();
    
    // Create agent with system prompt
    var agent = chatClient.CreateAIAgent(
        instructions: promptProvider.GetSystemPrompt(),
        tools: sp.GetRequiredService<IEnumerable<Delegate>>() // Tool methods
    );
    
    // Conditionally apply handler based on ToolInvocationStrategy
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

**Tool Registration Comparison**:

```csharp
// BEFORE: AIFunction (Microsoft.Extensions.AI)
public AIFunction GetWeather() => AIFunctionFactory.Create(
    (string location) => $"Weather in {location}",
    name: "get_weather",
    description: "Get weather for location"
);

// Registration
var tools = new List<AIFunction>();
tools.Add(weatherTools.GetWeather());
var options = new ChatOptions { Tools = tools };

// AFTER: Agent Framework (simplified)
public class WeatherTools
{
    [Description("Get weather for a location")]
    public string GetWeather(string location) => $"Weather in {location}";
}

// Registration (automatic discovery)
services.AddSingleton<WeatherTools>();
var agent = chatClient.CreateAIAgent(
    instructions: systemPrompt,
    tools: [
        weatherTools.GetWeather, // Method delegate
        airQualityTools.GetAirQuality
    ]
);
```

**Alternatives Considered**:

| Alternative | Rejected Because |
|-------------|------------------|
| Continue with direct IChatClient | No agent abstractions, manual state management, missing middleware system |
| Semantic Kernel | Violates Constitution Principle III (Agent Framework Only) |
| Custom agent wrapper | Reinventing Agent Framework capabilities, higher maintenance burden |

**Decision Rationale**: Agent Framework provides production-ready patterns for middleware, state management, and tool integration. Middleware system is ideal for conditional handler application based on `ToolInvocationStrategy`.

---

## Research Task 2: Tool Invocation Handler Architecture

### Decision: IToolInvocationHandler Interface with Keyed Services

**Research Findings**:

Agent Framework middleware signature:

```csharp
public delegate Task<AgentRunResponse> AgentMiddlewareDelegate(AgentInvokeContext context);

public interface IAgentMiddleware
{
    Task<AgentRunResponse> InvokeAsync(
        AgentInvokeContext context,
        AgentMiddlewareDelegate next
    );
}
```

**Design Decision**: Create `IToolInvocationHandler` interface matching middleware signature, use **keyed DI services** for discovery.

**Interface Design**:

```csharp
/// <summary>
/// Interface for model-specific tool invocation handling.
/// Implementations parse custom tool formats (e.g., functools, ReAct JSON)
/// and execute tools via the Agent Framework pipeline.
/// </summary>
public interface IToolInvocationHandler
{
    /// <summary>
    /// Processes agent invocation context, potentially intercepting and handling
    /// custom tool call formats before/after calling the next middleware.
    /// </summary>
    /// <param name="context">Agent invocation context with messages, tools, options</param>
    /// <param name="next">Next middleware in the pipeline</param>
    /// <returns>Agent run response with tool results integrated</returns>
    Task<AgentRunResponse> InvokeAsync(
        AgentInvokeContext context,
        AgentMiddlewareDelegate next
    );
}
```

**Handler Discovery via Keyed Services**:

```csharp
// DI Registration (Program.cs or ServiceExtensions.cs)
services.AddKeyedSingleton<IToolInvocationHandler, FunctoolsHandler>("Functools");
services.AddKeyedSingleton<IToolInvocationHandler, ReActJSONHandler>("ReActJSON");
// Future handlers registered with unique keys

// Usage (ChatClientAgent factory)
if (!string.IsNullOrEmpty(config.ToolInvocationStrategy))
{
    var handler = serviceProvider.GetRequiredKeyedService<IToolInvocationHandler>(
        config.ToolInvocationStrategy // "Functools", "ReActJSON", etc.
    );
    agent.AddMiddleware(async (context) => await handler.InvokeAsync(context, next));
}
```

**FunctoolsHandler Implementation** (converted from FunctoolsChatClient decorator):

```csharp
public class FunctoolsHandler : IToolInvocationHandler
{
    private readonly ILogger<FunctoolsHandler> _logger;
    private readonly FunctoolsParser _parser;
    
    public async Task<AgentRunResponse> InvokeAsync(
        AgentInvokeContext context,
        AgentMiddlewareDelegate next
    )
    {
        // 1. Inject functools format into system prompt (if not already present)
        // 2. Call next middleware (actual model invocation)
        var response = await next(context);
        
        // 3. Parse functools blocks from response
        var functoolsBlocks = _parser.ExtractFunctoolsBlocks(response.Text);
        if (!functoolsBlocks.Any())
            return response; // No tool calls, return as-is
        
        // 4. Execute tools (Agent Framework handles this via tool registry)
        // 5. Append tool results to conversation
        // 6. Re-invoke model with tool results
        // 7. Return final response
        
        _logger.LogInformation("Processed {Count} functools calls", functoolsBlocks.Count);
        return response;
    }
}
```

**Middleware vs Handler Pattern**:

| Pattern | Pros | Cons | Decision |
|---------|------|------|----------|
| Direct Middleware | Simpler registration | Less flexibility, harder to swap implementations | ❌ Rejected |
| Handler Interface + Middleware Wrapper | Flexible, testable, discoverable via DI | Extra abstraction layer | ✅ **Selected** |
| Decorator Pattern (current) | Works with IChatClient | Not composable, doesn't integrate with Agent Framework telemetry | ❌ Replace |

**Alternatives Considered**:

| Alternative | Rejected Because |
|-------------|------------------|
| Attribute-based discovery ([Tool Invocation("Functools")]) | Requires reflection, violates constitution security principle (no dynamic code execution) |
| Factory pattern (HandlerFactory.Create(strategy)) | Less idiomatic in .NET DI, keyed services provide same capability |
| Plugin system (load handlers from assemblies) | Over-engineered for current needs, future extensibility via keyed services sufficient |

**Decision Rationale**: Keyed DI services provide clean discovery without reflection, integrate naturally with Agent Framework middleware system, and enable adding handlers via DI registration (no core code changes).

---

## Research Task 3: Configuration-Driven Prompt Loading

### Decision: IPromptProvider with File System Access

**Research Findings**:

Prompt loading strategies:

1. **Embedded Resources**: Compile prompts into assembly (immutable, versioned)
2. **File System**: Load from disk (editable without recompilation, hotswap-friendly)
3. **Database/API**: Remote storage (overkill for local-first AI)

**Design Decision**: File system loading via `IPromptProvider`, prompts in `prompts/*.md` directory.

**Interface Design**:

```csharp
/// <summary>
/// Provides system prompts and model metadata for agent configuration.
/// </summary>
public interface IPromptProvider
{
    /// <summary>
    /// Loads the system prompt for the current model.
    /// </summary>
    /// <returns>System prompt text (Markdown format)</returns>
    string GetSystemPrompt();
    
    /// <summary>
    /// Gets the tool invocation strategy for the current model.
    /// </summary>
    string? ToolInvocationStrategy { get; }
}
```

**Implementation**:

```csharp
public class FileSystemPromptProvider : IPromptProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileSystemPromptProvider> _logger;
    
    public string GetSystemPrompt()
    {
        var promptFile = _configuration["AI:Models:{currentModel}:SystemPromptFile"];
        var promptPath = Path.Combine(AppContext.BaseDirectory, promptFile);
        
        if (!File.Exists(promptPath))
        {
            _logger.LogError("Prompt file not found: {Path}", promptPath);
            throw new FileNotFoundException($"Prompt file not found: {promptPath}");
        }
        
        return File.ReadAllText(promptPath);
    }
    
    public string? ToolInvocationStrategy =>
        _configuration["AI:Models:{currentModel}:ToolInvocationStrategy"];
}
```

**File Location Convention**:

```text
prompts/
├── weather-assistant.md       # Default use case (weather queries)
├── code-assistant.md          # Future use case (code generation)
└── data-analyst.md            # Future use case (data analysis)
```

**Prompt Format** (Markdown):

```markdown
# Weather Assistant System Prompt

You are a helpful weather assistant that provides accurate weather information.

## Available Tools

- **get_weather**: Retrieves weather forecast for a location
- **get_air_quality**: Retrieves air quality and pollen data

## Instructions

1. Always ask for location if not provided
2. Use tools to retrieve real-time data
3. Format responses clearly with temperature, conditions, and recommendations

## Functools Format (Phi-4 Mini / Qwen)

When calling tools, use this format:

functools[{"name": "get_weather", "arguments": {"location": "Seattle"}}]
```

**Prompt Composition** (future enhancement):

```csharp
// Base prompt + model-specific additions
public class CompositePromptProvider : IPromptProvider
{
    public string GetSystemPrompt()
    {
        var basePrompt = LoadPrompt("prompts/base.md");
        var modelSpecificPrompt = LoadPrompt($"prompts/{modelId}.md");
        return $"{basePrompt}\n\n{modelSpecificPrompt}";
    }
}
```

**Alternatives Considered**:

| Alternative | Rejected Because |
|-------------|------------------|
| Embedded resources | Requires recompilation for prompt changes, prevents runtime experimentation |
| Database storage | Over-engineered for local-first AI, adds unnecessary dependency |
| Hardcoded strings | Current pattern, violates configuration-driven principle |
| Per-model prompt files (weather-phi4.md, weather-gpt4.md) | Violates industry standard (single prompt per use case), constitution specifies behavior controlled by ToolInvocationStrategy not separate files |

**Decision Rationale**: File system loading enables prompt iteration without recompilation. Single prompt per use case (industry standard) with model-specific behavior controlled by `ToolInvocationStrategy` configuration property. Aligns with constitution Clarification 2.

---

## Research Task 4: Model Configuration Schema

### Decision: JSON Schema with .NET Options Pattern

**Research Findings**:

.NET configuration validation patterns:

1. **Options pattern** (`IOptions<T>`): Type-safe configuration binding
2. **Data annotations**: Declarative validation (`[Required]`, `[Url]`)
3. **FluentValidation**: Complex validation rules
4. **JSON Schema**: Design-time validation in editors

**Design Decision**: Combine Options pattern + Data Annotations + JSON Schema for comprehensive validation.

**ModelConfiguration Model**:

```csharp
using System.ComponentModel.DataAnnotations;

public class AIConfiguration
{
    [Required]
    public string DefaultModel { get; set; } = "phi-4-mini";
    
    [Required]
    public Dictionary<string, ModelConfiguration> Models { get; set; } = new();
}

public class ModelConfiguration
{
    [Required]
    public string Provider { get; set; } = string.Empty; // Foundry, Ollama, OpenAI, etc.
    
    [Required]
    [Url]
    public string Endpoint { get; set; } = string.Empty;
    
    public string? ApiKey { get; set; } // Nullable for local models
    
    public string? ToolInvocationStrategy { get; set; } // "Functools", "ReActJSON", null
    
    [Required]
    public string SystemPromptFile { get; set; } = "prompts/weather-assistant.md";
}
```

**DI Registration with Validation**:

```csharp
// Program.cs
services.AddOptions<AIConfiguration>()
    .Bind(builder.Configuration.GetSection("AI"))
    .ValidateDataAnnotations()
    .ValidateOnStart(); // Fail fast at startup
```

**JSON Schema** (`contracts/appsettings.schema.json`):

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "AI Configuration Schema",
  "type": "object",
  "properties": {
    "AI": {
      "type": "object",
      "properties": {
        "DefaultModel": {
          "type": "string",
          "description": "Default model identifier",
          "examples": ["phi-4-mini", "qwen2.5-vl-3b", "gpt-4o"]
        },
        "Models": {
          "type": "object",
          "additionalProperties": {
            "$ref": "#/definitions/ModelConfiguration"
          }
        }
      },
      "required": ["DefaultModel", "Models"]
    }
  },
  "definitions": {
    "ModelConfiguration": {
      "type": "object",
      "properties": {
        "Provider": {
          "type": "string",
          "enum": ["Foundry", "Ollama", "OpenAI", "AzureOpenAI", "GoogleGemini"]
        },
        "Endpoint": {
          "type": "string",
          "format": "uri"
        },
        "ApiKey": {
          "type": ["string", "null"],
          "description": "API key (required for cloud providers, null for local)"
        },
        "ToolInvocationStrategy": {
          "type": ["string", "null"],
          "enum": ["Functools", "ReActJSON", "ReActXML", null],
          "description": "Handler for custom tool formats (null = native calling)"
        },
        "SystemPromptFile": {
          "type": "string",
          "description": "Path to markdown prompt file"
        }
      },
      "required": ["Provider", "Endpoint", "SystemPromptFile"]
    }
  }
}
```

**Environment Variable Substitution**:

```json
{
  "AI": {
    "Models": {
      "gpt-4o": {
        "ApiKey": "${OPENAI_API_KEY}" // Substituted at runtime
      }
    }
  }
}
```

**Implementation**:

```csharp
public static class ConfigurationExtensions
{
    public static string? ResolveEnvironmentVariables(this string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (!value.StartsWith("${") || !value.EndsWith("}")) return value;
        
        var varName = value[2..^1]; // Extract variable name
        return Environment.GetEnvironmentVariable(varName);
    }
}

// Usage
var apiKey = config.ApiKey.ResolveEnvironmentVariables();
```

**Alternatives Considered**:

| Alternative | Rejected Because |
|-------------|------------------|
| YAML configuration | .NET has first-class JSON support, YAML requires extra library |
| XML configuration | Verbose, less human-friendly than JSON |
| Code-based configuration (no appsettings.json) | Violates configuration-driven principle, requires recompilation |
| Per-environment files (appsettings.{env}.json) | Already supported by .NET, works with our design |

**Decision Rationale**: Options pattern provides type-safe binding and validation. JSON Schema enables design-time validation in VS Code. Data Annotations catch configuration errors at startup (fail fast). Environment variable substitution supports cloud deployments without committing secrets.

---

## Research Task 5: State Management Migration

### Decision: AgentThread Replaces Manual Conversation History

**Research Findings**:

Agent Framework provides `AgentThread` for conversation state management:

- **Thread Lifecycle**: Create, use, dispose (optional persistence)
- **Conversation History**: Automatically maintained by framework
- **Checkpointing**: Save/restore thread state (future feature)
- **Thread Deletion**: Clean up old conversations

**Migration Pattern**:

```csharp
// BEFORE: Manual conversation history in Chat.razor
private List<ChatMessage> _conversationHistory = new();

private async Task SendMessage()
{
    _conversationHistory.Add(new ChatMessage(ChatRole.User, userInput));
    var response = await _chatClient.CompleteAsync(_conversationHistory, options);
    _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, response.Message.Content));
}

// AFTER: AgentThread in Chat.razor
private AgentThread? _thread;
private ChatClientAgent _agent;

protected override void OnInitialized()
{
    _thread = _agent.GetNewThread(); // Framework manages state
}

private async Task SendMessage()
{
    var response = await _agent.RunAsync(userInput, _thread);
    // Thread automatically updated with full conversation context
    StateHasChanged(); // Trigger Blazor re-render
}

public void Dispose()
{
    _thread?.Dispose(); // Optional: cleanup
}
```

**Thread Management in Blazor Component**:

```csharp
@page "/chat"
@implements IDisposable
@inject ChatClientAgent Agent

<div class="chat-container">
    @if (_thread == null)
    {
        <button @onclick="StartNewChat">Start New Chat</button>
    }
    else
    {
        <ChatMessages Thread="_thread" />
        <ChatInput OnSend="SendMessage" Disabled="@_isSending" />
    }
</div>

@code {
    private AgentThread? _thread;
    private bool _isSending;
    
    private void StartNewChat()
    {
        _thread?.Dispose(); // Clean up old thread
        _thread = Agent.GetNewThread();
        StateHasChanged();
    }
    
    private async Task SendMessage(string userInput)
    {
        _isSending = true;
        try
        {
            var response = await Agent.RunAsync(userInput, _thread!);
            // UI automatically updates from thread state
        }
        finally
        {
            _isSending = false;
            StateHasChanged();
        }
    }
    
    public void Dispose()
    {
        _thread?.Dispose();
    }
}
```

**Thread Persistence** (future feature):

```csharp
// Save thread state
var threadState = _thread.GetState();
await _storage.SaveThreadAsync(threadId, threadState);

// Restore thread state
var threadState = await _storage.LoadThreadAsync(threadId);
_thread = _agent.RestoreThread(threadState);
```

**Alternatives Considered**:

| Alternative | Rejected Because |
|-------------|------------------|
| Keep manual history list | Misses Agent Framework state management benefits, no built-in persistence |
| Custom state manager | Reinventing AgentThread, higher maintenance burden |
| SignalR for state sync | Over-engineered, AgentThread is server-side (Blazor Server already uses SignalR) |

**Decision Rationale**: AgentThread provides production-ready state management with minimal code. Framework handles conversation history automatically. Enables future features like thread persistence and checkpointing without refactoring.

---

## Summary of Decisions

| Research Task | Decision | Key Benefit |
|---------------|----------|-------------|
| **Agent Framework Migration** | ChatClientAgent + Middleware | Production-ready patterns, composable middleware |
| **Tool Invocation Handlers** | IToolInvocationHandler + Keyed DI | Pluggable without core changes, no reflection |
| **Prompt Loading** | IPromptProvider + File System | Editable without recompilation, industry standard |
| **Configuration Schema** | Options Pattern + JSON Schema | Type-safe, fail-fast validation, design-time checks |
| **State Management** | AgentThread | Automatic history, enables future persistence |

**No NEEDS CLARIFICATION Remaining**: All technical decisions finalized and ready for Phase 1 design.

**Next Steps**: Generate `data-model.md`, `contracts/*.schema.json`, `quickstart.md` in Phase 1.
