var builder = DistributedApplication.CreateBuilder(args);

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
var agent = builder.AddProject("agent", @"..\..\src\Phi4WeatherAgent.Agent\Phi4WeatherAgent.Agent.csproj")
    .WithEnvironment("AI_MODEL_ENDPOINT", () => aiModelEndpoint.GetEndpoint("http").Url);

// Blazor Server web frontend
var web = builder.AddProject("web", @"..\..\src\Phi4WeatherAgent.Web\Phi4WeatherAgent.Web.csproj")
    .WithReference(agent)
    .WithEnvironment("AI_MODEL_ENDPOINT", () => aiModelEndpoint.GetEndpoint("http").Url);

builder.Build().Run();
