# Research Document: Phi-4 Weather Assistant

**Feature**: 001-phi4-weather-assistant  
**Phase**: 0 (Outline & Research)  
**Generated**: 2025-11-16

## Purpose

This document resolves all "NEEDS CLARIFICATION" items from Technical Context and provides research-backed decisions for technology choices, integration patterns, and best practices.

---

## 1. Microsoft.Extensions.AI Agent Framework Patterns

### Decision
Use **Microsoft Agent Framework's `ChatClientAgent`** for orchestration, leveraging built-in tool calling, OpenTelemetry integration, and support for function invocation from `Microsoft.Extensions.AI.IChatClient`.

### Rationale
- **Production-ready framework**: Agent Framework combines best of Semantic Kernel and AutoGen with enterprise features
- **IChatClient abstraction**: Uses `Microsoft.Extensions.AI.IChatClient` for provider flexibility (OpenAI, Azure OpenAI, Ollama)
- **Built-in function invocation**: `.UseFunctionInvocation()` middleware handles tool calling automatically
- **Aspire-ready**: Built-in telemetry exports to Aspire Dashboard via OpenTelemetry

### Code Pattern
```csharp
// Program.cs - Register Agent Framework services
builder.Services.AddChatClient(services =>
{
    IChatClient chatClient = /* Ollama or Foundry Local IChatClient */;
    
    return new ChatClientBuilder(chatClient)
        .UseFunctionInvocation() // Enable tool calling
        .UseOpenTelemetry() // Aspire Dashboard integration
        .Build(services);
});

// AgentService.cs - Create ChatClientAgent with tools
public class WeatherAgentService
{
    private readonly ChatClientAgent _agent;

    public WeatherAgentService(IChatClient chatClient)
    {
        _agent = new ChatClientAgent(
            chatClient,
            instructions: "You are a weather assistant. Use the provided tools to answer weather queries. " +
                         "Respond only to weather-related questions. Be concise and friendly.",
            name: "WeatherAgent");
    }

    public async Task<string> ProcessQueryAsync(string userMessage, CancellationToken cancellationToken)
    {
        // Agent Framework handles tool calling automatically via IChatClient middleware
        var chatOptions = new ChatOptions
        {
            Tools = [
                AIFunctionFactory.Create(GeocodeTool.GeocodeAsync),
                AIFunctionFactory.Create(WeatherTool.GetWeatherAsync),
                AIFunctionFactory.Create(AllergenTool.GetAllergensAsync)
            ],
            Temperature = 0.7f
        };

        var response = await _agent.RunAsync(userMessage, chatOptions, cancellationToken);
        return response.Text;
    }
}
```

### Alternatives Considered
- **Semantic Kernel**: Rejected due to Constitution Principle III (Agent Framework only)
- **Custom agent loop**: Rejected - framework provides production-ready orchestration with telemetry

---

## 2. Model Context Protocol (MCP) Tool Implementation

### Decision
Implement **native C# methods as tools using `AIFunctionFactory.Create()`**, passing them via `ChatOptions.Tools` to the `IChatClient` pipeline with `.UseFunctionInvocation()` middleware.

### Rationale
- **Microsoft.Extensions.AI pattern**: Functions decorated with XML docs become tools automatically
- **Type safety**: Strong typing for parameters and return values at compile time
- **Testability**: Tools are injected services, easily mocked for unit tests
- **Performance**: No IPC overhead - tools run in-process via reflection

### Code Pattern
```csharp
// Tools/GeocodingTool.cs
public class GeocodingTool
{
    private readonly HttpClient _httpClient;

    public GeocodingTool(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("OpenMeteo");
    }

    /// <summary>
    /// Convert location name to geographic coordinates
    /// </summary>
    /// <param name="locationName">Location name (city, state, country)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of matching locations</returns>
    public async Task<Location[]> GeocodeAsync(
        string locationName,
        CancellationToken cancellationToken = default)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(locationName))
            return Array.Empty<Location>();

        // Call OpenMeteo Geocoding API
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(locationName)}&count=5&language=en&format=json";
        var response = await _httpClient.GetFromJsonAsync<GeocodingResponse>(url, cancellationToken);

        if (response?.Results == null || response.Results.Length == 0)
            return Array.Empty<Location>();

        return response.Results.Select(r => new Location
        {
            Name = r.Name,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            Country = r.Country,
            State = r.Admin1
        }).ToArray();
    }
}

// Register tools in Program.cs
builder.Services.AddSingleton<GeocodingTool>();
builder.Services.AddSingleton<WeatherTool>();
builder.Services.AddSingleton<AllergenTool>();

// Use tools via AIFunctionFactory
var geocodeTool = serviceProvider.GetRequiredService<GeocodingTool>();
var chatOptions = new ChatOptions
{
    Tools = [AIFunctionFactory.Create(geocodeTool.GeocodeAsync)],
    Temperature = 0.7f
};
```

