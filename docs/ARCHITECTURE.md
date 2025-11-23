# Architecture Diagrams

## Component Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        User Interface                             │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         Blazor Chat UI (Components/Pages/Chat)           │   │
│  │  - Model Dropdown (WCAG 2.1 AA accessible)               │   │
│  │  - Message List (conversation history)                   │   │
│  │  - Input Field (user messages)                           │   │
│  └────────────────┬─────────────────────────────────────────┘   │
└────────────────────┼──────────────────────────────────────────────┘
                     │
                     │ IChatClient
                     ↓
┌─────────────────────────────────────────────────────────────────┐
│              Agent Layer (LocalAIAgent.Agent)                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              ChatAgentService                             │   │
│  │  - Wraps ChatClientAgent (Microsoft.Agents.AI)           │   │
│  │  - Manages AgentThread lifecycle                         │   │
│  │  - Exposes: RunAsync(), RunStreamingAsync()              │   │
│  └────────────────┬─────────────────────────────────────────┘   │
│                   │                                              │
│  ┌────────────────┴─────────────────────────────────────────┐   │
│  │            ChatClientFactory                              │   │
│  │  - Reads AI:Models configuration                         │   │
│  │  - Creates provider-specific IChatClient                 │   │
│  │  - Applies IToolInvocationHandler if needed              │   │
│  │  - Returns: Ollama/Foundry/Azure/OpenAI clients          │   │
│  └────────────────┬─────────────────────────────────────────┘   │
└────────────────────┼──────────────────────────────────────────────┘
                     │
                     │ ToolInvocationStrategy?
                     ↓
┌─────────────────────────────────────────────────────────────────┐
│      Handler Layer (Optional - Model-Specific Adapters)          │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │          FunctoolsHandler (for Qwen models)               │   │
│  │  - Converts function calls to Functools format           │   │
│  │  - Parses Functools responses                            │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │       ReActJSONHandler (future extensibility)             │   │
│  │  - Converts function calls to ReAct JSON format          │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                     │
                     │ Tool Discovery
                     ↓
┌─────────────────────────────────────────────────────────────────┐
│          Tool Layer (LocalAIAgent.OpenMeteo)                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │          GeocodingTools (Location Search)                 │   │
│  │  - SearchLocations(query): returns lat/lon + name        │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │          WeatherTools (Forecast Retrieval)                │   │
│  │  - GetCurrentWeather(lat, lon): current conditions       │   │
│  │  - GetWeatherForecast(lat, lon, days): 7-day forecast   │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │          AirQualityTools (Pollen/Allergens)               │   │
│  │  - GetPollenForecast(lat, lon): grass/birch/ragweed      │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  NOTE: All tools return primitive types (string, int, double).   │
│        SDK types (openmeteo_sdk) are internal-only.              │
└─────────────────────────────────────────────────────────────────┘
                     │
                     │ HTTP API Calls
                     ↓
