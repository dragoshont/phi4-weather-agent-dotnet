# Data Model: Functools Invocation Layer

**Branch**: 002-functools-invocation-layer  
**Date**: 2025-11-17 (Updated after Foundry native support discovery)  
**Purpose**: Define core entities for tool registration, conversion to AIFunction, and execution

## Entity Relationship Diagram

```
┌──────────────────┐   register   ┌──────────────────┐   convert   ┌─────────────┐
│  ToolMetadata    │─────────────▶│  ToolRegistry    │─────────────▶│ AIFunction  │
│  (from [Tool]    │              │  (discovered at  │             │ (passed to  │
│   attributes)    │              │   startup)       │             │  Foundry)   │
└──────────────────┘              └──────────────────┘             └─────────────┘
        │                                  │                              │
        │                                  │                              │
    Discovered by                      Queried by                    Injected into
 ToolDiscoveryService              ChatOptionsBuilder              {Tool} placeholder
        │                                  │                              │
        │                                  │                              │
        └──────────────────────────────────┴──────────────────────────────┘
                                           │
                                           ▼
                                  ┌─────────────────┐
                                  │  FunctionCall   │ (parsed from model response)
                                  │  (functools     │
                                  │   syntax)       │
                                  └─────────────────┘
                                           │
                                           │ execute via
                                           ▼
                                  ┌─────────────────┐
                                  │   ToolResult    │ (returned to model)
                                  └─────────────────┘
```

## Foundry Template Integration

### AIFunction Schema Format

**Purpose**: Define expected JSON Schema format for Foundry's {Tool} placeholder injection

**Foundry Template Structure** (from inference_model.json):
```
{Tool} placeholder expects array of objects:
[
  {
    "type": "function",
    "function": {
      "name": "string",
      "description": "string",
      "parameters": {
        "type": "object",
        "properties": { ... },
        "required": [ ... ]
      }
    }
  }
]
```

**Example AIFunction** (GetWeather tool):
```json
{
  "type": "function",
  "function": {
    "name": "GetWeather",
    "description": "Retrieves current weather for a location",
    "parameters": {
      "type": "object",
      "properties": {
        "location": {
          "type": "string",
          "description": "City name or coordinates"
        },
        "units": {
          "type": "string",
          "enum": ["metric", "imperial"],
          "description": "Temperature unit system"
        }
      },
      "required": ["location"]
    }
  }
}
```

**Template Placeholder Semantics**:
- **{Tool}**: Replaced by Foundry with JSON array of function definitions from ChatOptions.Tools
- **Injection Point**: System prompt template before user message
- **Format Validation**: Foundry expects OpenAI function calling schema format
- **Version Requirement**: Available in Foundry Local 0.8.103+ with Phi-4-mini-instruct-generic-cpu:5 model

---

## Core Entities

### 1. FunctionCall

**Purpose**: Represents a parsed tool invocation from Phi-4-mini's functools block

**Properties**:

| Name | Type | Nullable | Description | Validation |
|------|------|----------|-------------|------------|
| `Name` | `string` | ❌ | Tool name exactly as model outputs it | Matches `^[a-zA-Z][a-zA-Z0-9_]*$`, max 100 chars |
| `Arguments` | `JsonElement` | ❌ | Unvalidated JSON arguments from model | May be empty object `{}`, never null |

**Example**:
```json
{
  "name": "GetWeather",
  "arguments": {
    "location": "Seattle",
    "units": "metric"
  }
}
```

**Validation Rules**:
- `Name` must match regex `^[a-zA-Z][a-zA-Z0-9_]*$` (alphanumeric + underscore, starts with letter)
- `Name` length: 1-100 characters
- `Arguments` must be valid JSON (enforced by `JsonElement` type)
- `Arguments` can be empty object but never null (use `JsonElement.Parse("{}")`)

**State Transitions**: None (immutable value object, no lifecycle)

