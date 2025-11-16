using Microsoft.Extensions.AI;
using Phi4WeatherAgent.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (Aspire telemetry, health checks, resilience)
builder.AddServiceDefaults();

// Blazor Server
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Configure IChatClient with platform-specific AI provider (T030)
// Platform detection from AppHost: Foundry Local (Windows/macOS) vs Ollama (Linux)
var aiModelEndpoint = builder.Configuration["AI_MODEL_ENDPOINT"] ?? "http://localhost:11434";

builder.Services.AddChatClient(services =>
{
    // Use OllamaChatClient for both Foundry Local and Ollama (compatible API)
    // Constructor: OllamaChatClient(Uri endpoint, string modelId)
    return new OllamaChatClient(new Uri(aiModelEndpoint), "phi4");
});

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