┌─────────────────────────────────────────────────────────────────┐
│                  External Services                                │
│  - OpenMeteo API (https://api.open-meteo.com)                   │
│  - Nominatim Geocoding (https://nominatim.openstreetmap.org)    │
└─────────────────────────────────────────────────────────────────┘
```

## Data Flow (User Query → AI Response)

```
┌─────────────┐
│    User     │
│ "Weather in │
│  Seattle?"  │
└──────┬──────┘
       │
       │ 1. User message + selected model
       ↓
┌────────────────────────────────────────────┐
│     Blazor Chat Component                  │
│  - Lock dropdown after first message       │
│  - Add message to conversation history     │
└──────┬─────────────────────────────────────┘
       │
       │ 2. AddUserMessageAsync(message)
       ↓
┌────────────────────────────────────────────┐
│       ChatAgentService                     │
│  - Get AgentThread for conversation        │
│  - Call ChatClientAgent.RunAsync()         │
└──────┬─────────────────────────────────────┘
       │
       │ 3. RunAsync(message, thread)
       ↓
┌────────────────────────────────────────────┐
│    ChatClientAgent (M.Agents.AI)           │
│  - Determine if tool calls needed          │
│  - Execute model inference via IChatClient │
└──────┬─────────────────────────────────────┘
       │
       │ 4. Model inference
       ↓
┌────────────────────────────────────────────┐
│         IChatClient Provider               │
│  (Ollama/Foundry/Azure/OpenAI)             │
│  - Send messages + tool definitions        │
│  - Receive tool call request               │
└──────┬─────────────────────────────────────┘
       │
       │ 5. Tool call: SearchLocations("Seattle")
       ↓
┌────────────────────────────────────────────┐
│       GeocodingTools                       │
│  - Call Nominatim API                      │
│  - Return: "Seattle, WA, USA (47.6, -122.3)│
└──────┬─────────────────────────────────────┘
       │
       │ 6. Tool result
       ↓
┌────────────────────────────────────────────┐
│    ChatClientAgent (M.Agents.AI)           │
│  - Append tool result to conversation      │
│  - Request model to synthesize response    │
└──────┬─────────────────────────────────────┘
       │
       │ 7. Second model call with tool results
       ↓
┌────────────────────────────────────────────┐
│         IChatClient Provider               │
│  - Synthesize natural language response    │
│  - Stream response tokens                  │
└──────┬─────────────────────────────────────┘
       │
       │ 8. Streaming response
       ↓
┌────────────────────────────────────────────┐
│       ChatAgentService                     │
│  - Accumulate streaming tokens             │
│  - Update UI in real-time                  │
└──────┬─────────────────────────────────────┘
       │
       │ 9. Display response
       ↓
┌────────────────────────────────────────────┐
│     Blazor Chat Component                  │
│  - Render assistant message                │
│  - Enable input for next user message      │
└────────────────────────────────────────────┘
       │
       ↓
┌─────────────┐
│    User     │
│   Sees:     │
│ "Currently  │
│  55°F..."   │
└─────────────┘
```

## Agent Framework Migration (Legacy → New)

```
BEFORE (Legacy IChatClient):
┌────────────────────────────────────────────┐
│        Chat Component                      │
└──────┬─────────────────────────────────────┘
       │ Direct IChatClient usage
       ↓
┌────────────────────────────────────────────┐
│    IChatClient.GetStreamingResponseAsync() │
│  - Raw message handling                    │
│  - Manual conversation state management    │
│  - No built-in tool orchestration          │
└────────────────────────────────────────────┘

AFTER (Agent Framework):
┌────────────────────────────────────────────┐
│        Chat Component                      │
└──────┬─────────────────────────────────────┘
       │ ChatAgentService abstraction
       ↓
┌────────────────────────────────────────────┐
│       ChatAgentService                     │
│  - Wraps ChatClientAgent                   │
│  - Manages AgentThread                     │
│  - Exposes clean async API                 │
└──────┬─────────────────────────────────────┘
       │
       ↓
┌────────────────────────────────────────────┐
│  ChatClientAgent (M.Agents.AI)             │
│  ✅ Automatic tool orchestration           │
│  ✅ Conversation state management          │
│  ✅ Multi-turn tool calling                │
│  ✅ AgentThread isolation                  │
└──────┬─────────────────────────────────────┘
       │
       ↓
┌────────────────────────────────────────────┐
│         IChatClient Provider               │
│  (Ollama/Foundry/Azure/OpenAI)             │
└────────────────────────────────────────────┘

BENEFITS:
✅ Centralized tool execution logic
✅ Thread-safe conversation isolation
✅ Easier testing (ChatAgentService mocking)
✅ Future-ready for checkpointing/persistence
✅ Consistent error handling
```

## Configuration-Driven Architecture

```
┌─────────────────────────────────────────────┐
│        appsettings.json                     │
│  AI:                                        │
│    DefaultModel: "phi-4-mini"               │
│    Models:                                  │
│      - Name: "phi-4-mini"                   │
│        Provider: FoundryLocal               │
│        Endpoint: http://localhost:5272      │
│        ToolInvocationStrategy: null         │
│      - Name: "qwen-vl-3b"                   │
│        Provider: Ollama                     │
│        Endpoint: http://localhost:11434     │
│        ToolInvocationStrategy: "Functools"  │
└──────┬──────────────────────────────────────┘
       │
       │ Configuration Binding
       ↓
┌────────────────────────────────────────────┐
│     AIConfiguration (Model)                │
│  - DefaultModel: string                    │
│  - Models: Dictionary<string, ModelConfig> │
└──────┬─────────────────────────────────────┘
       │
       │ Factory Pattern
       ↓
┌────────────────────────────────────────────┐
│      ChatClientFactory                     │
│  CreateChatClient(modelKey):               │
│    1. Load ModelConfiguration              │
│    2. Create provider-specific client      │
│    3. Apply handler if strategy specified  │
│    4. Return wrapped/unwrapped client      │
└──────┬─────────────────────────────────────┘
       │
       ├─ Provider = Ollama ──→ OllamaChatClient
       │
       ├─ Provider = Foundry ─→ FoundryChatClient
       │
       ├─ Provider = Azure ───→ AzureOpenAIChatClient
       │
       └─ ToolStrategy? ──→ Wrap with Handler
                            (FunctoolsHandler, ReActHandler)

ZERO-RECOMPILATION SWITCH:
1. Edit appsettings.json: Change DefaultModel
2. Restart application
3. New model active immediately
```

## Accessibility Architecture (WCAG 2.1 AA)

```
┌────────────────────────────────────────────┐
│       Model Dropdown Component             │
│                                            │
│  <select id="model-selector"               │
│          aria-label="Select AI model"      │◄─ Screen reader label
│          aria-describedby="help-text"      │◄─ Context description
│          @bind="selectedModel"             │
│          disabled="@isLocked">             │◄─ Programmatic state
│    @foreach (var model in models)          │
│    {                                       │
│      <option value="@model.Key">           │
│        @FormatDisplayName(model)           │◄─ Descriptive text
│      </option>                             │
│    }                                       │
│  </select>                                 │
│                                            │
│  <span id="help-text" class="sr-only">    │◄─ Associated help text
│    @helpMessage                            │
│  </span>                                   │
└────────────────────────────────────────────┘

ACCESSIBILITY VALIDATION:
┌────────────────────────────────────────────┐
│   Automated (CI/CD Pipeline)               │
│  ✅ HTML structure validation              │
│  ✅ ARIA attribute presence check          │
│  ✅ Semantic markup verification           │
└────────────────────────────────────────────┘
┌────────────────────────────────────────────┐
│   Manual (axe DevTools)                    │
│  ✅ Color contrast (4.5:1 text, 3:1 UI)    │
│  ✅ Keyboard navigation (Tab/Enter/Arrows) │
│  ✅ Focus indicators visible               │
│  ✅ Screen reader compatibility            │
│  ✅ Zero violations required               │
└────────────────────────────────────────────┘
```

## Assembly Boundaries (Encapsulation)

```
┌─────────────────────────────────────────────────────────────┐
│          LocalAIAgent.Agent (Core Assembly)                  │
│  PUBLIC API:                                                 │
│    - ChatAgentService                                        │
│    - ChatClientFactory                                       │
│    - IToolInvocationHandler                                  │
│    - ModelConfiguration, AIConfiguration                     │
│  INTERNAL:                                                   │
│    - Handler implementations                                 │
│    - Tool discovery internals                                │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│       LocalAIAgent.OpenMeteo (Tool Assembly)                 │
│  PUBLIC API:                                                 │
│    - GeocodingTools.SearchLocations(string): string          │
│    - WeatherTools.GetCurrentWeather(double, double): string  │
│    - AirQualityTools.GetPollenForecast(double, double): str  │
│  INTERNAL:                                                   │
│    - openmeteo_sdk classes (SDK encapsulation)               │
│    - DTO mapping logic                                       │
│    - HTTP client internals                                   │
│                                                              │
│  BOUNDARY ENFORCEMENT (SC-011):                              │
│    ✅ Only primitives (string, int, double) cross boundary   │
│    ✅ SDK types never appear in public signatures            │
│    ✅ Validated by OpenMeteoEncapsulationTests (Roslyn)      │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│          LocalAIAgent.Web (UI Assembly)                      │
│  DEPENDENCIES:                                               │
│    ← LocalAIAgent.Agent                                      │
│    ← LocalAIAgent.OpenMeteo (via tool discovery)             │
│  NO DIRECT SDK ACCESS:                                       │
│    ✗ Cannot reference openmeteo_sdk                          │
│    ✅ Consumes only tool method signatures                   │
└─────────────────────────────────────────────────────────────┘
```

## Key Design Principles

1. **Separation of Concerns**: UI ← Agent Layer ← Tool Layer ← External APIs
2. **Configuration Over Code**: Model switching via appsettings.json (zero recompilation)
3. **SDK Encapsulation**: Tools return primitives only, SDK types internal
4. **Accessibility First**: WCAG 2.1 AA compliance validated at build time
5. **Handler Extensibility**: IToolInvocationHandler enables model-specific adapters
6. **Agent Framework**: ChatClientAgent + AgentThread for conversation management
7. **Provider Agnostic**: Ollama, Foundry Local, Azure OpenAI, OpenAI, Gemini support
