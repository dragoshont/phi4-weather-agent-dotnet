# Phi-4 Weather Assistant Technical Research

**Research Date**: November 16, 2025  
**Target Framework**: .NET 10 Preview  
**Purpose**: Technical decision support for implementation

---

## 1. Microsoft.Extensions.AI Agent Framework (v10.0.0-preview.1.25071.7+)

### Decision
Use **ChatClientAgent** with IChatClient implementations for building the weather assistant agent, leveraging the unified Microsoft.Extensions.AI abstractions for model integration.

### Rationale
- **Microsoft Agent Framework** is the production-ready successor to Semantic Kernel and AutoGen, providing:
  - Built-in observability with OpenTelemetry
  - Multi-agent orchestration patterns (sequential, concurrent, group chat)
  - Standards-based interoperability (A2A protocol, MCP integration)
  - Cloud-agnostic architecture supporting Azure OpenAI, OpenAI, Ollama, and Foundry Local
- **ChatClientAgent** supports:
  - Function calling (tool integration)
  - Multi-turn conversations with history management
  - Structured output via `ChatResponseFormat.ForJsonSchema<T>()`
  - Custom service-provided tools (MCP, Code Execution)

### Code Pattern

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Create chat client (supports Azure OpenAI, OpenAI, Ollama, Foundry Local)
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();

// Create agent with instructions and tools
AIAgent agent = chatClient.CreateAIAgent(
    name: "WeatherAssistant",
    instructions: @"You are a helpful weather assistant. When users ask about weather,
        use the available tools to fetch current conditions, forecasts, and air quality data.
        Always provide clear, actionable information.",
    tools: [weatherTools] // Function tools for OpenMeteo API
);

// Run agent with streaming
await foreach (var update in agent.RunStreamingAsync("What's the weather in Seattle?"))
{
    if (update is TextContentUpdate textUpdate)
    {
        Console.Write(textUpdate.Text);
    }
}
```

**Key Configuration Options:**
```csharp
// With structured output
var agent = chatClient.CreateAIAgent(new ChatClientAgentOptions
{
    Name = "WeatherAssistant",
    Instructions = "...",
    ChatOptions = new ChatOptions
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema<WeatherResponse>()
    }
});

// With observability
var instrumentedAgent = agent.AsBuilder()
    .UseOpenTelemetry(sourceName: "weather-agent-telemetry")
    .Build();
```

---

## 2. MCP (Model Context Protocol) Tool Implementation for .NET

### Decision
Use **Microsoft Agent Framework's native MCP integration** with `McpClientTool` for exposing weather tools, avoiding manual MCP server implementation.

### Rationale
- Agent Framework provides **seamless MCP integration** out-of-the-box
- MCP tools inherit from `AIFunction`, ensuring smooth integration with Microsoft.Extensions.AI
- Automatic schema discovery and tool registration via `McpClient.ListToolsAsync()`
- Supports both **local MCP servers** (stdio) and **remote servers** (SSE over HTTPS)
- Type-safe tool invocation with automatic function calling

### Code Pattern

```csharp
using ModelContextProtocol.Client;
using Microsoft.Extensions.AI;

// Option 1: Connect to existing MCP server (e.g., Azure MCP for resource management)
var mcpClient = await McpClient.CreateAsync(
    new StdioClientTransport(new()
    {
        Command = "npx",
        Arguments = ["-y", "@azure/mcp@latest", "server", "start"],
        Name = "Azure MCP"
    }));

// Get all available tools from MCP server
var tools = await mcpClient.ListToolsAsync();

// Add tools to chat client
var chatClient = azureOpenAIClient
    .GetChatClient("gpt-4o")
    .AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

// Conversational loop with MCP tools
List<ChatMessage> messages = [];
while (true)
{
    Console.Write("User: ");
    messages.Add(new(ChatRole.User, Console.ReadLine()));

    await foreach (var update in chatClient.GetStreamingResponseAsync(
        messages, new() { Tools = [.. tools] }))
    {
        Console.Write(update);
    }
}

// Option 2: Expose agent as MCP tool for other systems
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

// Convert agent to MCP tool
McpServerTool tool = McpServerTool.Create(weatherAgent.AsAIFunction());

// Host MCP server
var host = Host.CreateEmptyApplicationBuilder(settings: null)
    .Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools([tool])
    .Build();

