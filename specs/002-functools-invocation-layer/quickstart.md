# Quickstart: Add Your First Custom Tool

**Goal**: Create a simple "Hello World" tool and verify Phi-4-mini can invoke it through the functools invocation layer.

**Time to Complete**: ~10 minutes

---

## Prerequisites

Before starting, ensure you have:

- ✅ .NET 10 SDK (10.0.100+) installed
- ✅ Foundry Local service running (`foundry service status` should show "Running")
- ✅ Aspire workload installed (`dotnet workload list` should show `aspire`)
- ✅ Phi-4-mini model downloaded (`foundry model list | Select-String "Phi-4-mini"`)
- ✅ Project repository cloned and at root directory

---

## Step 1: Create the Tool Class

Create a new file `src/Phi4WeatherAgent.Tools/HelloWorldTools.cs`:

```csharp
using Phi4WeatherAgent.Tools;

namespace Phi4WeatherAgent.Tools;

/// <summary>
/// Example tools for quickstart demonstration.
/// </summary>
public class HelloWorldTools
{
    /// <summary>
    /// Says hello to a person by name.
    /// </summary>
    /// <param name="name">Name of the person to greet</param>
    /// <returns>Personalized greeting message</returns>
    [Tool("SayHello", 
        Description = "Greets a person by name", 
        InputSchemaJson = @"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": { 
                    ""type"": ""string"", 
                    ""description"": ""Name of person to greet"",
                    ""maxLength"": 50
                }
            },
            ""required"": [""name""]
        }")]
    public string SayHello(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty", nameof(name));
        }
        
        return $"Hello, {name}! Welcome to the Phi-4 Weather Assistant with custom tool support!";
    }
    
    /// <summary>
    /// Adds two numbers together.
    /// Demonstrates tools can perform computations.
    /// </summary>
    [Tool("Add",
        Description = "Adds two numbers",
        InputSchemaJson = @"{
            ""type"": ""object"",
            ""properties"": {
                ""a"": { ""type"": ""number"", ""description"": ""First number"" },
                ""b"": { ""type"": ""number"", ""description"": ""Second number"" }
            },
            ""required"": [""a"", ""b""]
        }")]
    public int Add(int a, int b)
    {
        return a + b;
    }
}
```

**What's happening here?**

- The `[Tool("SayHello")]` attribute marks the method for automatic discovery
- `InputSchemaJson` provides JSON Schema for argument validation (optional but recommended)
- Method parameters are automatically mapped from JSON arguments
- Return value is serialized and sent back to the model

---

## Step 2: Restart the Application

The invocation layer automatically discovers tools at startup. Restart Aspire AppHost:

```powershell
# Stop any running instances
Stop-Process -Name "Phi4WeatherAgent*","dotnet" -Force -ErrorAction SilentlyContinue

# Start Aspire AppHost
.\Start-AspireHost.ps1
```

**Expected Output**:
```
[12:34:56] info: Phi4WeatherAgent.Agent.Registry.ToolRegistry[0]
      Discovered 3 tools via reflection:
      - GetWeather (Local:WeatherTools)
      - GeocodeLocation (Local:GeocodingTools)
      - SayHello (Local:HelloWorldTools)  ← Your new tool!
      
[12:34:56] info: Phi4WeatherAgent.AppHost.Program[0]
      Aspire Dashboard: http://localhost:15888
```

---

## Step 3: Test the Tool

Open the Blazor UI at `http://localhost:52636` and send a prompt:

```
Say hello to Alice
```

**Expected Behavior**:

1. **Model Responds** with functools block:
   ```
   functools[{"name": "SayHello", "arguments": {"name": "Alice"}}]
   ```

2. **Parser Extracts** FunctionCall:
   ```csharp
   FunctionCall { Name = "SayHello", Arguments = {"name": "Alice"} }
   ```

3. **Registry Lookup** finds ToolDescriptor:
   ```csharp
   ToolDescriptor { Name = "SayHello", Source = "Local:HelloWorldTools", ... }
   ```

4. **Dispatcher Validates** arguments (matches schema: `name` is string, <50 chars)

5. **Invoker Executes** tool:
   ```csharp
   HelloWorldTools.SayHello("Alice") 
   → "Hello, Alice! Welcome to the Phi-4 Weather Assistant with custom tool support!"
   ```

6. **ToolResult Returned** to model:
   ```csharp
   ToolResult { 
       Name = "SayHello", 
       Content = "Hello, Alice! Welcome to the Phi-4 Weather Assistant with custom tool support!",
       Duration = 2ms
   }
   ```

7. **Model Final Response**:
   ```
   Hello, Alice! Welcome to the Phi-4 Weather Assistant with custom tool support!
   ```

---