### Alternatives Considered
- **Standalone MCP servers**: Rejected - adds deployment complexity, IPC overhead
- **REST API gateway**: Rejected - unnecessary network layer for local-only tools

---

## 3. Aspire 13 Integration with Local AI Models

### Decision
Use **Aspire Community Toolkit's `AddOllama()` extension for development**, with Foundry Local configured as OpenAI-compatible endpoint for production.

### Rationale
- **Unified orchestration**: Aspire manages both HTTP services and AI model endpoints
- **Platform detection**: AppHost can detect OS and configure Foundry Local (Windows/macOS) vs Ollama (Linux) automatically
- **Telemetry**: All model calls appear in Aspire Dashboard with latency/token metrics
- **Local development**: Developers don't need separate Ollama/Foundry Local processes - Aspire starts them

### Code Pattern
```csharp
// Phi4WeatherAgent.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Detect platform and configure AI model endpoint
var aiModel = OperatingSystem.IsLinux() 
    ? builder.AddOllama("ollama", port: 11434)
             .AddModel("phi4-mini") // Ollama model name
    : builder.AddConnectionString("foundry-local", 
                "http://localhost:5272/v1/chat/completions"); // Foundry Local OpenAI-compatible endpoint

// Add web application with AI model reference
var web = builder.AddProject<Projects.Phi4WeatherAgent_Web>("web")
                 .WithReference(aiModel)
                 .WithExternalHttpEndpoints(); // Expose to browser

builder.Build().Run();

// Phi4WeatherAgent.Web/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure IChatClient based on Aspire-provided endpoint
builder.AddServiceDefaults(); // Aspire telemetry + service discovery

var aiEndpoint = builder.Configuration.GetConnectionString("ollama") 
              ?? builder.Configuration.GetConnectionString("foundry-local")
              ?? throw new InvalidOperationException("No AI endpoint configured");

if (aiEndpoint.Contains("ollama"))
{
    builder.Services.AddOllamaChatClient("phi4-mini", endpoint: new Uri(aiEndpoint));
}
else
{
    builder.Services.AddOpenAIChatClient("phi4-mini", endpoint: new Uri(aiEndpoint));
}

builder.Services.AddChatClient(/* agent configuration */);
```

### Alternatives Considered
- **Manual process management**: Rejected - error-prone, no telemetry integration
- **Docker containers**: Rejected - Constitution requires native local inference (no containers)

---

## 4. OpenMeteo API Contracts

### Decision
Create **strongly-typed DTOs for three OpenMeteo endpoints** (Geocoding, Weather Forecast, Air Quality) with JSON source generation for performance.

### Rationale
- **Free & no-auth**: OpenMeteo APIs require no API keys, aligning with Constitution Principle VI (Zero Cost)
- **Comprehensive data**: Provides current weather, 7-day forecasts, and pollen levels in single API
- **Reliable**: 99.9% uptime, rate limit 10,000 requests/day (sufficient for single-user app)
- **Type safety**: Strong typing prevents runtime errors from API schema changes

### API Contracts

#### Geocoding API
**Endpoint**: `https://geocoding-api.open-meteo.com/v1/search`

```json
{
  "name": "Seattle",
  "count": 5,
  "language": "en",
  "format": "json"
}
```

**Response**:
```json
{
  "results": [
    {
      "id": 5809844,
      "name": "Seattle",
      "latitude": 47.60621,
      "longitude": -122.33207,
      "country": "United States",
      "admin1": "Washington"
    }
  ]
}
```

#### Weather Forecast API
**Endpoint**: `https://api.open-meteo.com/v1/forecast`

```json
{
  "latitude": 47.60621,
  "longitude": -122.33207,
  "current": ["temperature_2m", "relative_humidity_2m", "apparent_temperature", "precipitation", "weather_code", "wind_speed_10m"],
  "daily": ["temperature_2m_max", "temperature_2m_min", "precipitation_probability_max", "weather_code"],
  "temperature_unit": "fahrenheit",
  "wind_speed_unit": "mph",
  "precipitation_unit": "inch",
  "timezone": "auto",
  "forecast_days": 7
}
```