await host.RunAsync();
```

**Azure Functions Remote MCP Server Pattern:**
```csharp
[Function(nameof(GetWeatherForecast))]
public WeatherForecast GetWeatherForecast(
    [McpToolTrigger("get_weather", "Fetches weather forecast for a location")] 
    ToolInvocationContext context)
{
    var location = context.Arguments["location"];
    return _weatherService.GetForecast(location);
}
```

---

## 3. Aspire 13 Integration with Local AI Models (Foundry Local/Ollama)

### Decision
Use **Aspire Community Toolkit Ollama integration** for local development with **Foundry Local support** via OpenAI-compatible endpoints for production scenarios.

### Rationale
- **Aspire Ollama Integration** (`CommunityToolkit.Aspire.Hosting.Ollama`):
  - Automatic container orchestration with `AddOllama()`
  - GPU support via `WithContainerRuntimeArgs("--gpus=all")`
  - Model persistence with `WithDataVolume()`
  - Health checks for model availability
  - Open WebUI support for testing
- **Foundry Local** (Microsoft's on-device AI):
  - OpenAI-compatible API endpoint (`http://localhost:PORT/v1/chat/completions`)
  - Supports Phi-3.5-mini and other SLMs
  - Native integration via OpenAI agent pattern
  - ONNX runtime optimization

### Code Pattern

```csharp
// AppHost/Program.cs - Aspire Orchestration

var builder = DistributedApplication.CreateBuilder(args);

// Option 1: Ollama with local model (development)
var ollama = builder.AddOllama("ollama")
    .WithDataVolume() // Persist models across restarts
    .WithContainerRuntimeArgs("--gpus=all"); // Enable GPU

var phi35 = ollama.AddModel("phi3.5");

// Option 2: Foundry Local (production on-device)
var foundryLocal = builder.AddOpenAI("foundry-local")
    .WithApiKey(builder.AddParameter("foundry-api-key"))
    .WithEndpoint("http://localhost:5272"); // Foundry Local port

var phi4Model = foundryLocal.AddModel("phi4-chat", "phi-4");

// Add weather agent service
var agentService = builder.AddProject<Projects.Phi4WeatherAgent_Agent>("agent")
    .WithReference(phi35) // or phi4Model for Foundry Local
    .WithEnvironment("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true"); // For local dev

// Add web frontend
builder.AddProject<Projects.Phi4WeatherAgent_Web>("web")
    .WithReference(agentService);

builder.Build().Run();
```

**Agent Service Configuration:**
```csharp
// Agent/Program.cs

var builder = WebApplication.CreateBuilder(args);

// Configure chat client from Aspire connection
builder.AddOpenAIClient("foundry-local"); // or "ollama"

builder.Services.AddChatClient(sp =>
{
    var client = sp.GetRequiredService<OpenAIClient>();
    return client.GetChatClient("phi-4").AsIChatClient(); // Model from Aspire
})
.UseFunctionInvocation()
.UseLogging();

// Create weather agent
builder.Services.AddSingleton<AIAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return chatClient.CreateAIAgent(
        name: "Phi4WeatherAssistant",
        instructions: "You are a weather assistant powered by Phi-4...",
        tools: [/* weather tools */]
    );
});
```

**Foundry Local Setup Commands:**
```powershell
# Start Foundry Local service
foundry service start

# Load Phi-4 model
foundry model load phi-4

# Verify service and model
foundry service status
foundry model list
```

---

## 4. OpenMeteo API Contracts

### Decision
Use **direct HTTP integration** with OpenMeteo's free public API (no API key required), implementing strongly-typed request/response models for the three endpoints.

### Rationale
- **OpenMeteo API** is free, open-source, and requires no authentication
- Provides comprehensive weather data from government sources (NOAA, DWD, etc.)
- Three key endpoints needed:
  1. **Geocoding**: Location name → coordinates
  2. **Weather Forecast**: Temperature, precipitation, wind for 1-16 days
  3. **Air Quality**: Pollutant levels, AQI, health recommendations

### Code Pattern

