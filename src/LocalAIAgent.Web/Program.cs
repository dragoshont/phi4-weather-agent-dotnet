using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OpenAI;
using System.ClientModel;
using LocalAIAgent.Web.Components;
using LocalAIAgent.Agent.Services;
using LocalAIAgent.Agent.Tools;
using LocalAIAgent.Agent.Parsing;
using LocalAIAgent.Agent.Registry;
using LocalAIAgent.Agent.Dispatching;
using LocalAIAgent.Agent.Integration;
using LocalAIAgent.Agent.Telemetry;
using LocalAIAgent.Agent.Interfaces;
using LocalAIAgent.Agent.Handlers;
using LocalAIAgent.Agent.Models;
using LocalAIAgent.Agent.Adapters;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (Aspire telemetry, health checks, resilience)
builder.AddServiceDefaults();

// Configure OpenTelemetry for functools invocation layer
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(ActivitySources.AgentSourceName + ".Parsing")
        .AddSource(ActivitySources.AgentSourceName + ".Validation")
        .AddSource(ActivitySources.AgentSourceName + ".Dispatch")
        .AddSource(ActivitySources.AgentSourceName + ".Execution"))
    .WithMetrics(metrics => metrics
        .AddMeter("LocalAIAgent.Agent"));

// Blazor Server
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Configure AI settings with Options pattern (T010)
builder.Services.Configure<AIConfiguration>(builder.Configuration.GetSection("AI"));

// Register OpenMeteo HTTP clients (used by [Tool] decorated methods in LocalAIAgent.Tools)
builder.Services.AddOpenMeteoClients(builder.Configuration);

// T132-T136: Register functools invocation layer services
builder.Services.AddSingleton<IFunctoolsParser, FunctoolsParser>();
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddSingleton<IToolInvoker, ToolInvoker>();
builder.Services.AddHostedService<ToolDiscoveryService>();

// T212-T213: Register Foundry native integration services
builder.Services.AddSingleton<LocalAIAgent.Agent.Adapters.IAIFunctionAdapter, LocalAIAgent.Agent.Adapters.AIFunctionAdapter>();
builder.Services.AddSingleton<LocalAIAgent.Agent.Adapters.IChatOptionsBuilder, LocalAIAgent.Agent.Adapters.ChatOptionsBuilder>();

// Register Agent Framework dependencies (T045)
builder.Services.AddSingleton<LocalAIAgent.Agent.Services.ConfigurationProvider>();
builder.Services.AddHostedService<LocalAIAgent.Agent.Services.ConfigurationValidator>();
builder.Services.AddSingleton<IPromptProvider, PromptProvider>();
builder.Services.AddKeyedSingleton<IToolInvocationHandler, FunctoolsHandler>("Functools");

// Register tools (T048-T051)
builder.Services.AddSingleton<GeocodeTool>();
builder.Services.AddSingleton<WeatherTool>();
builder.Services.AddSingleton<AllergenTool>();

// T212: Foundry version check (minimum 0.8.103 required for native function calling)
// NOTE: This is commented out for now since Foundry version detection requires process execution
// which may not be available in all deployment environments (e.g., containers without Foundry CLI)
// TODO: Implement version check using foundry --version if needed
// if (!VerifyFoundryVersion())
// {
//     throw new NotSupportedException("Foundry 0.8.103+ required for native function calling");
// }

// Configure IChatClient with platform-specific AI provider (T034)
// Platform detection from AppHost: Foundry Local (Windows/macOS) vs Ollama (Linux)
// AI Model Endpoint Configuration
var aiModelEndpoint = builder.Configuration["AI_MODEL_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AI_MODEL_ENDPOINT")
    ?? (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? $"http://localhost:{Environment.GetEnvironmentVariable("FOUNDRY_PORT") ?? "63336"}/v1"
        : "http://localhost:11434");

// T138: Register base IChatClient
// Platform-specific client selection:
// - Windows/macOS: Use OpenAI client for Foundry Local (OpenAI-compatible)
// - Linux: Use Ollama client for Ollama container
builder.Services.AddSingleton<IChatClient>(sp =>
{
    IChatClient baseClient;

    if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
    {
        // Foundry Local - OpenAI-compatible API with Phi-4 mini
        var modelId = "Phi-4-mini-instruct-generic-cpu:5";
        Console.WriteLine($"Using OpenAI client with Phi-4 model: {modelId}");
        Console.WriteLine($"Endpoint: {aiModelEndpoint}");

        var openAIClient = new OpenAIClient(new ApiKeyCredential("not-used"), new OpenAIClientOptions
        {
            Endpoint = new Uri(aiModelEndpoint)
        });
        baseClient = openAIClient.GetChatClient(modelId).AsIChatClient();
    }
    else
    {
        // Ollama for Linux with Mistral
        var modelId = "mistral:7b-instruct";
        Console.WriteLine($"Using Ollama client with Mistral model: {modelId}");
        baseClient = new OllamaChatClient(new Uri(aiModelEndpoint), modelId);
    }

    // Note: ChatClientAgent will handle tool invocation internally
    // No need to wrap with handler here

    return baseClient;
});// T045: Register ChatClientAgent with Agent Framework
builder.Services.AddSingleton<ChatClientAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var promptProvider = sp.GetRequiredService<IPromptProvider>();
    var toolRegistry = sp.GetRequiredService<IToolRegistry>();
    var aiFunctionAdapter = sp.GetRequiredService<IAIFunctionAdapter>();
    var logger = sp.GetRequiredService<ILogger<ChatClientAgent>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    logger.LogInformation("Creating ChatClientAgent with Agent Framework");

    // Use a simple default instruction that will be overridden by ChatAgentService
    // This avoids blocking async file I/O during DI container initialization
    var defaultInstructions = "You are a helpful weather assistant.";

    // Get all discovered tools from registry and convert to AITool declarations
    var discoveredTools = new List<AITool>();
    var enumerator = toolRegistry.ListAsync().GetAsyncEnumerator();
    try
    {
        while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
        {
            var toolDescriptor = enumerator.Current;
            var aiFunction = aiFunctionAdapter.ConvertToAIFunction(toolDescriptor);
            discoveredTools.Add(aiFunction);
        }
    }
    finally
    {
        enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    logger.LogInformation("Loaded {ToolCount} tools from registry", discoveredTools.Count);

    // Create ChatClientAgent with dynamically discovered tools
    var agent = new ChatClientAgent(
        chatClient: chatClient,
        instructions: defaultInstructions,
        name: "WeatherAssistant",
        description: "AI assistant for weather forecasts and allergen information",
        tools: discoveredTools,
        loggerFactory: loggerFactory,
        services: sp
    );

    logger.LogInformation("ChatClientAgent created successfully with {ToolCount} tools", discoveredTools.Count);

    return agent;
});

// T045: Register ChatAgentService for Blazor components
// Changed to Singleton to avoid blocking during WebSocket connection setup
builder.Services.AddSingleton<ChatAgentService>();

var app = builder.Build();

// Test endpoint to verify AI Agent connection
app.MapGet("/test-ai", async (ChatAgentService agentService) =>
{
    try
    {
        var thread = agentService.CreateThread();
        var response = await agentService.RunAsync("Say 'Hello from Weather Assistant!'", thread);
        return Results.Ok(new { success = true, response = response, hasThread = thread != null });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { success = false, error = ex.Message, details = ex.ToString() });
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();

// Map Aspire health endpoints
app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Make Program accessible to WebApplicationFactory for integration testing
public partial class Program { }