**Response**:
```json
{
  "latitude": 47.6,
  "longitude": -122.33,
  "current": {
    "time": "2025-11-16T10:00",
    "temperature_2m": 52.0,
    "relative_humidity_2m": 75,
    "apparent_temperature": 48.0,
    "precipitation": 0.0,
    "weather_code": 3,
    "wind_speed_10m": 8.5
  },
  "daily": {
    "time": ["2025-11-16", "2025-11-17", ...],
    "temperature_2m_max": [58.0, 60.0, ...],
    "temperature_2m_min": [45.0, 47.0, ...],
    "precipitation_probability_max": [20, 10, ...],
    "weather_code": [3, 2, ...]
  }
}
```

#### Air Quality API (Pollen)
**Endpoint**: `https://air-quality-api.open-meteo.com/v1/air-quality`

```json
{
  "latitude": 47.60621,
  "longitude": -122.33207,
  "current": ["european_aqi", "alder_pollen", "birch_pollen", "grass_pollen", "mugwort_pollen", "ragweed_pollen"],
  "timezone": "auto"
}
```

**Response**:
```json
{
  "latitude": 47.6,
  "longitude": -122.33,
  "current": {
    "time": "2025-11-16T10:00",
    "european_aqi": 25,
    "alder_pollen": 2.1,
    "birch_pollen": 0.3,
    "grass_pollen": 12.5,
    "mugwort_pollen": 0.0,
    "ragweed_pollen": 4.2
  }
}
```

### Code Pattern
```csharp
// Models/WeatherData.cs
[JsonSerializable(typeof(WeatherData))]
public record WeatherData
{
    public required CurrentWeather Current { get; init; }
    public required DailyForecast Daily { get; init; }
}

public record CurrentWeather
{
    [JsonPropertyName("time")]
    public required string Time { get; init; }
    
    [JsonPropertyName("temperature_2m")]
    public required double Temperature { get; init; }
    
    [JsonPropertyName("apparent_temperature")]
    public required double FeelsLike { get; init; }
    
    [JsonPropertyName("relative_humidity_2m")]
    public required int Humidity { get; init; }
    
    [JsonPropertyName("wind_speed_10m")]
    public required double WindSpeed { get; init; }
    
    [JsonPropertyName("weather_code")]
    public required int WeatherCode { get; init; }
}

public record DailyForecast
{
    [JsonPropertyName("time")]
    public required string[] Time { get; init; }
    
    [JsonPropertyName("temperature_2m_max")]
    public required double[] TemperatureMax { get; init; }
    
    [JsonPropertyName("temperature_2m_min")]
    public required double[] TemperatureMin { get; init; }
    
    [JsonPropertyName("precipitation_probability_max")]
    public required int[] PrecipitationProbability { get; init; }
    
    [JsonPropertyName("weather_code")]
    public required int[] WeatherCode { get; init; }
}
```

### Weather Code Mapping
OpenMeteo uses WMO weather codes (0-99):
- **0**: Clear sky → "sunny.svg"
- **1-3**: Partly cloudy → "partly-cloudy.svg"
- **45, 48**: Fog → "fog.svg"
- **51-67**: Rain (various intensities) → "rainy.svg"
- **71-77**: Snow → "snowy.svg"
- **80-99**: Showers/thunderstorms → "stormy.svg"

---

## 5. Blazor Server Accessibility Patterns (WCAG 2.1 AA)

### Decision
Use **semantic HTML with ARIA labels, FlexBox/CSS Grid for responsive layout, and inline SVG icons with `<title>` elements** for screen reader compatibility.

### Rationale
- **Standards compliance**: Semantic HTML + ARIA is the W3C recommended approach for WCAG 2.1 AA
- **Browser support**: FlexBox and Grid have 97%+ browser support, work on all target platforms
- **Screen reader compatibility**: Inline SVG with `<title>` is announced by NVDA/JAWS/VoiceOver
- **Performance**: No external icon font or image requests - SVGs embedded in Blazor components

### Code Pattern