**Geocoding API:**
```csharp
// GET https://geocoding-api.open-meteo.com/v1/search
public record GeocodingRequest(string Name, int Count = 1, string Language = "en");

public record GeocodingResponse(
    [property: JsonPropertyName("results")] Location[] Results
);

public record Location(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("admin1")] string? State
);

// Usage
var httpClient = _httpClientFactory.CreateClient("OpenMeteo");
var response = await httpClient.GetFromJsonAsync<GeocodingResponse>(
    $"https://geocoding-api.open-meteo.com/v1/search?name={location}&count=1&language=en"
);
```

**Weather Forecast API:**
```csharp
// GET https://api.open-meteo.com/v1/forecast
public record WeatherForecastRequest(
    double Latitude,
    double Longitude,
    string[] Current = null, // ["temperature_2m", "relative_humidity_2m", ...]
    string[] Daily = null,   // ["temperature_2m_max", "precipitation_sum", ...]
    int ForecastDays = 7,
    string TemperatureUnit = "fahrenheit",
    string PrecipitationUnit = "inch",
    string WindSpeedUnit = "mph",
    string Timezone = "auto"
);

public record WeatherForecastResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("current")] CurrentWeather Current,
    [property: JsonPropertyName("daily")] DailyForecast Daily
);

public record CurrentWeather(
    [property: JsonPropertyName("time")] DateTime Time,
    [property: JsonPropertyName("temperature_2m")] double Temperature,
    [property: JsonPropertyName("relative_humidity_2m")] int Humidity,
    [property: JsonPropertyName("apparent_temperature")] double ApparentTemperature,
    [property: JsonPropertyName("precipitation")] double Precipitation,
    [property: JsonPropertyName("weather_code")] int WeatherCode, // WMO code
    [property: JsonPropertyName("wind_speed_10m")] double WindSpeed,
    [property: JsonPropertyName("wind_direction_10m")] int WindDirection
);

public record DailyForecast(
    [property: JsonPropertyName("time")] DateTime[] Time,
    [property: JsonPropertyName("temperature_2m_max")] double[] TemperatureMax,
    [property: JsonPropertyName("temperature_2m_min")] double[] TemperatureMin,
    [property: JsonPropertyName("precipitation_sum")] double[] PrecipitationSum,
    [property: JsonPropertyName("weather_code")] int[] WeatherCode,
    [property: JsonPropertyName("wind_speed_10m_max")] double[] WindSpeedMax
);

// Usage
var url = $"https://api.open-meteo.com/v1/forecast?" +
    $"latitude={lat}&longitude={lon}" +
    $"&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_direction_10m" +
    $"&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,weather_code,wind_speed_10m_max" +
    $"&temperature_unit=fahrenheit&precipitation_unit=inch&wind_speed_unit=mph&timezone=auto&forecast_days=7";

var response = await httpClient.GetFromJsonAsync<WeatherForecastResponse>(url);
```

**Air Quality API:**
```csharp
// GET https://air-quality-api.open-meteo.com/v1/air-quality
public record AirQualityRequest(
    double Latitude,
    double Longitude,
    string[] Current = null, // ["us_aqi", "pm10", "pm2_5", ...]
    int ForecastDays = 1
);

public record AirQualityResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("current")] CurrentAirQuality Current
);

public record CurrentAirQuality(
    [property: JsonPropertyName("time")] DateTime Time,
    [property: JsonPropertyName("us_aqi")] int AirQualityIndex,
    [property: JsonPropertyName("pm10")] double PM10,
    [property: JsonPropertyName("pm2_5")] double PM25,
    [property: JsonPropertyName("carbon_monoxide")] double CarbonMonoxide,
    [property: JsonPropertyName("nitrogen_dioxide")] double NitrogenDioxide,
    [property: JsonPropertyName("sulphur_dioxide")] double SulphurDioxide,
    [property: JsonPropertyName("ozone")] double Ozone
);

// Usage
var url = $"https://air-quality-api.open-meteo.com/v1/air-quality?" +
    $"latitude={lat}&longitude={lon}" +
    $"&current=us_aqi,pm10,pm2_5,carbon_monoxide,nitrogen_dioxide,sulphur_dioxide,ozone";

var response = await httpClient.GetFromJsonAsync<AirQualityResponse>(url);
```

