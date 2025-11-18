using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Phi4WeatherAgent.Web.Components;
using Phi4WeatherAgent.Agent.Services;
using Phi4WeatherAgent.Agent.Tools;
using Phi4WeatherAgent.Agent.Parsing;
using Phi4WeatherAgent.Agent.Registry;
using Phi4WeatherAgent.Agent.Dispatching;
using Phi4WeatherAgent.Agent.Integration;
using Phi4WeatherAgent.Agent.Telemetry;
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
        .AddMeter("Phi4WeatherAgent.Agent"));

// Blazor Server
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Register OpenMeteo HTTP clients (used by [Tool] decorated methods in Phi4WeatherAgent.Tools)
builder.Services.AddOpenMeteoClients(builder.Configuration);

// T132-T136: Register functools invocation layer services
builder.Services.AddSingleton<IFunctoolsParser, FunctoolsParser>();
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddSingleton<IToolInvoker, ToolInvoker>();
builder.Services.AddHostedService<ToolDiscoveryService>();

// T212-T213: Register Foundry native integration services
builder.Services.AddSingleton<Phi4WeatherAgent.Agent.Adapters.IAIFunctionAdapter, Phi4WeatherAgent.Agent.Adapters.AIFunctionAdapter>();
builder.Services.AddSingleton<Phi4WeatherAgent.Agent.Adapters.IChatOptionsBuilder, Phi4WeatherAgent.Agent.Adapters.ChatOptionsBuilder>();

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

// T138: Register base IChatClient and wrap with FunctoolsChatClient decorator
builder.Services.AddChatClient(services =>
{
    // Platform-specific client selection:
    // - Windows/macOS: Use OpenAI client for Foundry Local (OpenAI-compatible)
    // - Linux: Use Ollama client for Ollama container
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

    // Wrap base client with FunctoolsChatClient decorator to enable custom functools parsing
    // NOTE: Phi-4-mini does NOT support native tool calling, so we use custom functools format
    var parser = services.GetRequiredService<IFunctoolsParser>();
    var invoker = services.GetRequiredService<IToolInvoker>();
    var logger = services.GetRequiredService<ILogger<FunctoolsChatClient>>();
    
    return new FunctoolsChatClient(baseClient, parser, invoker, logger);
});

var app = builder.Build();

// Test endpoint to verify AI connection
app.MapGet("/test-ai", async (IChatClient chatClient) =>
{
    try
    {
        var response = await chatClient.GetResponseAsync("Say 'Hello from Mistral!'");
        return Results.Ok(new { success = true, response = response.ToString(), messageCount = response.Messages.Count });
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
