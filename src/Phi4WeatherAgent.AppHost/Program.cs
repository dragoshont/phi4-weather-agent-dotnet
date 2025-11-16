var builder = DistributedApplication.CreateBuilder(args);

// T067: Configure Aspire OpenTelemetry exporters + dashboards
// Aspire automatically configures OpenTelemetry for metrics, traces, and logs
// Dashboard accessible at: http://localhost:15888 (default Aspire dashboard port)
// Exporters: Console, OTLP (configurable via appsettings)
// Note: ServiceDefaults already wires OpenTelemetry SDK in each project

// Platform detection for AI model hosting (Principle I: Local-First AI)
// Windows/macOS: Foundry Local (external service - check with `foundry service status`)
// Linux: Ollama container
// Note: Foundry Local must be running and port may vary (typically 62859 or 63336)
string aiModelEndpointUrl;
if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
{
    // Foundry Local OpenAI-compatible endpoint (requires /v1 base path)
    // Get port from environment variable or use default
    var foundryPort = Environment.GetEnvironmentVariable("FOUNDRY_PORT") ?? "62859";
    aiModelEndpointUrl = $"http://localhost:{foundryPort}/v1";
}
else
{
    // Ollama container (Linux)
    var ollama = builder.AddContainer("phi4-ollama", "ollama/ollama", "latest")
                        .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "http")
                        .WithBindMount("ollama-data", "/root/.ollama");
    aiModelEndpointUrl = $"http://localhost:11434"; // Will be replaced by actual endpoint
}

// Agent backend (ASP.NET Core API with MCP tools)
var agent = builder.AddProject<Projects.Phi4WeatherAgent_Agent>("agent")
    .WithEnvironment("AI_MODEL_ENDPOINT", aiModelEndpointUrl)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317"); // Optional: OTLP collector

// Blazor Server web frontend
var web = builder.AddProject<Projects.Phi4WeatherAgent_Web>("web")
    .WithReference(agent)
    .WithEnvironment("AI_MODEL_ENDPOINT", aiModelEndpointUrl)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

builder.Build().Run();