**AIFunction Tools Pattern:**
```csharp
using Microsoft.Extensions.AI;

public class WeatherTools
{
    private readonly IHttpClientFactory _httpClientFactory;

    [Description("Gets current weather and 7-day forecast for a location")]
    public async Task<string> GetWeatherForecast(
        [Description("City name or location")] string location)
    {
        // 1. Geocode location
        var geocoding = await GeocodeLocation(location);
        if (geocoding.Results?.Length == 0) return "Location not found";

        var loc = geocoding.Results[0];

        // 2. Fetch weather forecast
        var weather = await GetWeatherData(loc.Latitude, loc.Longitude);

        // 3. Format response
        return $"Weather for {loc.Name}, {loc.Country}:\n" +
               $"Current: {weather.Current.Temperature}°F, {GetWeatherDescription(weather.Current.WeatherCode)}\n" +
               $"7-Day Forecast: ...";
    }

    [Description("Gets current air quality information for a location")]
    public async Task<string> GetAirQuality(
        [Description("City name or location")] string location)
    {
        var geocoding = await GeocodeLocation(location);
        if (geocoding.Results?.Length == 0) return "Location not found";

        var loc = geocoding.Results[0];
        var airQuality = await GetAirQualityData(loc.Latitude, loc.Longitude);

        return $"Air Quality for {loc.Name}:\n" +
               $"AQI: {airQuality.Current.AirQualityIndex} ({GetAQICategory(airQuality.Current.AirQualityIndex)})\n" +
               $"PM2.5: {airQuality.Current.PM25} µg/m³";
    }

    private string GetAQICategory(int aqi) => aqi switch
    {
        <= 50 => "Good",
        <= 100 => "Moderate",
        <= 150 => "Unhealthy for Sensitive Groups",
        <= 200 => "Unhealthy",
        <= 300 => "Very Unhealthy",
        _ => "Hazardous"
    };
}
```

---

## 5. Blazor Server Accessibility Patterns for WCAG 2.1 AA

### Decision
Implement **semantic HTML with ARIA attributes**, **responsive FlexBox/Grid layouts**, and **SVG icons with proper labels** to achieve WCAG 2.1 Level AA conformance.

### Rationale
- **WCAG 2.1 AA** is the legal benchmark for accessibility (required by EU EN 301 549, US Section 508)
- **Blazor Server** supports full accessibility via:
  - Server-side rendering with semantic HTML
  - UI Automation API integration for screen readers (NVDA, JAWS, Narrator)
  - Automatic ARIA mapping for standard HTML elements
- **Key WCAG 2.1 AA Requirements:**
  - **1.4.3 Contrast (Minimum)**: 4.5:1 for normal text, 3:1 for large text
  - **2.1.1 Keyboard**: All functionality available via keyboard
  - **2.4.7 Focus Visible**: Clear focus indicators
  - **4.1.2 Name, Role, Value**: Proper ARIA labels and roles

### Code Pattern

**SVG Icons with aria-label:**
```razor
<!-- WeatherIcon.razor -->
<svg xmlns="http://www.w3.org/2000/svg" 
     width="48" 
     height="48" 
     viewBox="0 0 24 24" 
     fill="none" 
     stroke="currentColor" 
     stroke-width="2"
     role="img"
     aria-label="@IconLabel">
    @if (IconType == "sunny")
    {
        <circle cx="12" cy="12" r="5" />
        <line x1="12" y1="1" x2="12" y2="3" />
        <line x1="12" y1="21" x2="12" y2="23" />
        <!-- More sun rays... -->
    }
    else if (IconType == "cloudy")
    {
        <path d="M18 10h-1.26A8 8 0 1 0 9 20h9a5 5 0 0 0 0-10z" />
    }
    else if (IconType == "rainy")
    {
        <path d="M16 13v8M8 13v8M12 15v8" />
        <path d="M20 16.58A5 5 0 0 0 18 7h-1.26A8 8 0 1 0 4 15.25" />
    }
</svg>

@code {
    [Parameter] public string IconType { get; set; } = "sunny";
    [Parameter] public string IconLabel { get; set; } = "Weather icon";
}

<!-- Usage with descriptive label -->
<WeatherIcon IconType="sunny" IconLabel="Sunny with clear skies" />
```

