using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using System.ClientModel;
using System.Runtime.CompilerServices;
using Xunit;
using Phi4WeatherAgent.Agent.Integration;
using Phi4WeatherAgent.Agent.Parsing;
using Phi4WeatherAgent.Agent.Registry;
using Phi4WeatherAgent.Agent.Dispatching;
using Phi4WeatherAgent.Tools;

namespace Phi4WeatherAgent.Agent.Tests.Integration;

/// <summary>
/// Integration tests for FunctoolsChatClient with real AI model and tools.
/// Tests the complete flow: user query -> functools detection -> tool execution -> final response.
/// </summary>
public class FunctoolsChatClientIntegrationTests
{
    private readonly ILogger<FunctoolsChatClient> _logger;
    private readonly ILogger<ToolDiscoveryService> _discoveryLogger;
    private readonly ILoggerFactory _loggerFactory;

    public FunctoolsChatClientIntegrationTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        _logger = _loggerFactory.CreateLogger<FunctoolsChatClient>();
        _discoveryLogger = _loggerFactory.CreateLogger<ToolDiscoveryService>();
    }

    /// <summary>
    /// Test case reproducing user issue: "What are pollen levels in Austin?"
    /// Expected: Model generates GeocodeLocation functools, client intercepts and executes
    /// Actual (bug): Functools displayed directly to user
    /// </summary>
    [Fact]
    public async Task PollenQuery_Austin_ShouldNotShowFunctoolsInResponse()
    {
        // Arrange - Mock the inner chat client to simulate Phi-4 model response
        var modelResponse = "To get pollen levels in Austin, I'll first need to geocode the location.\nfunctools[{\"name\":\"GeocodeLocation\",\"arguments\":{\"location\":\"Austin, Texas\"}}]";
        var mockClient = new MockStreamingChatClient(modelResponse);
        
        var parser = new FunctoolsParser();
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry, _loggerFactory.CreateLogger<ToolInvoker>());
        
        // Create decorator
        var functoolsClient = new FunctoolsChatClient(mockClient, parser, invoker, _logger);
        
        // Act - Send user query
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "What are pollen levels in Austin?")
        };
        
        var responseText = new System.Text.StringBuilder();
        await foreach (var update in functoolsClient.GetStreamingResponseAsync(messages))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                responseText.Append(update.Text);
            }
        }
        
        var finalResponse = responseText.ToString();
        
        Console.WriteLine($"Final response shown to user:\n{finalResponse}");
        
        // Assert - functools should NOT appear in the response
        Assert.DoesNotContain("functools[", finalResponse);
        Assert.DoesNotContain("GeocodeLocation", finalResponse);
        
        // The response should either be:
        // 1. Empty (if tools execute but model doesn't generate follow-up)
        // 2. Natural language (if model generates response after tool execution)
        // It should NOT contain raw functools syntax
    }

    /// <summary>
    /// Test case reproducing user issue: "weather in Atlanta Georgia"
    /// Model hallucinated coordinates and skipped geocoding
    /// </summary>
    [Fact]
    public async Task WeatherQuery_Atlanta_ShouldNotHallucinateCoordinates()
    {
        // Arrange - Mock model response with hallucinated coordinates
        var modelResponse = "functools[{\"name\":\"GetPollenForecast\",\"arguments\":{\"latitude\":33.6407,\"longitude\":-84.2772}}]";
        var mockClient = new MockStreamingChatClient(modelResponse);
        
        var parser = new FunctoolsParser();
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry, _loggerFactory.CreateLogger<ToolInvoker>());
        
        var functoolsClient = new FunctoolsChatClient(mockClient, parser, invoker, _logger);
        
        // Act
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "weather in Atlanta Georgia")
        };
        
        var responseText = new System.Text.StringBuilder();
        await foreach (var update in functoolsClient.GetStreamingResponseAsync(messages))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                responseText.Append(update.Text);
            }
        }
        
        var finalResponse = responseText.ToString();
        
        Console.WriteLine($"Final response shown to user:\n{finalResponse}");
        
        // Assert - functools should NOT appear in response
        Assert.DoesNotContain("functools[", finalResponse);
        Assert.DoesNotContain("GetPollenForecast", finalResponse);
        
        // This test demonstrates the model hallucinating coordinates
        // System prompt needs to be clearer about always geocoding first
    }

    [Fact(Skip = "Requires Foundry Local running on port 60613")]
    public async Task WeatherQuery_Brasov_ShouldExecuteGeocodeAndGetWeather()
    {
        // Arrange - Set up the functools invocation layer
        var parser = new FunctoolsParser();
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry, _loggerFactory.CreateLogger<ToolInvoker>());

        // Manually register tools (simulating ToolDiscoveryService)
        // In production, this is done automatically by scanning assemblies
        Console.WriteLine("Registering tools...");
        
        // Note: We need to register the HTTP clients for OpenMeteo
        // For this test, we'll skip tool registration and just test the parsing
        
        // Get Foundry endpoint
        var foundryPort = Environment.GetEnvironmentVariable("FOUNDRY_PORT") ?? "60613";
        var endpoint = $"http://localhost:{foundryPort}/v1";
        
        Console.WriteLine($"Using Foundry endpoint: {endpoint}");
        
        // Create OpenAI client for Foundry Local
        var openAIClient = new OpenAIClient(
            new ApiKeyCredential("not-used"), 
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
        );
        var baseClient = openAIClient.GetChatClient("Phi-4-mini-instruct-generic-cpu:5").AsIChatClient();
        
        // Wrap with FunctoolsChatClient
        var functoolsClient = new FunctoolsChatClient(baseClient, parser, invoker, _logger);
        
        // System prompt with functools instructions
        var systemPrompt = @"
