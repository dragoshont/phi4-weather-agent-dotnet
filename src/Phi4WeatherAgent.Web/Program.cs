using Microsoft.Extensions.AI;
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
// Both use OpenAI-compatible API format
var aiModelEndpoint = builder.Configuration["AI_MODEL_ENDPOINT"] ?? "http://localhost:62859";

builder.Services.AddChatClient(services =>
{
    // Foundry Local and Ollama both use OpenAI-compatible endpoints
    // OllamaChatClient works with both since they share the same API format
    // Model: "phi-4-mini" for Foundry Local, "phi4" for Ollama
    var modelId = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() 
        ? "phi-4-mini" 
        : "phi4";
    return new OllamaChatClient(new Uri(aiModelEndpoint), modelId);
})
.UseFunctionInvocation() // Enable MCP tool calling (T051)
.UseLogging(); // Add telemetry (T018-T020)

var app = builder.Build();

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