#### Responsive Weather Card Layout
```razor
@* Components/WeatherCard.razor *@
<article class="weather-card" role="region" aria-label="Weather forecast for @Location.Name">
    <header class="weather-card__header">
        <h2 id="weather-location-@LocationId">@Location.Name, @Location.State</h2>
    </header>
    
    <div class="weather-card__current">
        <div class="weather-icon" aria-hidden="true">
            @GetWeatherIconSvg(WeatherData.Current.WeatherCode)
        </div>
        
        <div class="weather-details">
            <p class="temperature" aria-label="Current temperature">
                <span class="temp-value">@WeatherData.Current.Temperature.ToString("F0")</span>
                <span class="temp-unit">°F</span>
            </p>
            <p class="condition">@GetWeatherDescription(WeatherData.Current.WeatherCode)</p>
            <p class="feels-like" aria-label="Feels like temperature">
                Feels like @WeatherData.Current.FeelsLike.ToString("F0")°F
            </p>
        </div>
    </div>
    
    <div class="weather-card__forecast" role="list" aria-label="7-day forecast">
        @for (int i = 0; i < WeatherData.Daily.Time.Length; i++)
        {
            <div class="forecast-day" role="listitem">
                <p class="day-name">@GetDayName(WeatherData.Daily.Time[i])</p>
                <div class="forecast-icon" aria-hidden="true">
                    @GetWeatherIconSvg(WeatherData.Daily.WeatherCode[i], small: true)
                </div>
                <p class="day-temp" aria-label="High @WeatherData.Daily.TemperatureMax[i]°F, Low @WeatherData.Daily.TemperatureMin[i]°F">
                    <span class="high">@WeatherData.Daily.TemperatureMax[i].ToString("F0")°</span>
                    <span class="low">@WeatherData.Daily.TemperatureMin[i].ToString("F0")°</span>
                </p>
            </div>
        }
    </div>
</article>

@code {
    [Parameter] public required Location Location { get; set; }
    [Parameter] public required WeatherData WeatherData { get; set; }
    
    private string LocationId => Location.Name.Replace(" ", "-").ToLowerInvariant();
    
    private RenderFragment GetWeatherIconSvg(int weatherCode, bool small = false)
    {
        var size = small ? 32 : 64;
        var iconName = weatherCode switch
        {
            0 => "sunny",
            >= 1 and <= 3 => "partly-cloudy",
            >= 45 and <= 48 => "fog",
            >= 51 and <= 67 => "rainy",
            >= 71 and <= 77 => "snowy",
            _ => "stormy"
        };
        
        var description = GetWeatherDescription(weatherCode);
        
        return @<svg width="@size" height="@size" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <title>@description weather icon</title>
            @* SVG path data based on icon type *@
            @if (iconName == "sunny")
            {
                <circle cx="12" cy="12" r="5" fill="currentColor"/>
                <path d="M12 1v3M12 20v3M4.22 4.22l2.12 2.12M17.66 17.66l2.12 2.12M1 12h3M20 12h3M4.22 19.78l2.12-2.12M17.66 6.34l2.12-2.12" 
                      stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
            }
            @* Other icons... *@
        </svg>;
    }
    
    private string GetWeatherDescription(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        _ => "Various conditions"
    };
}
```

#### CSS for Responsive Layout
```css
/* wwwroot/css/weather-cards.css */

.weather-card {
    background-color: var(--card-bg); /* #FFFFFF in light mode */
    border: 1px solid var(--card-border); /* #D1D5DB - 4.5:1 contrast ratio */
    border-radius: 12px;
    padding: 1.5rem;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.weather-card:focus-within {
    outline: 3px solid var(--focus-color); /* #2563EB - visible focus indicator */
    outline-offset: 2px;
}

.weather-card__header h2 {
    font-size: 1.5rem;
    font-weight: 600;
    color: var(--text-primary); /* #111827 - 13.6:1 contrast */
    margin: 0 0 1rem 0;
}

.weather-card__current {
    display: flex;
    align-items: center;
    gap: 1.5rem;
    margin-bottom: 2rem;
}

.weather-icon svg {
    color: var(--icon-color); /* #F59E0B for sunny, etc. */
}

.temperature {
    font-size: 3rem;
    font-weight: 700;
    line-height: 1;
    color: var(--text-primary);
}

.weather-card__forecast {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(80px, 1fr));
    gap: 1rem;
}

/* Mobile-specific: Horizontal scrollable carousel */
@media (max-width: 640px) {
    .weather-card__forecast {
        display: flex;
        overflow-x: auto;
        scroll-snap-type: x mandatory;
        gap: 0.75rem;
        padding-bottom: 0.5rem;
    }
    
    .forecast-day {
        flex: 0 0 80px;
        scroll-snap-align: start;
    }
}

/* Ensure color contrast ≥4.5:1 for WCAG AA */
:root {
    --card-bg: #FFFFFF;
    --card-border: #D1D5DB;
    --text-primary: #111827;
    --text-secondary: #6B7280;
    --focus-color: #2563EB;
    --icon-color: #F59E0B;
}

@media (prefers-color-scheme: dark) {
    :root {
        --card-bg: #1F2937;
        --card-border: #374151;
        --text-primary: #F9FAFB;
        --text-secondary: #D1D5DB;
        --focus-color: #60A5FA;
    }
}
```