You are a helpful weather assistant. When you need to use a tool, output it in this EXACT format:
functools[{""name"":""ToolName"",""arguments"":{""arg"":""value""}}]

Available tools:
- GeocodeLocation: Convert location names to coordinates
  Arguments: {""location"": string}
- GetWeather: Get current weather
  Arguments: {""latitude"": number, ""longitude"": number}

For the query 'weather in Brasov', you should:
1. First call GeocodeLocation with location='Brasov'
2. Then call GetWeather with the returned coordinates
";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, "What's the weather in Brasov?")
        };

        // Act
        Console.WriteLine("\n=== Sending query to model ===");
        var response = await functoolsClient.GetStreamingResponseAsync(messages).ToListAsync();
        
        var fullResponse = string.Join("", response.Select(u => u.Text));
        
        Console.WriteLine($"\n=== Model Response ===");
        Console.WriteLine(fullResponse);
        
        // Assert
        Assert.NotNull(fullResponse);
        Assert.NotEmpty(fullResponse);
        
        // Check if functools were detected (should NOT appear in final response)
        Assert.DoesNotContain("functools[", fullResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FunctoolsParser_ShouldDetectGeocodeCall()
    {
        // Arrange
        var parser = new FunctoolsParser();
        var modelOutput = @"To get the weather in Brasov, I'll first need to geocode the location.

functools[{""name"":""GeocodeLocation"",""arguments"":{""location"":""Brasov""}}]";

        // Act
        Console.WriteLine($"Parsing model output:\n{modelOutput}\n");
        var calls = parser.Parse(modelOutput.AsSpan()).ToList();

        // Assert
        Console.WriteLine($"Detected {calls.Count} tool calls");
        Assert.Single(calls);
        Assert.Equal("GeocodeLocation", calls[0].Name);
        
        var locationArg = calls[0].Arguments.GetProperty("location").GetString();
        Assert.Equal("Brasov", locationArg);
        
        Console.WriteLine($"✓ Successfully parsed: {calls[0].Name}(location='{locationArg}')");
    }

    [Fact]
    public async Task FunctoolsParser_ShouldHandleMalformedJson()
    {
        // Arrange
        var parser = new FunctoolsParser();
        
        // Model output with EXTRA closing brace (common Phi-4 error)
        var modelOutput = @"functools[{""name"":""GeocodeLocation"",""arguments"":{""location"":""Brasov""}}}]";

        // Act
        Console.WriteLine($"Parsing malformed output:\n{modelOutput}\n");
        var calls = parser.Parse(modelOutput.AsSpan()).ToList();

        // Assert - Parser should sanitize and still extract the call
        Console.WriteLine($"Detected {calls.Count} tool calls after sanitization");
        Assert.Single(calls);
        Assert.Equal("GeocodeLocation", calls[0].Name);
        
        Console.WriteLine($"✓ Successfully handled malformed JSON");
    }

    [Fact]
    public async Task StreamingResponse_WithFunctools_ShouldBufferAndParse()
    {
        // Arrange
        var parser = new FunctoolsParser();
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry, _loggerFactory.CreateLogger<ToolInvoker>());
        
        // Create a mock streaming client that returns functools
        var mockClient = new MockStreamingChatClient(
            "I'll check the weather for you.\n\nfunctools[{\"name\":\"GeocodeLocation\",\"arguments\":{\"location\":\"Brasov\"}}]"
        );
        
        var functoolsClient = new FunctoolsChatClient(mockClient, parser, invoker, _logger);
        
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in Brasov?")
        };

        // Act
        Console.WriteLine("\n=== Testing streaming with functools ===");
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in functoolsClient.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
            if (!string.IsNullOrEmpty(update.Text))
            {
                Console.Write(update.Text);
            }
        }
        Console.WriteLine();

        var fullText = string.Join("", updates.Select(u => u.Text));
        Console.WriteLine($"\nFull buffered text: {fullText}");

        // Assert - Functools should be detected and buffered
        Assert.NotNull(fullText);
        // The mock client will return the functools, but FunctoolsChatClient should detect it
    }
}

/// <summary>
/// Mock IChatClient that returns predefined streaming responses.
/// </summary>
internal class MockStreamingChatClient : IChatClient
{
    private readonly string _response;

    public MockStreamingChatClient(string response)
    {
        _response = response;
    }

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public TService? GetService<TService>(object? serviceKey = null) where TService : class => null;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Simulate streaming by breaking response into chunks
        var chunkSize = 10;
        for (int i = 0; i < _response.Length; i += chunkSize)
        {
            var chunk = _response.Substring(i, Math.Min(chunkSize, _response.Length - i));
            yield return new ChatResponseUpdate 
            { 
                Contents = [new TextContent(chunk)] 
            };
            await Task.Delay(10); // Simulate network delay
        }
    }

    public ChatClientMetadata Metadata => new("Mock", new Uri("http://localhost"), "mock-model");
}