**Usage**:
```csharp
// Parsing from functools block
var call = new FunctionCall
{
    Name = "GetWeather",
    Arguments = JsonSerializer.Deserialize<JsonElement>("{\"location\": \"Seattle\"}")
};

// Lookup in registry
if (registry.TryGet(call.Name, out var descriptor))
{
    var result = await invoker.InvokeAsync(call.Name, call.Arguments, ct);
}
```

---

### 2. ToolDescriptor

**Purpose**: Registry entry for a discovered/registered tool (local C# method or MCP-discovered)

**Properties**:

| Name | Type | Nullable | Description | Validation |
|------|------|----------|-------------|------------|
| `Name` | `string` | ❌ | Canonical tool name (case-insensitive lookup) | Unique in registry, matches `^[a-zA-Z][a-zA-Z0-9_]*$` |
| `Source` | `string` | ❌ | Origin of tool | Format: "Local:{TypeName}" or "MCP:{ServerUrl}" or "Generated" |
| `ArgsSchema` | `JsonSchema?` | ✅ | Optional JSON Schema for argument validation | Valid JSON Schema Draft 2020-12 (from JsonSchema.Net) |
| `Invoker` | `Func<JsonElement, ValueTask<ToolResult>>` | ❌ | Async function to execute tool | Non-null delegate, may throw exceptions (caught by dispatcher) |
| `SecurityClass` | `SecurityClass` | ❌ | Classification for allowlist filtering | Enum: Public, Internal, Admin |
| `Timeout` | `TimeSpan` | ❌ | Per-tool timeout override | Must be >0, defaults to 30 seconds if not specified |

**SecurityClass Enum**:
```csharp
public enum SecurityClass
{
    Public = 0,    // Available to all users (e.g., GetWeather)
    Internal = 1,  // Requires authentication (e.g., ReadUserProfile)
    Admin = 2      // Requires admin role (e.g., DeleteAllData)
}
```

**Example**:
```csharp
var descriptor = new ToolDescriptor
{
    Name = "GetWeather",
    Source = "Local:WeatherTools",
    ArgsSchema = JsonSchema.FromText(@"
    {
        ""type"": ""object"",
        ""properties"": {
            ""location"": { ""type"": ""string"", ""maxLength"": 100 },
            ""units"": { ""type"": ""string"", ""enum"": [""metric"", ""imperial""] }
        },
        ""required"": [""location""]
    }"),
    Invoker = async (args) => 
    {
        var location = args.GetProperty("location").GetString();
        var units = args.TryGetProperty("units", out var u) ? u.GetString() : "metric";
        var weather = await weatherService.GetWeatherAsync(location, units);
        return new ToolResult { Content = JsonSerializer.Serialize(weather) };
    },
    SecurityClass = SecurityClass.Public,
    Timeout = TimeSpan.FromSeconds(10)
};
```

**Relationships**:
- **One-to-Many**: Source → Descriptors (e.g., "MCP:weather-api" may expose multiple tools: GetWeather, GetForecast, GetAllergens)
- **Many-to-One**: Descriptors → Registry (all registered tools stored in single `ConcurrentDictionary<string, ToolDescriptor>`)

**State Transitions**:
1. **Unregistered** → (Register() called) → **Registered** (in registry)
2. **Registered** → (Unregister() called) → **Unregistered** (removed from registry)

**Validation Rules**:
- `Name` must be unique in registry (case-insensitive, throws if duplicate)
- `Invoker` must be non-null (throws `ArgumentNullException` if missing)
- `Timeout` must be >0 (throws `ArgumentOutOfRangeException` if ≤0)
- `Source` format validated (must match "Local:*", "MCP:*", or "Generated")

**Usage**:
```csharp
// Registration
var registry = serviceProvider.GetRequiredService<IToolRegistry>();
registry.Register(descriptor);

// Lookup
if (registry.TryGet("GetWeather", out var descriptor))
{
    Console.WriteLine($"Found tool: {descriptor.Name} from {descriptor.Source}");
}
```

---

### 3. ToolResult

**Purpose**: Output of tool invocation returned to model (as `tool` role message)

**Properties**:

| Name | Type | Nullable | Description | Validation |
|------|------|----------|-------------|------------|
| `Name` | `string` | ❌ | Tool name (matches FunctionCall.Name) | Same validation as FunctionCall.Name |
| `Content` | `string?` | ✅ | Successful result content | May be JSON, text, etc.; null if Error is set |
| `Error` | `string?` | ✅ | Error message if invocation failed | Enum values: MALFORMED_BLOCK, UNKNOWN_TOOL, ARG_VALIDATION_FAILED, INVOCATION_FAILED, TIMEOUT, CANCELLED; null if Content is set |
| `Duration` | `TimeSpan?` | ✅ | Actual tool execution time (for telemetry) | Optional, used for metrics tracking |
| `Meta` | `IDictionary<string, string>?` | ✅ | Additional metadata | Key-value pairs: truncation flags, retry counts, etc. |

**Error Codes**:

| Code | Meaning | When Used |
|------|---------|-----------|
| `MALFORMED_BLOCK` | Functools block has invalid JSON or missing fields | Parser detects syntax error |
| `UNKNOWN_TOOL` | Tool name not found in registry | Dispatcher lookup fails |
| `ARG_VALIDATION_FAILED` | Arguments don't match tool schema | JsonSchema validation fails |
| `INVOCATION_FAILED` | Tool threw exception during execution | Invoker catches exception |
| `TIMEOUT` | Tool exceeded configured timeout | CancellationToken triggered |
| `CANCELLED` | User cancelled operation | External cancellation request |

**Example (Success)**:
```csharp
var result = new ToolResult
{
    Name = "GetWeather",
    Content = "{\"temperature\": 12.5, \"conditions\": \"Partly cloudy\"}",
    Duration = TimeSpan.FromMilliseconds(342),
    Meta = new Dictionary<string, string> 
    { 
        ["source"] = "OpenMeteo API",
        ["cached"] = "false"
    }
};
```

**Example (Error)**:
```csharp
var result = new ToolResult
{
    Name = "GetWeather",
    Error = "ARG_VALIDATION_FAILED: location exceeds maxLength (100 chars)",
    Duration = TimeSpan.FromMilliseconds(2)
};
```

**Validation Rules**:
- **Exactly one of `Content` or `Error` must be non-null** (never both, never neither)
- If `Content` is set, `Error` must be null (success case)
- If `Error` is set, `Content` must be null (failure case)
- `Duration` should be set whenever possible (used for P50/P95/P99 metrics)

**State Transitions**:
1. **Pending** (initial state, dispatcher invokes tool) → (tool completes successfully) → **Success** (Content set, Error null)
2. **Pending** → (tool throws exception or validation fails) → **Failed** (Error set, Content null)

**Conversion to ChatMessage**:
```csharp
// Success case
var message = new ChatMessage
{
    Role = ChatRole.Tool,
    Content = result.Content,
    ToolCallId = toolCallId // from original FunctionCall
};

// Error case
var message = new ChatMessage
{
    Role = ChatRole.Tool,
    Content = $"Error: {result.Error}",
    ToolCallId = toolCallId
};
```

**Usage**:
```csharp
// Dispatcher returns result
var result = await invoker.InvokeAsync(call.Name, call.Arguments, ct);

if (result.Error != null)
{
    logger.LogError("Tool {ToolName} failed: {Error}", result.Name, result.Error);
    // Return error to model for retry/fallback
}
else
{
    logger.LogInformation("Tool {ToolName} succeeded in {Duration}ms", 
        result.Name, result.Duration?.TotalMilliseconds);
    // Format as tool message, append to conversation
}
```

## Supporting Types

### ParserException

**Purpose**: Thrown by parser for malformed functools blocks

```csharp
public class ParserException : Exception
{
    public string ErrorCode { get; } // MALFORMED_BLOCK, INCOMPLETE_STREAM
    
    public ParserException(string errorCode, string message) 
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
```

### DispatcherException

**Purpose**: Thrown by dispatcher for validation/invocation errors

```csharp
public class DispatcherException : Exception
{
    public string ErrorCode { get; } // UNKNOWN_TOOL, ARG_VALIDATION_FAILED, TIMEOUT
    
    public DispatcherException(string errorCode, string message) 
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
```

## Data Flow

```
1. Model Response: "The weather is... functools[{\"name\": \"GetWeather\", \"arguments\": {\"location\": \"Seattle\"}}]"
   
2. Parser extracts:
   FunctionCall { Name = "GetWeather", Arguments = {\"location\": \"Seattle\"} }
   
3. Registry lookup:
   registry.TryGet("GetWeather") → ToolDescriptor { Name, Source, ArgsSchema, Invoker, ... }
   
4. Dispatcher validates:
   - Check whitelist (is "GetWeather" allowed?)
   - Validate arguments against ArgsSchema
   - Invoke with timeout
   
5. Invoker executes:
   result = await descriptor.Invoker(call.Arguments)
   
6. Return to model:
   ToolResult { Name = "GetWeather", Content = "{\"temperature\": 12.5}", Duration = 342ms }
   → ChatMessage { Role = Tool, Content = "{\"temperature\": 12.5}" }
   
7. Model continues:
   "Based on the weather data, it's currently 12.5°C and partly cloudy in Seattle."
```

## Persistence

**No persistence required**. All entities are in-memory:
- **FunctionCall**: Transient (parsed from each model response)
- **ToolDescriptor**: In-memory registry (`ConcurrentDictionary`), rebuilt at startup
- **ToolResult**: Transient (returned to model, logged for telemetry)

Configuration (MCP server URLs, tool allowlist) stored in `appsettings.tools.json` and loaded at startup.

## Thread Safety

- **FunctionCall**: Immutable (thread-safe by design)
- **ToolDescriptor**: Immutable after registration (thread-safe by design)
- **ToolResult**: Immutable after creation (thread-safe by design)
- **ToolRegistry**: Uses `ConcurrentDictionary` for thread-safe registration and lookup

## Memory Management

- **FunctionCall**: Small (typically <1KB JSON), short-lived (GC collects after dispatch)
- **ToolDescriptor**: Long-lived (registered at startup, exists for app lifetime)
- **ToolResult**: Small (typically <10KB response), short-lived (GC collects after message formatting)
- **Total Memory**: ~50 tools × ~1KB descriptor = ~50KB registry overhead (negligible)

---

## New Entities: Foundry Native Integration

### 5. AIFunctionAdapter

**Purpose**: Convert `ToolMetadata` to `AIFunction` format expected by Foundry's `{Tool}` placeholder

**Properties**:

| Name | Type | Nullable | Description |
|------|------|----------|-------------|
| `_logger` | `ILogger<AIFunctionAdapter>` | ❌ | Logger for conversion diagnostics |

**Methods**:

```csharp
AIFunction ConvertToAIFunction(ToolMetadata tool);
JsonElement GenerateJsonSchema(IReadOnlyList<ToolParameterMetadata> parameters);
string MapToJsonType(Type dotnetType);
```

**Responsibilities**:

1. Convert `ToolMetadata` → `AIFunction` with JSON Schema
2. Map .NET parameter types → JSON Schema types
3. Mark required parameters in schema
4. Do NOT include execution delegate (we use custom ToolInvoker)

**Example Conversion**:

Input (`ToolMetadata`):

```csharp
new ToolMetadata
{
    Name = "GeocodeLocation",
    Description = "Convert location to coordinates",
    Parameters = new[]
    {
        new ToolParameterMetadata
        {
            Name = "location",
            Type = typeof(string),
            Description = "Location name or address",
            IsRequired = true
        },
        new ToolParameterMetadata
        {
            Name = "count",
            Type = typeof(int),
            Description = "Max results",
            IsRequired = false
        }
    }
}
```

Output (`AIFunction`):

```csharp
new AIFunction
{
    Name = "GeocodeLocation",
    Description = "Convert location to coordinates",
    Metadata = new AIFunctionMetadata
    {
        Name = "GeocodeLocation",
        Description = "Convert location to coordinates",
        JsonSchemaElement = JsonElement.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""location"": {
                    ""type"": ""string"",
                    ""description"": ""Location name or address""
                },
                ""count"": {
                    ""type"": ""number"",
                    ""description"": ""Max results""
                }
            },
            ""required"": [""location""],
            ""additionalProperties"": false
        }")
    }
}
```

**Type Mapping**:

| .NET Type | JSON Schema Type | Notes |
|-----------|------------------|-------|
| `string` | `"string"` | |
| `int`, `long`, `decimal`, `double`, `float` | `"number"` | |
| `bool` | `"boolean"` | |
| `DateTime`, `DateTimeOffset` | `"string"` | Add `format: "date-time"` |
| `Guid` | `"string"` | Add `format: "uuid"` |
| `IEnumerable<T>` | `"array"` | Recursively map `T` for `items` |
| Custom classes | `"object"` | Recursively generate nested schema |

**Validation Rules**:

- Schema must be valid JSON Schema Draft 2020-12
- All required parameters must be in `required` array
- `additionalProperties: false` prevents hallucinated parameters

**State Transitions**: Stateless (pure function)

### 6. ChatOptionsBuilder

**Purpose**: Build `ChatOptions` with all registered tools for each chat request

**Properties**:

| Name | Type | Nullable | Description |
|------|------|----------|-------------|
| `_toolRegistry` | `IToolRegistry` | ❌ | Registry to query for all tools |
| `_adapter` | `IAIFunctionAdapter` | ❌ | Adapter to convert tools to AIFunctions |
| `_logger` | `ILogger<ChatOptionsBuilder>` | ❌ | Logger for build diagnostics |

**Methods**:

```csharp
ChatOptions BuildWithTools();
```

**Responsibilities**:

1. Query `IToolRegistry.GetAllTools()` for discovered tools
2. Convert each `ToolMetadata` to `AIFunction` via adapter
3. Build `ChatOptions` with tools list
4. Log tool count for diagnostics

**Example Usage**:

```csharp
// In Chat.razor
@inject IChatOptionsBuilder ChatOptionsBuilder

private async Task SendMessage()
{
    var options = ChatOptionsBuilder.BuildWithTools();
    // options.Tools contains 5 AIFunctions (if 5 tools registered)
    
    await foreach (var update in ChatClient.GetStreamingResponseAsync(messages, options))
    {
        // FunctoolsChatClient intercepts functools and executes
    }
}
```

**Validation Rules**:

- Must have at least 1 tool registered (warn if empty)
- All tools must convert successfully (log error if conversion fails)
- Tools list must be immutable after build

**State Transitions**: Stateless (builds new ChatOptions each call)

**Performance**: Fast (<5ms for 50 tools) - conversion is cheap, no reflection after initial discovery

## Extensibility

New tool types can be added without modifying core entities:
- **Custom Attribute**: Define `[SpecialTool]` → discovery scans for it → creates `ToolDescriptor`
- **Custom Source**: Add "Database:{ConnectionString}" or "RPC:{Endpoint}" → invoker calls custom backend
- **Custom Validation**: Extend `ArgsSchema` with custom validators (e.g., regex patterns, business rules)

All extensions work through the same `ToolDescriptor` abstraction (zero-code extensibility achieved).
