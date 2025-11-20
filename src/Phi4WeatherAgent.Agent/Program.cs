using Phi4WeatherAgent.Agent.Services;
using Phi4WeatherAgent.Agent.Tools;
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

// Register OpenMeteo HTTP clients with Polly resilience (T027-T028)
builder.Services.AddOpenMeteoClients(builder.Configuration);

// Register AgentService for conversation context (T029)
builder.Services.AddScoped<AgentService>();

// Register MCP tools (T036-T037, T047)
builder.Services.AddScoped<GeocodeTool>();
builder.Services.AddScoped<WeatherTool>();
builder.Services.AddScoped<AllergenTool>();

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
