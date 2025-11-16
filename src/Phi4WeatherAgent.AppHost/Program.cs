var builder = DistributedApplication.CreateBuilder(args);

// T067: Configure Aspire OpenTelemetry exporters + dashboards
// Aspire automatically configures OpenTelemetry for metrics, traces, and logs
// Dashboard accessible at: http://localhost:15888 (default Aspire dashboard port)
// Exporters: Console, OTLP (configurable via appsettings)
// Note: ServiceDefaults already wires OpenTelemetry SDK in each project

// Platform detection for AI model hosting (Principle I: Local-First AI)
// Windows/macOS: Foundry Local
// Linux: Ollama
var aiModelEndpoint = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
    ? builder.AddContainer("phi4-foundry", "foundry-local", "phi4:latest")
             .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "http")
    : builder.AddContainer("phi4-ollama", "ollama/ollama", "latest")
             .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "http")
             .WithBindMount("ollama-data", "/root/.ollama");

// Agent backend (ASP.NET Core API with MCP tools)
var agent = builder.AddProject<Projects.Phi4WeatherAgent_Agent>("agent")
    .WithEnvironment("AI_MODEL_ENDPOINT", () => aiModelEndpoint.GetEndpoint("http").Url)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317"); // Optional: OTLP collector

// Blazor Server web frontend
var web = builder.AddProject<Projects.Phi4WeatherAgent_Web>("web")
    .WithReference(agent)
    .WithEnvironment("AI_MODEL_ENDPOINT", () => aiModelEndpoint.GetEndpoint("http").Url)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

builder.Build().Run();