**Responsive Carousel/Grid Layout:**
```razor
<!-- WeatherForecast.razor -->
<div class="weather-container" role="region" aria-label="7-Day Weather Forecast">
    <h2 id="forecast-heading">7-Day Forecast</h2>
    
    <!-- Responsive grid (desktop) / carousel (mobile) -->
    <div class="forecast-grid" 
         role="list" 
         aria-labelledby="forecast-heading"
         tabindex="0">
        
        @foreach (var day in Forecast)
        {
            <div class="forecast-card" 
                 role="listitem" 
                 tabindex="0"
                 aria-label="@GetForecastLabel(day)">
                
                <div class="forecast-date">
                    <time datetime="@day.Date.ToString("yyyy-MM-dd")">
                        @day.Date.ToString("ddd, MMM d")
                    </time>
                </div>
                
                <WeatherIcon IconType="@day.IconType" 
                            IconLabel="@day.Condition" />
                
                <div class="forecast-temp" aria-label="Temperature">
                    <span class="temp-high" aria-label="High">@day.HighTemp°</span>
                    <span class="temp-divider">/</span>
                    <span class="temp-low" aria-label="Low">@day.LowTemp°</span>
                </div>
                
                <p class="forecast-description">@day.Condition</p>
            </div>
        }
    </div>
    
    <!-- Carousel navigation for mobile -->
    <div class="carousel-controls" role="group" aria-label="Forecast navigation">
        <button type="button" 
                class="carousel-prev" 
                aria-label="Previous day"
                @onclick="PreviousDay">
            <svg aria-hidden="true"><path d="..."/></svg>
        </button>
        
        <div class="carousel-indicators" role="tablist">
            @for (int i = 0; i < Forecast.Count; i++)
            {
                var index = i;
                <button type="button"
                        role="tab"
                        aria-selected="@(CurrentIndex == index)"
                        aria-label="Day @(i + 1)"
                        @onclick="() => GoToDay(index)">
                </button>
            }
        </div>
        
        <button type="button" 
                class="carousel-next" 
                aria-label="Next day"
                @onclick="NextDay">
            <svg aria-hidden="true"><path d="..."/></svg>
        </button>
    </div>
</div>

@code {
    [Parameter] public List<DailyForecast> Forecast { get; set; } = new();
    private int CurrentIndex { get; set; } = 0;

    private string GetForecastLabel(DailyForecast day) =>
        $"{day.Date:dddd, MMMM d}: {day.Condition}, high {day.HighTemp}, low {day.LowTemp}";

    private void PreviousDay() => CurrentIndex = Math.Max(0, CurrentIndex - 1);
    private void NextDay() => CurrentIndex = Math.Min(Forecast.Count - 1, CurrentIndex + 1);
    private void GoToDay(int index) => CurrentIndex = index;
}
```

**CSS with Accessibility:**
```css
/* WeatherForecast.razor.css */

/* Responsive grid → mobile carousel */
.forecast-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
    gap: 1rem;
    padding: 1rem;
}

@media (max-width: 768px) {
    .forecast-grid {
        display: flex;
        overflow-x: auto;
        scroll-snap-type: x mandatory;
        -webkit-overflow-scrolling: touch;
    }
    
    .forecast-card {
        flex: 0 0 80%;
        scroll-snap-align: center;
    }
}

/* Focus visible (WCAG 2.1 requirement) */
.forecast-card:focus,
.carousel-controls button:focus {
    outline: 3px solid var(--focus-color, #0078d4);
    outline-offset: 2px;
}

/* High contrast mode support */
@media (prefers-contrast: high) {
    .forecast-card {
        border: 2px solid currentColor;
    }
}

/* Reduced motion for animations */
@media (prefers-reduced-motion: reduce) {
    .forecast-grid,
    .forecast-card {
        transition: none;
        scroll-behavior: auto;
    }
}

/* Ensure sufficient color contrast (4.5:1 minimum) */
.forecast-card {
    background-color: #ffffff;
    color: #1a1a1a;
}

.temp-high {
    color: #d13438; /* 4.51:1 contrast on white */
}

.temp-low {
    color: #0078d4; /* 4.54:1 contrast on white */
}
```

**Semantic HTML Component:**
```razor
<!-- LoadingSpinner.razor -->
<div class="loading-container" 
     role="status" 
     aria-live="polite" 
     aria-busy="true">
    
    <svg class="spinner" 
         role="img" 
         aria-label="Loading weather data">
        <circle cx="50%" cy="50%" r="40%" />
        <circle cx="50%" cy="50%" r="40%" />
    </svg>
    
    <p class="loading-text">@LoadingMessage</p>
</div>

@code {
    [Parameter] public string LoadingMessage { get; set; } = "Loading...";
}
```

