using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using System.ClientModel;
using System.Reflection;
using Xunit;
using LocalAIAgent.Agent.Integration;
using LocalAIAgent.Agent.Parsing;
using LocalAIAgent.Agent.Dispatching;
using LocalAIAgent.Agent.Registry;
using LocalAIAgent.Tools;

namespace LocalAIAgent.Agent.Tests.Integration;

/// <summary>
/// Diagnostic test to see exactly what's happening with tool results
/// </summary>
public class FunctoolsDiagnosticTests : IAsyncLifetime
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FunctoolsChatClient> _logger;
    private IChatClient? _baseClient;
    private FunctoolsChatClient? _functoolsClient;
    private readonly string _foundryEndpoint;
    private readonly List<string> _conversationLog = new();

    public FunctoolsDiagnosticTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
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

        // Set up DI for tool instantiation
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var serviceProvider = services.BuildServiceProvider();

        // Set up functools layer
        var parser = new FunctoolsParser();
        var registry = new ToolRegistry();

        // Register tools
        RegisterToolsFromAssembly(typeof(GeocodingTools).Assembly, registry, serviceProvider);

        var invoker = new ToolInvoker(registry, _loggerFactory.CreateLogger<ToolInvoker>());
        _functoolsClient = new FunctoolsChatClient(_baseClient, parser, invoker, _logger);

        await Task.CompletedTask;
    }

    private void RegisterToolsFromAssembly(Assembly assembly, ToolRegistry registry, IServiceProvider serviceProvider)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = method.GetCustomAttribute<ToolAttribute>();
                if (attr == null) continue;

                Func<System.Text.Json.JsonElement, ValueTask<ToolResult>> invoker = async (args) =>
                {
                    try
                    {
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

                        var parameters = method.GetParameters();
                        var paramValues = new object?[parameters.Length];

                        for (int i = 0; i < parameters.Length; i++)
                        {
                            var param = parameters[i];
                            if (param.ParameterType == typeof(HttpClient))
                            {
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

                        var startTime = DateTime.UtcNow;
                        var result = method.Invoke(null, paramValues);

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
            }
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Diagnostic test: See exactly what happens with geocode query
    /// </summary>
    [Fact]
    public async Task Diagnostic_GeocodeOnly_LogFullConversation()
    {
        // Arrange
        var systemPrompt = File.ReadAllText("c:\\src\\phi4-weather-agent-dotnet\\prompts\\weather-assistant.md");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, "What are the coordinates of Austin, Texas?")
        };

        Console.WriteLine("=== SYSTEM PROMPT ===");
        Console.WriteLine(systemPrompt);
        Console.WriteLine("\n=== USER QUERY ===");
        Console.WriteLine("What are the coordinates of Austin, Texas?");
        Console.WriteLine("\n=== CONVERSATION FLOW ===\n");

        // Act - collect response and log all messages
        var responseText = new System.Text.StringBuilder();

        await foreach (var update in _functoolsClient!.GetStreamingResponseAsync(messages))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                responseText.Append(update.Text);
            }
        }

        Console.WriteLine("\n=== FINAL RESPONSE ===");
        Console.WriteLine(responseText.ToString());
        Console.WriteLine("\n===================\n");

        // Now manually call GeocodeLocation to see what it actually returns
        Console.WriteLine("=== DIRECT TOOL CALL TEST ===");
        var geocodeResult = await GeocodingTools.GeocodeLocation("Austin, Texas");
        Console.WriteLine($"Direct GeocodeLocation result: {geocodeResult}");
        Console.WriteLine("=============================\n");

        // Assert - just log everything, no assertions
        Assert.True(true, "Diagnostic test - check console output");
    }
}
