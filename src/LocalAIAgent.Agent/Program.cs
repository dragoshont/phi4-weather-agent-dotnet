using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using LocalAIAgent.Agent.Dispatching;
using LocalAIAgent.Agent.Handlers;
using LocalAIAgent.Agent.Interfaces;
using LocalAIAgent.Agent.Models;
using LocalAIAgent.Agent.Parsing;
using LocalAIAgent.Agent.Registry;
using LocalAIAgent.Agent.Services;
using LocalAIAgent.Agent.Tools;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

// Add Aspire service defaults (telemetry, health checks, service discovery, resilience)
builder.AddServiceDefaults();

// Add OpenAPI/Swagger
builder.Services.AddOpenApi();

// Register AI configuration
builder.Services.Configure<AIConfiguration>(builder.Configuration.GetSection("AI"));

// Register configuration provider for environment variable substitution
builder.Services.AddSingleton<LocalAIAgent.Agent.Services.ConfigurationProvider>();

// Register configuration validator for fail-fast behavior
builder.Services.AddSingleton<ConfigurationValidator>();

// Register prompt provider
builder.Services.AddSingleton<IPromptProvider, PromptProvider>();

// Register functools parsing and tool invocation dependencies
builder.Services.AddSingleton<IFunctoolsParser, FunctoolsParser>();
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddSingleton<IToolInvoker, ToolInvoker>();
builder.Services.AddHostedService<ToolDiscoveryService>();

// Register tool invocation handlers (keyed services)
builder.Services.AddKeyedSingleton<IToolInvocationHandler, FunctoolsHandler>("Functools");
// Future handlers can be registered here:
// builder.Services.AddKeyedSingleton<IToolInvocationHandler, ReActJSONHandler>("ReActJSON");

// Register OpenMeteo HTTP clients with Polly resilience
builder.Services.AddOpenMeteoClients(builder.Configuration);

// Register AgentService for legacy support (T029)
builder.Services.AddScoped<AgentService>();

// Register MCP tools (T036-T037, T047)
builder.Services.AddScoped<GeocodeTool>();
builder.Services.AddScoped<WeatherTool>();
builder.Services.AddScoped<AllergenTool>();

// T045: Register ChatClientFactory for creating IChatClient with conditional handlers
builder.Services.AddSingleton<ChatClientFactory>();

// T045: Register ChatClientAgent with Agent Framework
builder.Services.AddSingleton<ChatClientAgent>(sp =>
{
    var factory = sp.GetRequiredService<ChatClientFactory>();
    var promptProvider = sp.GetRequiredService<IPromptProvider>();
    var geocodeTool = sp.GetRequiredService<GeocodeTool>();
    var weatherTool = sp.GetRequiredService<WeatherTool>();
    var allergenTool = sp.GetRequiredService<AllergenTool>();
    var logger = sp.GetRequiredService<ILogger<ChatClientAgent>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    // Create base IChatClient with conditional handler
    var chatClient = factory.CreateChatClient();

    logger.LogInformation("Creating ChatClientAgent with Agent Framework");

    // Get system prompt
    var systemPrompt = promptProvider.GetSystemPromptAsync().GetAwaiter().GetResult();

    // Create ChatClientAgent with tools using constructor
    var agent = new ChatClientAgent(
        chatClient: chatClient,
        instructions: systemPrompt,
        name: "WeatherAssistant",
        description: "AI assistant for weather forecasts and allergen information",
        tools: [
            AIFunctionFactory.Create(geocodeTool.GeocodeLocationAsync),
            AIFunctionFactory.Create(weatherTool.GetWeatherForecastAsync),
            AIFunctionFactory.Create(allergenTool.GetAllergenLevelsAsync)
        ],
        loggerFactory: loggerFactory,
        services: sp
    );

    logger.LogInformation("ChatClientAgent created successfully with {ToolCount} tools", 3);

    return agent;
});

// T045: Register ChatAgentService for managing AgentThread
builder.Services.AddScoped<ChatAgentService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map Aspire default endpoints (/health, /alive)
app.MapDefaultEndpoints();

// TODO T035: Map MCP tool endpoints

app.Run();