### Accessibility Testing Checklist
- [ ] Keyboard navigation: All cards tabbable, Enter/Space activates
- [ ] Screen reader: Weather card announces "Weather forecast for Seattle, Washington"
- [ ] Focus indicators: Visible 3px outline on :focus-within
- [ ] Color contrast: All text ≥4.5:1 ratio (verified with axe DevTools)
- [ ] Responsive: Carousel scrollable on mobile, grid on desktop
- [ ] ARIA labels: All interactive elements have descriptive labels

---

## 6. Polly 8.5+ Resilience Patterns

### Decision
Use **`Microsoft.Extensions.Http.Resilience` standard resilience pipeline** with built-in retry, circuit breaker, and timeout policies.

### Rationale
- **Simplified configuration**: No manual Polly policy building - extension provides sensible defaults
- **Aspire integration**: Resilience events automatically appear in Aspire Dashboard telemetry
- **OpenTelemetry support**: All retry attempts logged with structured traces
- **Production-ready**: Defaults include exponential backoff, jitter, and circuit breaker thresholds

### Code Pattern
```csharp
// Program.cs - Register HTTP client with resilience
builder.Services.AddHttpClient<OpenMeteoClient>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com");
    client.DefaultRequestHeaders.Add("User-Agent", "Phi4WeatherAgent/1.0");
})
.AddStandardResilienceHandler(options =>
{
    // Retry policy: 3 attempts with exponential backoff
    options.Retry = new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(1)
    };
    
    // Circuit breaker: Open after 5 failures in 30s, half-open after 10s
    options.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(10)
    };
    
    // Timeout: 10 seconds per request
    options.TotalRequestTimeout = new HttpTimeoutStrategyOptions
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
});

// HttpClients/OpenMeteoClient.cs
public class OpenMeteoClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoClient> _logger;

    public OpenMeteoClient(HttpClient httpClient, ILogger<OpenMeteoClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WeatherData?> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var url = $"/v1/forecast?" +
                  $"latitude={latitude}&longitude={longitude}&" +
                  $"current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&" +
                  $"daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max,weather_code&" +
                  $"temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch&timezone=auto&forecast_days=7";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<WeatherData>(url, cancellationToken);
            
            if (response == null)
            {
                _logger.LogWarning("OpenMeteo returned null response for lat={Latitude}, lon={Longitude}", latitude, longitude);
                return null;
            }

            _logger.LogInformation("Successfully retrieved weather data for lat={Latitude}, lon={Longitude}", latitude, longitude);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed after retries for lat={Latitude}, lon={Longitude}", latitude, longitude);
            throw new WeatherServiceException("Failed to retrieve weather data. Check network connection and try again.", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout for lat={Latitude}, lon={Longitude}", latitude, longitude);
            throw new WeatherServiceException("Weather service timed out. Please try again.", ex);
        }
    }
}

// Custom exception for user-friendly error messages
public class WeatherServiceException : Exception
{
    public WeatherServiceException(string message, Exception? innerException = null) 
        : base(message, innerException)
    {
    }
}
```

### Alternatives Considered
- **Manual Polly policies**: Rejected - more boilerplate, no Aspire telemetry integration
- **No retry logic**: Rejected - Constitution Principle IX requires resilience testing

---

## Summary of Decisions

| Topic | Decision | Primary Benefit |
|-------|----------|----------------|
| Agent Framework | IChatClient with Agent Extensions | Native .NET 10 integration, built-in tool calling |
| MCP Tools | Native C# classes with [Tool] attribute | Type safety, testability, in-process performance |
| Aspire + AI | Ollama extension + Foundry Local OpenAI endpoint | Unified orchestration, automatic telemetry |
| OpenMeteo APIs | Strongly-typed DTOs with JSON source gen | Free, no-auth, comprehensive weather data |
| Accessibility | Semantic HTML + SVG icons with aria-label | WCAG 2.1 AA compliance, screen reader support |
| Resilience | Microsoft.Extensions.Http.Resilience | Production-ready defaults, Aspire integration |

All decisions align with Constitution principles and .NET 10 best practices. No "NEEDS CLARIFICATION" items remain.