**Keyboard Navigation:**
```csharp
// MainLayout.razor.cs
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await JS.InvokeVoidAsync("initKeyboardNavigation");
    }
}
```

```javascript
// app.js
window.initKeyboardNavigation = () => {
    document.addEventListener('keydown', (e) => {
        // Arrow key navigation for carousel
        if (e.key === 'ArrowLeft') {
            document.querySelector('.carousel-prev')?.click();
        } else if (e.key === 'ArrowRight') {
            document.querySelector('.carousel-next')?.click();
        }
        // Skip to main content (WCAG 2.4.1)
        else if (e.key === 's' && e.ctrlKey) {
            document.querySelector('main')?.focus();
        }
    });
};
```

---

## 6. Polly 8.5+ Resilience Patterns for HTTP Retry Logic

### Decision
Use **Microsoft.Extensions.Http.Resilience** with standard resilience handlers for automatic retry, circuit breaker, and timeout strategies on OpenMeteo API calls.

### Rationale
- **Polly 8.5** is built into .NET resilience extensions, replacing legacy `Microsoft.Extensions.Http.Polly`
- **Standard Resilience Handler** provides:
  - Retry with exponential backoff and jitter
  - Circuit breaker (5 failures → 30 sec break)
  - Timeout (per-request and total)
  - Bulkhead isolation (rate limiting)
- **HttpClientFactory integration** ensures resilience is applied at HTTP message handler level
- **Handles transient faults**: `HttpRequestException`, HTTP 5xx, HTTP 408, HTTP 429

### Code Pattern

**Basic Setup (Recommended):**
```csharp
// Program.cs
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Add standard resilience handler to OpenMeteo HTTP client
builder.Services.AddHttpClient("OpenMeteo", client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com");
    client.DefaultRequestHeaders.Add("User-Agent", "Phi4WeatherAgent/1.0");
})
.AddStandardResilienceHandler(); // Default: 3 retries, circuit breaker, 30s timeout

// Alternative: Configure all HTTP clients with default resilience
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddStandardResilienceHandler();
});
```

**Custom Resilience Pipeline:**
```csharp
builder.Services.AddHttpClient("OpenMeteo", client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com");
})
.AddResilienceHandler("WeatherApiPipeline", builder =>
{
    // Retry with exponential backoff and jitter
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        BackoffType = DelayBackoffType.Exponential,
        MaxRetryAttempts = 5,
        UseJitter = true, // Prevents retry storms
        Delay = TimeSpan.FromSeconds(1),
        ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.StatusCode is 
                HttpStatusCode.RequestTimeout or 
                HttpStatusCode.TooManyRequests or
                HttpStatusCode.ServiceUnavailable
        )
    });

    // Circuit breaker
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        SamplingDuration = TimeSpan.FromSeconds(10),
        FailureRatio = 0.2, // Break after 20% failures
        MinimumThroughput = 3,
        BreakDuration = TimeSpan.FromSeconds(30),
        ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.StatusCode >= HttpStatusCode.InternalServerError
        )
    });

    // Timeout strategy
    builder.AddTimeout(TimeSpan.FromSeconds(10));
});
```

**Advanced: Handle Rate Limiting (429 Too Many Requests):**
```csharp
// WeatherService.cs
public class WeatherService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public WeatherService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<WeatherForecastResponse> GetForecastAsync(
        double lat, double lon, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("OpenMeteo");
        
        var url = $"/v1/forecast?" +
            $"latitude={lat}&longitude={lon}" +
            $"&current=temperature_2m,weather_code" +
            $"&daily=temperature_2m_max,temperature_2m_min" +
            $"&forecast_days=7";

        // Resilience handler automatically retries with exponential backoff
        var response = await client.GetAsync(url, ct);
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WeatherForecastResponse>(ct);
    }
}

// Resilience configuration in Program.cs
builder.Services.AddHttpClient<WeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com");
})
.AddResilienceHandler("OpenMeteoResilience", resilienceBuilder =>
{
    // Custom retry policy that respects Retry-After header
    resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        UseJitter = true,
        DelayGenerator = args =>
        {
            // Check for Retry-After header (RFC 7231)
            if (args.Outcome.Result?.Headers.RetryAfter?.Delta is { } retryAfter)
            {
                return new ValueTask<TimeSpan?>(retryAfter);
            }

            // Exponential backoff: 2^attempt seconds
            var delay = TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber));
            return new ValueTask<TimeSpan?>(delay);
        }
    });

    resilienceBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(60)
    });
});
```

