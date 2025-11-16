using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Phi4WeatherAgent.Web.Components;
using Phi4WeatherAgent.Agent.Services;
using Phi4WeatherAgent.Agent.Tools;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (Aspire telemetry, health checks, resilience)
builder.AddServiceDefaults();

// Blazor Server
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Register OpenMeteo HTTP clients (needed for MCP tools)
builder.Services.AddOpenMeteoClients(builder.Configuration);

// Register MCP tools for weather queries
builder.Services.AddScoped<GeocodeTool>();
builder.Services.AddScoped<WeatherTool>();
builder.Services.AddScoped<AllergenTool>();

// Register AgentService for conversation management (T033)
builder.Services.AddScoped<AgentService>();

// Configure IChatClient with platform-specific AI provider (T034)
// Platform detection from AppHost: Foundry Local (Windows/macOS) vs Ollama (Linux)
// AI Model Endpoint Configuration
var aiModelEndpoint = builder.Configuration["AI_MODEL_ENDPOINT"] 
    ?? Environment.GetEnvironmentVariable("AI_MODEL_ENDPOINT")
    ?? (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() 
        ? $"http://localhost:{Environment.GetEnvironmentVariable("FOUNDRY_PORT") ?? "63336"}/v1"
        : "http://localhost:11434");

builder.Services.AddChatClient(services =>
{
    // Platform-specific client selection:
    // - Windows/macOS: Use OpenAI client for Foundry Local (OpenAI-compatible)
    // - Linux: Use Ollama client for Ollama container
    if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
    {
        // Foundry Local - OpenAI-compatible API
        var modelId = "Phi-4-mini-instruct-generic-cpu:5";
        Console.WriteLine($"Using OpenAI client with model: {modelId}");
        Console.WriteLine($"Endpoint: {aiModelEndpoint}");
        
        var openAIClient = new OpenAIClient(new ApiKeyCredential("not-used"), new OpenAIClientOptions 
        { 
            Endpoint = new Uri(aiModelEndpoint)
        });
        return openAIClient.GetChatClient(modelId).AsIChatClient();
    }
    else
    {
        // Ollama for Linux
        var modelId = "phi4";
        Console.WriteLine($"Using Ollama client with model: {modelId}");
        return new OllamaChatClient(new Uri(aiModelEndpoint), modelId);
    }
})
.UseFunctionInvocation() // Enable MCP tool calling (T051)
.UseLogging(); // Add telemetry (T018-T020)

var app = builder.Build();

// Test endpoint to verify AI connection
app.MapGet("/test-ai", async (IChatClient chatClient) =>
{
    try
    {
        var response = await chatClient.GetResponseAsync("Say 'Hello from Phi-4!'");
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