## Step 4: Verify Telemetry

Open Aspire Dashboard at `http://localhost:15888/traces`:

**Expected Trace** (expand "Chat Completion" trace):

```
Chat Completion [250ms]
├── Parse functools block [3ms]
│   └── FunctionCall: SayHello
├── Registry lookup [<1μs]
│   └── Found: SayHello (Local:HelloWorldTools)
├── Validate arguments [1ms]
│   └── JSON Schema validation: PASS
├── Invoke tool [2ms]
│   └── HelloWorldTools.SayHello("Alice") → Success
└── Format tool message [<1ms]
    └── ChatMessage { Role = Tool, Content = "Hello, Alice! ..." }
```

**Metrics** (check "Metrics" tab):
- `tool.invocation.duration`: Histogram showing 2ms execution
- `tool.invocation.count`: Counter incremented to 1
- `tool.validation.duration`: Histogram showing 1ms validation

---

## Step 5: Test Error Handling

Send a prompt with invalid arguments:

```
Say hello to a person with an extremely long name that exceeds the 50 character limit defined in the JSON schema for this tool
```

**Expected Behavior**:

1. Model outputs functools with long name (>50 chars)
2. Dispatcher validates arguments → **FAILS** (exceeds `maxLength: 50`)
3. ToolResult returned with error:
   ```csharp
   ToolResult { 
       Name = "SayHello",
       Error = "ARG_VALIDATION_FAILED: name exceeds maxLength (50 chars)",
       Duration = 1ms
   }
   ```
4. Model receives error message and responds:
   ```
   I apologize, but I couldn't process that request because the name provided 
   exceeds the maximum allowed length. Please provide a shorter name.
   ```

---

## Next Steps

### Add More Tools

Create additional tool methods in `HelloWorldTools.cs` or new tool classes:

```csharp
[Tool("GetCurrentTime")]
public string GetCurrentTime()
{
    return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
}

[Tool("GenerateRandomNumber")]
public int GenerateRandomNumber(int min, int max)
{
    return Random.Shared.Next(min, max);
}
```

### Connect MCP Server

Add MCP server URL to `appsettings.tools.json`:

```json
{
  "mcpServers": [
    {
      "url": "http://localhost:3000/mcp",
      "name": "WeatherAPI",
      "authentication": {
        "type": "bearer",
        "token": "your-api-key-here"
      }
    }
  ]
}
```

Tools from MCP server will be discovered automatically at startup (zero code changes needed).

### Explore Telemetry

1. **Traces**: View detailed execution flow in Aspire Dashboard
2. **Metrics**: Track tool performance (P50/P95/P99 latency)
3. **Logs**: Search for `ToolInvoker` or `ToolRegistry` to see tool execution logs

### Performance Tuning

- **Schema Validation**: Remove `InputSchemaJson` for faster invocation (<5ms → <2ms)
- **Timeout**: Set custom timeout in `[Tool]` attribute: `Timeout = 5000` (5 seconds)
- **Caching**: Cache tool results in tool method (e.g., weather data for 5 minutes)

---

## Troubleshooting

### Tool Not Discovered

**Symptom**: Model responds "Tool not found: SayHello"

**Solution**:
1. Verify `[Tool]` attribute is present and name matches
2. Check tool class is public and method is public
3. Restart AppHost (tools discovered at startup only)
4. Check logs for discovery errors: `grep "ToolRegistry" *.log`

### Validation Always Fails

**Symptom**: All tool calls rejected with `ARG_VALIDATION_FAILED`

**Solution**:
1. Verify JSON Schema is valid (use https://www.jsonschemavalidator.net/)
2. Check argument names match schema properties exactly (case-sensitive)
3. Test validation independently:
   ```csharp
   var result = await invoker.ValidateAsync("SayHello", args);
   Console.WriteLine($"Valid: {result.IsValid}, Errors: {string.Join(", ", result.Errors)}");
   ```

### Tool Execution Timeout

**Symptom**: Tool returns `Error = "TIMEOUT"`

**Solution**:
1. Increase timeout in `[Tool]` attribute: `Timeout = 60000` (60 seconds)
2. Optimize tool logic (reduce HTTP calls, database queries)
3. Check for deadlocks (use async/await properly, avoid `.Result` or `.Wait()`)

---

## Summary

You've successfully:

✅ Created a custom tool with `[Tool]` attribute  
✅ Verified automatic discovery at startup  
✅ Tested tool invocation via Phi-4-mini  
✅ Inspected telemetry in Aspire Dashboard  
✅ Handled validation errors gracefully

**Zero code changes to parser or dispatcher were needed** — this is zero-code extensibility in action!

**Next Command**: `/speckit.tasks` to generate implementation tasks for building the invocation layer.
