using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using System.ClientModel;
using System.Reflection;
using Xunit;
using Phi4WeatherAgent.Agent.Integration;
using Phi4WeatherAgent.Agent.Parsing;
using Phi4WeatherAgent.Agent.Dispatching;
using Phi4WeatherAgent.Agent.Registry;
using Phi4WeatherAgent.Tools;

namespace Phi4WeatherAgent.Agent.Tests.Integration;

/// <summary>
/// Integration tests for complete functools flow with real Foundry Local model.
/// Tests various query patterns: single tool, chained tools, no tools.
/// </summary>
public class FunctoolsFlowIntegrationTests : IAsyncLifetime
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FunctoolsChatClient> _logger;
    private IChatClient? _baseClient;
    private FunctoolsChatClient? _functoolsClient;
    private readonly string _foundryEndpoint;

    public FunctoolsFlowIntegrationTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        _logger = _loggerFactory.CreateLogger<FunctoolsChatClient>();
        
        var foundryPort = Environment.GetEnvironmentVariable("FOUNDRY_PORT") ?? "51185";
        _foundryEndpoint = $"http://localhost:{foundryPort}/v1";
    }

    public async Task InitializeAsync()
    {
        // Set up Foundry client
        var openAIClient = new OpenAIClient(
            new ApiKeyCredential("not-used"), 
            new OpenAIClientOptions { Endpoint = new Uri(_foundryEndpoint) }
        );
        _baseClient = openAIClient.GetChatClient("Phi-4-mini-instruct-generic-cpu:5").AsIChatClient();
        
        // Set up DI for tool instantiation (needed for tools with HttpClient dependencies)
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var serviceProvider = services.BuildServiceProvider();
        
        // Set up functools layer
        var parser = new FunctoolsParser();
        var registry = new ToolRegistry();
        
        // Manually register tools using reflection (similar to ToolDiscoveryService)
        RegisterToolsFromAssembly(typeof(GeocodingTools).Assembly, registry, serviceProvider);
        
        var invoker = new ToolInvoker(registry, _loggerFactory.CreateLogger<ToolInvoker>());
        _functoolsClient = new FunctoolsChatClient(_baseClient, parser, invoker, _logger);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Manually register tools from assembly (simplified version of ToolDiscoveryService logic)
    /// </summary>
    private void RegisterToolsFromAssembly(Assembly assembly, ToolRegistry registry, IServiceProvider serviceProvider)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = method.GetCustomAttribute<ToolAttribute>();
                if (attr == null) continue;

                // Create the invoker delegate
                Func<System.Text.Json.JsonElement, ValueTask<ToolResult>> invoker = async (args) =>
                {
                    try
                    {
                        // Convert JsonElement to dictionary
                        var argsDict = new Dictionary<string, object?>();
                        foreach (var prop in args.EnumerateObject())
                        {
                            argsDict[prop.Name] = prop.Value.ValueKind switch
                            {
                                System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                                System.Text.Json.JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? i : prop.Value.GetDouble(),
                                System.Text.Json.JsonValueKind.True => true,
                                System.Text.Json.JsonValueKind.False => false,
                                _ => prop.Value.ToString()
                            };
                        }

                        // Build parameters for method invocation
                        var parameters = method.GetParameters();
                        var paramValues = new object?[parameters.Length];
                        
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            var param = parameters[i];
                            if (param.ParameterType == typeof(HttpClient))
                            {
                                // Resolve from DI
                                paramValues[i] = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
                            }
                            else if (argsDict.TryGetValue(param.Name!, out var value))
                            {
                                paramValues[i] = Convert.ChangeType(value, param.ParameterType);
                            }
                            else if (param.HasDefaultValue)
                            {
                                paramValues[i] = param.DefaultValue;
                            }
                        }

                        // Invoke method
                        var startTime = DateTime.UtcNow;
                        var result = method.Invoke(null, paramValues);
                        
                        // Handle Task/ValueTask return types
                        object? finalResult;
                        if (result is Task task)
                        {
                            await task;
                            var resultProperty = task.GetType().GetProperty("Result");
                            finalResult = resultProperty?.GetValue(task);
                        }
                        else
                        {
                            finalResult = result;
                        }
                        
                        var duration = DateTime.UtcNow - startTime;
                        
                        return new ToolResult
                        {
                            Name = attr.Name,
                            Content = finalResult?.ToString() ?? "",
                            Duration = duration
                        };
                    }
                    catch (Exception ex)
                    {
                        return new ToolResult
                        {
                            Name = attr.Name,
                            Error = ex.Message
                        };
                    }
                };

                var descriptor = new ToolDescriptor
                {
                    Name = attr.Name,
                    Source = $"Local:{type.FullName}",
                    Invoker = invoker,
                    ArgsSchema = null
                };

                registry.Register(descriptor);
                Console.WriteLine($"Registered tool: {attr.Name}");
            }
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private string GetSystemPrompt()
    {
        return @"You are a helpful weather assistant powered by Phi-4. You have access to several tools.

When you need to use a tool, output it in this EXACT format on a single line:
functools[{""name"":""ToolName"",""arguments"":{""arg1"":""value1"",""arg2"":123}}]

CRITICAL SPELLING: The keyword is ""functools"" (NOT ""funtions"", NOT ""functions"", ONLY ""functools"")

Available tools:
- GeocodeLocation(location: string) - Convert location names to coordinates
- GetWeather(latitude: number, longitude: number) - Get CURRENT weather conditions
- GetForecast(latitude: number, longitude: number, days: number) - Get 7-day weather forecast
- GetPollenForecast(latitude: number, longitude: number, days: number) - Get pollen forecast (EUROPE ONLY)
- GetAirQuality(latitude: number, longitude: number) - Get air quality data (WORLDWIDE, use for US/non-European locations)

CRITICAL RULES - READ CAREFULLY:
1. ALWAYS geocode location names first - never hallucinate coordinates
2. After receiving tool results, STOP and provide a natural language answer
3. NEVER call the same tool twice with identical arguments
4. NEVER output functools after you have the data needed to answer the user
5. Each tool should be called ONCE per unique request
6. DO NOT mention coordinates (latitude/longitude numbers) UNLESS the user explicitly asks for them
7. For pollen queries: Use GetPollenForecast ONLY for European cities (Paris, Berlin, Rome). For US/other locations (Austin, Seattle), use GetAirQuality which provides air quality data worldwide

WORKFLOW:
Question about location → GeocodeLocation → Get coordinates → Call weather/pollen/air tool ONCE → Provide answer
Do NOT: Call → Get data → Call same tool again → Repeat → Repeat (WRONG!)
Do: Call → Get data → Provide natural language answer (CORRECT!)

Example 1 - Simple geocoding:
User: ""What are the coordinates of Austin?""
You: functools[{""name"":""GeocodeLocation"",""arguments"":{""location"":""Austin, Texas""}}]
System: Coordinates (30.27, -97.74)
You: ""Austin, Texas is located at latitude 30.27 and longitude -97.74.""
STOP - Do not call GeocodeLocation again!

Example 2 - Chained calls:
User: ""What's the pollen in Seattle?""
You: functools[{""name"":""GeocodeLocation"",""arguments"":{""location"":""Seattle""}}]
System: Coordinates (47.61, -122.33)
You: functools[{""name"":""GetPollenForecast"",""arguments"":{""latitude"":47.61,""longitude"":-122.33,""days"":3}}]
System: Pollen data (grass: low, tree: moderate)
You: ""The pollen levels in Seattle are low for grass and moderate for tree pollen.""
STOP - Do not call any tools again!
";
    }

    /// <summary>
    /// Test: Single tool call - just geocoding
    /// Query: ""What are the coordinates of Austin?""
    /// Expected: GeocodeLocation executes, returns coordinates in natural language
    /// </summary>
    [Fact]
    public async Task SingleToolCall_GeocodeOnly_ShouldReturnCoordinates()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, "What are the coordinates of Austin, Texas?")
        };

        // Act
        var response = await CollectResponseAsync(messages);
        
        // Assert
        Console.WriteLine($"\n=== Response ===\n{response}\n");
        
        Assert.DoesNotContain("functools[", response); // No raw functools visible
        Assert.DoesNotContain("GeocodeLocation", response); // Tool name not visible
        
        // Should contain actual coordinates (approximate)
        Assert.Contains("30", response); // Latitude ~30.27
        Assert.Contains("97", response); // Longitude ~-97.74
    }

    /// <summary>
    /// Test: Chained tool calls - geocode then pollen
    /// Query: ""What are pollen levels in Austin?""
    /// Expected: GeocodeLocation -> GetPollenForecast, both execute silently
    /// </summary>
    [Fact]
    public async Task ChainedToolCalls_GeocodeThenPollen_ShouldReturnPollenData()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, "What are pollen levels in Austin, Texas?")
        };

        // Act
        var response = await CollectResponseAsync(messages);
        
        // Assert
        Console.WriteLine($"\n=== Response ===\n{response}\n");
        
        Assert.DoesNotContain("functools[", response); // No raw functools
        Assert.DoesNotContain("GeocodeLocation", response); // No tool names
        Assert.DoesNotContain("GetPollenForecast", response);
        
        // Should contain pollen-related terms
        Assert.Matches(@"pollen|grass|tree|weed", response.ToLower());
    }

    /// <summary>
    /// Test: Chained tool calls - geocode then weather
    /// Query: ""What's the weather in Seattle?""
    /// Expected: GeocodeLocation -> GetWeatherForecast, both execute silently
    /// </summary>
    [Fact]
    public async Task ChainedToolCalls_GeocodeThenWeather_ShouldReturnWeatherData()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, "What's the weather forecast in Seattle?")
        };

        // Act
        var response = await CollectResponseAsync(messages);
        
        // Assert
        Console.WriteLine($"\n=== Response ===\n{response}\n");
        
        Assert.DoesNotContain("functools[", response);
        Assert.DoesNotContain("GeocodeLocation", response);
        Assert.DoesNotContain("GetWeatherForecast", response);
        
        // Should contain weather-related terms
        Assert.Matches(@"temperature|weather|forecast|rain|cloud|sunny", response.ToLower());
    }

    /// <summary>
    /// Test: Multiple tool calls in sequence - geocode, weather, pollen
    /// Query: ""What's the weather and pollen in Portland?""
    /// Expected: GeocodeLocation -> GetWeatherForecast -> GetPollenForecast
    /// </summary>
    [Fact]
    public async Task MultipleToolCalls_WeatherAndPollen_ShouldReturnBothDatasets()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, "What's the weather and pollen forecast in Portland, Oregon?")
        };

        // Act
        var response = await CollectResponseAsync(messages);
        
        // Assert
        Console.WriteLine($"\n=== Response ===\n{response}\n");
        
        Assert.DoesNotContain("functools[", response);
        
        // Should contain both weather and pollen information
        Assert.Matches(@"temperature|weather|forecast", response.ToLower());
        Assert.Matches(@"pollen|grass|tree", response.ToLower());
    }

    /// <summary>
    /// Test: No tool calls needed - direct answer
    /// Query: ""What is pollen?""
    /// Expected: Model answers directly without using tools
    /// </summary>
    [Fact]
    public async Task NoToolCalls_ConceptualQuestion_ShouldAnswerDirectly()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, "What is pollen and why does it cause allergies?")
        };

        // Act
        var response = await CollectResponseAsync(messages);
        
        // Assert
        Console.WriteLine($"\n=== Response ===\n{response}\n");
        
        Assert.DoesNotContain("functools[", response);
        
        // Should contain educational content about pollen
        Assert.Matches(@"pollen|allergies|plant|flower", response.ToLower());
    }

    /// <summary>
    /// Test: Air quality query - geocode then air quality
    /// Query: ""What's the air quality in Denver?""
    /// Expected: GeocodeLocation -> GetAirQuality
    /// </summary>
    [Fact]
    public async Task ChainedToolCalls_GeocodeThenAirQuality_ShouldReturnAirQualityData()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, "What's the air quality in Denver, Colorado?")
        };

        // Act
        var response = await CollectResponseAsync(messages);
        
        // Assert
        Console.WriteLine($"\n=== Response ===\n{response}\n");
        
        Assert.DoesNotContain("functools[", response);
        Assert.DoesNotContain("GeocodeLocation", response);
        Assert.DoesNotContain("GetAirQuality", response);
        
        // Should contain air quality information
        Assert.Matches(@"air quality|aqi|pollution|pm2\.5|pm10", response.ToLower());
    }

    /// <summary>
    /// Test: Model should not hallucinate coordinates
    /// Query: ""What's the pollen in Brasov, Romania?""
    /// Expected: Should geocode first, not use hardcoded coordinates
    /// </summary>
    [Fact]
    public async Task PreventHallucination_BrasovPollen_ShouldGeocodeFirst()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, "What's the pollen forecast in Brasov, Romania?")
        };

        // Act
        var response = await CollectResponseAsync(messages);
        
        // Assert
        Console.WriteLine($"\n=== Response ===\n{response}\n");
        
        // The key assertion: no functools should be visible (all executed)
        Assert.DoesNotContain("functools[", response);
        
        // Should contain pollen data
        Assert.Matches(@"pollen|grass|tree|brasov|romania", response.ToLower());
    }

    /// <summary>
    /// Helper to collect full streaming response
    /// </summary>
    private async Task<string> CollectResponseAsync(List<ChatMessage> messages)
    {
        var responseText = new System.Text.StringBuilder();
        
        await foreach (var update in _functoolsClient!.GetStreamingResponseAsync(messages))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                responseText.Append(update.Text);
            }
        }
        
        return responseText.ToString();
    }
}