**Dynamic Reload Support:**
```csharp
// Resilience options can be dynamically reloaded from config
builder.Services.Configure<HttpRetryStrategyOptions>("RetryOptions", 
    builder.Configuration.GetSection("Resilience:Retry"));

builder.Services.AddHttpClient("OpenMeteo")
    .AddResilienceHandler("DynamicPipeline", 
        (ResiliencePipelineBuilder<HttpResponseMessage> pipelineBuilder,
         ResilienceHandlerContext context) =>
    {
        // Enable reloads when config changes
        context.EnableReloads<HttpRetryStrategyOptions>("RetryOptions");

        var retryOptions = context.GetOptions<HttpRetryStrategyOptions>("RetryOptions");
        pipelineBuilder.AddRetry(retryOptions);
    });
```

**Observability with OpenTelemetry:**
```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true; // Log retry exceptions
        });
        tracing.AddAspNetCoreInstrumentation();
    });

// Resilience pipeline events are automatically logged
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
});
```

**Testing Resilience:**
```csharp
// WeatherServiceTests.cs
[Fact]
public async Task GetForecast_Retries_On_Transient_Failure()
{
    var handler = new MockHttpMessageHandler();
    handler
        .When("*")
        .Respond(HttpStatusCode.ServiceUnavailable) // First call fails
        .Respond(HttpStatusCode.ServiceUnavailable) // Second call fails
        .Respond(HttpStatusCode.OK, "application/json", "{ ... }"); // Third succeeds

    var httpClient = handler.ToHttpClient();
    var service = new WeatherService(httpClient);

    var result = await service.GetForecastAsync(47.6062, -122.3321);

    Assert.NotNull(result);
    handler.VerifyNoOutstandingExpectation();
    handler.VerifyCallCount(3); // Verify 3 attempts were made
}
```

---

## Summary of Recommendations

| Topic | Recommended Approach | Key Benefits |
|-------|---------------------|--------------|
| **Agent Framework** | `ChatClientAgent` with `Microsoft.Agents.AI` | Production-ready, multi-agent orchestration, OpenTelemetry built-in |
| **MCP Integration** | Native Agent Framework MCP support | Seamless tool discovery, type-safe invocation, no custom server code |
| **Local AI Models** | Aspire Ollama integration + Foundry Local | GPU support, model persistence, OpenAI-compatible endpoints |
| **OpenMeteo API** | Direct HTTP with strongly-typed models | Free, no auth, comprehensive data (geocoding, forecast, air quality) |
| **Accessibility** | Semantic HTML + ARIA + responsive layout | WCAG 2.1 AA compliant, screen reader support, keyboard navigation |
| **HTTP Resilience** | `Microsoft.Extensions.Http.Resilience` | Standard handlers, exponential backoff, circuit breaker, auto-retry |

---

## Implementation Priority

1. **Phase 1**: Set up Aspire orchestration with Foundry Local/Ollama
2. **Phase 2**: Implement OpenMeteo API service with Polly resilience
3. **Phase 3**: Create weather tools as `AIFunction` implementations
4. **Phase 4**: Build `ChatClientAgent` with tool integration
5. **Phase 5**: Develop accessible Blazor UI with WCAG 2.1 AA compliance
6. **Phase 6**: Add MCP server support for external agent integration

---

## References

- [Microsoft Agent Framework Docs](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)
- [Model Context Protocol (MCP) C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [Aspire Community Toolkit Ollama](https://learn.microsoft.com/en-us/dotnet/aspire/community-toolkit/ollama)
- [Foundry Local Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/what-is-foundry-local)
- [OpenMeteo API Documentation](https://open-meteo.com/en/docs)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/standards-guidelines/wcag/)
- [Polly Resilience Documentation](https://www.pollydocs.org/)
- [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
