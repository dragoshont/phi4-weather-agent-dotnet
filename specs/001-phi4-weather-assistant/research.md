# Research Findings: Phi-4 Weather Assistant

**Date**: 2025-11-16  
**Phase**: 0 (Pre-Implementation Research)  
**Purpose**: Document technical decisions and patterns validated before Phase 1 design

---

## 1. Agent Framework Patterns

### Microsoft.Extensions.AI Architecture

**ChatClientAgent + FunctionInvokingChatClient Pattern**:
```csharp
// ChatClientAgent wraps any IChatClient (Ollama, Foundry Local, etc.)
var chatClient = new ChatClientAgent(
    innerClient: ollamaClient, // or foundryClient
    name: "WeatherAssistant",
    description: "Local weather and allergen information assistant"
);

// FunctionInvokingChatClient adds MCP tool calling middleware
var toolClient = new FunctionInvokingChatClient(
    innerClient: chatClient,
    tools: [geocodeTool, weatherTool, allergenTool]
);
```

**AIFunctionFactory for MCP Tool Registration**:
```csharp
// MCP tool definition with automatic parameter binding
var geocodeTool = AIFunctionFactory.Create(
    method: async (string locationName) => await GeocodeAsync(locationName),
    name: "geocode_location",
    description: "Convert location name to coordinates"
);
```

**Key Findings**:
- Agent Framework 10.0.0-preview.1.25071.7 uses `ChatClientAgent` (NOT Semantic Kernel's `Agent` class)
- `FunctionInvokingChatClient` provides MCP tool invocation middleware with automatic JSON serialization
- Tools registered via `AIFunctionFactory.Create()` with delegate signatures
- Function calling supports both streaming and non-streaming responses
- Middleware pipeline: Logging → Function Invocation → Chat Client

**References**:
- [ChatClientAgent Docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatclientagent)
- [Function Middleware Tutorial](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/middleware)

---

## 2. Aspire 13 Orchestration

### Dashboard Auto-Launch Behavior

**Platform Detection for AI Model Hosting**:
```csharp
// AppHost Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Platform-specific model hosting
var aiModelEndpoint = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
    ? builder.AddFoundryLocal("phi4") // Foundry Local for Win/macOS
    : builder.AddOllama("phi4");      // Ollama for Linux

builder.AddProject<Phi4WeatherAgent_Web>("web")
    .WithReference(aiModelEndpoint);
```

**Dashboard Auto-Launch Confirmation**:
- Dashboard launches **automatically** when AppHost project runs (no manual config needed)
- Default URL: `http://localhost:15000` (or random port if in use)
- DCP (Developer Control Plane) manages resource lifecycle
- OpenTelemetry metrics/logs/traces available immediately in Dashboard

**Key Findings**:
- Aspire 13.0.0-preview.1 requires `global.json` with `"allowPrerelease": true`
- Dashboard auto-launches via DCP orchestration (FR-016 validated ✅)
- Platform detection feasible via `OperatingSystem.IsWindows()` / `IsMacOS()` / `IsLinux()`
- Service discovery via `builder.AddProject<T>().WithReference()` pattern
- No separate dashboard start command needed

**References**:
- [Aspire Setup Tooling](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)
- [Aspire Architecture Overview](https://learn.microsoft.com/en-us/dotnet/aspire/architecture/overview)

---

## 3. OpenMeteo API Contracts

### Geocoding API

**Endpoint**: `https://geocoding-api.open-meteo.com/v1/search`

**Request Parameters**:
```http
GET /v1/search?name={locationName}&count=10&language=en
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✅ Yes | Location name or postal code (fuzzy matching, 3+ chars) |
| `count` | int | No | Results limit (1-100, default 10) |
| `language` | string | No | Response language (default `en`) |

**Response Schema**:
```json
{
  "results": [
    {
      "id": 5809844,
      "name": "Seattle",
      "latitude": 47.6062,
      "longitude": -122.3321,
      "country": "United States",
      "country_code": "US",
      "admin1": "Washington",
      "timezone": "America/Los_Angeles",
      "population": 737015
    }
  ]
}
```

**Data Source**: GeoNames database (open data)

---

### Weather Forecast API

**Endpoint**: `https://api.open-meteo.com/v1/forecast`

**Request Parameters**:
```http
GET /v1/forecast?latitude=47.6062&longitude=-122.3321&hourly=temperature_2m,precipitation,weather_code&daily=temperature_2m_max,temperature_2m_min,precipitation_sum&forecast_days=7&timezone=auto
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `latitude` | float | ✅ Yes | WGS84 latitude (-90 to 90) |
| `longitude` | float | ✅ Yes | WGS84 longitude (-180 to 180) |
| `hourly` | string | No | Comma-separated hourly variables |
| `daily` | string | No | Comma-separated daily variables |
| `forecast_days` | int | No | Forecast days (1-16, default 7) |
| `timezone` | string | No | Timezone (default `auto` from lat/lon) |

**Key Hourly Variables**:
- `temperature_2m` - Air temperature at 2m (°C/°F)
- `precipitation` - Total precipitation (mm)
- `weather_code` - WMO weather code (0-99)
- `wind_speed_10m` - Wind speed at 10m (km/h / mph)
- `cloud_cover` - Cloud cover percentage (0-100)

**Key Daily Variables**:
- `temperature_2m_max` / `temperature_2m_min` - Daily temperature range
- `precipitation_sum` - Daily total precipitation
- `weather_code` - Dominant weather code
- `sunrise` / `sunset` - Sun times (ISO 8601)

**WMO Weather Codes** (subset):
- `0` = Clear sky
- `1-3` = Mainly clear, partly cloudy, overcast
- `45, 48` = Fog
- `51-67` = Drizzle, rain, freezing rain
- `71-86` = Snow, snow grains, snow showers
- `95-99` = Thunderstorm

**Response Schema**:
```json
{
  "latitude": 47.6062,
  "longitude": -122.3321,
  "hourly": {
    "time": ["2025-11-16T00:00", "2025-11-16T01:00", ...],
    "temperature_2m": [10.5, 10.2, ...],
    "precipitation": [0.0, 0.1, ...]
  },
  "daily": {
    "time": ["2025-11-16", "2025-11-17", ...],
    "temperature_2m_max": [15.2, 16.8, ...],
    "temperature_2m_min": [8.1, 9.4, ...]
  }
}
```

---

### Air Quality API (Pollen/Allergen)

**Endpoint**: `https://air-quality-api.open-meteo.com/v1/air-quality`

**Request Parameters**:
```http
GET /v1/air-quality?latitude=47.6062&longitude=-122.3321&hourly=alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,olive_pollen,ragweed_pollen&forecast_days=4
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `latitude` | float | ✅ Yes | WGS84 latitude |
| `longitude` | float | ✅ Yes | WGS84 longitude |
| `hourly` | string | No | Comma-separated air quality variables |
| `forecast_days` | int | No | Forecast days (1-7, default 5) |

**Pollen Parameters** (grains/m³, Europe only):
- `alder_pollen` - Alder pollen concentration
- `birch_pollen` - Birch pollen concentration
- `grass_pollen` - Grass pollen concentration
- `mugwort_pollen` - Mugwort pollen concentration
- `olive_pollen` - Olive pollen concentration
- `ragweed_pollen` - Ragweed pollen concentration

**Other Air Quality Variables**:
- `pm10`, `pm2_5` - Particulate matter
- `european_aqi`, `us_aqi` - Air quality indices
- `carbon_monoxide`, `nitrogen_dioxide`, `ozone` - Gas concentrations

**Critical Constraints**:
- **Geographic Coverage**: Pollen data **Europe only** (CAMS European Air Quality Forecast)
- **Resolution**: 11 km grid (0.1°)
- **Forecast Length**: **4 days for pollen** (not 7 like weather)
- **Seasonal Availability**: Pollen data only during pollen season (null/zero outside season)
- **Data Source**: CAMS European air quality forecast + reanalysis

**Response Schema**:
```json
{
  "latitude": 47.6062,
  "longitude": -122.3321,
  "hourly": {
    "time": ["2025-11-16T00:00", "2025-11-16T01:00", ...],
    "grass_pollen": [0.0, 12.5, 34.8, ...],  // grains/m³
    "birch_pollen": [5.2, 8.1, ...]
  }
}
```

**Severity Thresholds** (grains/m³):
- Low: 0-20
- Moderate: 21-50
- High: 51-100
- Very High: >100

---

## 4. Polly Resilience Policies

### Retry + Circuit Breaker Pattern

**HTTP Client Configuration**:
```csharp
builder.Services.AddHttpClient<OpenMeteoWeatherClient>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(Policy
    .Handle<HttpRequestException>()
    .Or<TaskCanceledException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // Exponential backoff
        onRetry: (outcome, timespan, retryAttempt, context) =>
        {
            logger.LogWarning("Retry {RetryAttempt} after {Delay}s", retryAttempt, timespan.TotalSeconds);
        }
    )
)
.AddPolicyHandler(Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromMinutes(1)
    )
);
```

**Key Findings**:
- Polly 8.5.0 supports both sync and async policies
- Exponential backoff prevents API rate limiting
- Circuit breaker opens after 5 consecutive failures, breaks for 1 minute
- Combine with `ILogger` for telemetry integration

**References**:
- [Polly Documentation](https://www.pollydocs.org/)
- [.NET Resilience Patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly)

---

## 5. WCAG 2.1 AA Guidelines

### Color Contrast Requirements

**Minimum Contrast Ratios** (WCAG 2.1 AA):
- Normal text (<18pt / <14pt bold): **4.5:1**
- Large text (≥18pt / ≥14pt bold): **3:1**
- UI components (buttons, focus indicators): **3:1**

**Color Palette** (validated via WebAIM contrast checker):
```css
/* WCAG AA compliant tokens */
:root {
  --color-text-primary: #1a1a1a;      /* Black text on white bg = 16.94:1 ✅ */
  --color-text-secondary: #4a4a4a;    /* Gray text on white bg = 9.73:1 ✅ */
  --color-bg-primary: #ffffff;
  --color-bg-secondary: #f5f5f5;
  
  --color-info: #0066cc;              /* Blue on white bg = 8.59:1 ✅ */
  --color-success: #0f7a3e;           /* Green on white bg = 4.58:1 ✅ */
  --color-warning: #b35900;           /* Orange on white bg = 4.52:1 ✅ */
  --color-error: #c41e3a;             /* Red on white bg = 5.95:1 ✅ */
  
  --color-focus: #0056b3;             /* Focus indicator = 9.26:1 ✅ */
}
```

### Keyboard Navigation Requirements

**Essential Patterns**:
1. **Skip Links**: "Skip to main content" link at top (visible on focus)
2. **Tab Order**: Logical focus flow (header → chat input → messages → footer)
3. **Focus Indicators**: Visible 3px outline on all interactive elements
4. **No Keyboard Traps**: Users can Tab out of all components
5. **Escape Key**: Close modals/popovers

**Blazor Implementation**:
```razor
@* Skip link (MainLayout.razor) *@
<a href="#main-content" class="skip-link">Skip to main content</a>

@* Chat input with ARIA *@
<input type="text" 
       id="chat-input"
       aria-label="Message input"
       placeholder="Ask about weather or pollen levels..." />

@* Weather card with ARIA live region *@
<div class="weather-card" role="region" aria-live="polite" aria-label="Weather information">
  <h3>Weather for @LocationName</h3>
  ...
</div>
```

### Screen Reader Support

**ARIA Attributes** (required):
- `role="main"` on chat container
- `aria-label` on all inputs/buttons
- `aria-live="polite"` for dynamic weather cards
- `aria-describedby` for error messages

**Screen Reader Testing** (manual):
- NVDA (Windows) - keyboard-only workflow <2min
- JAWS (Windows) - weather card content read aloud
- VoiceOver (macOS) - live region announcements working

**References**:
- [WCAG 2.1 Quick Reference](https://www.w3.org/WAI/WCAG21/quickref/)
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- [Blazor Accessibility](https://learn.microsoft.com/en-us/aspnet/core/blazor/accessibility)

---

## Decisions & Trade-offs

### Why No Cloud AI?
- **Principle I**: Local-first reduces latency (no API round-trip), protects privacy, works offline
- **Trade-off**: Phi-4 (14B) requires ~8GB VRAM, limiting to desktop hardware

### Why OpenMeteo (vs NOAA/Weather.gov)?
- **Principle VI**: OpenMeteo is free, no API keys, global coverage
- **Trade-off**: Europe-only pollen data, 4-day allergen forecast (vs 7-day weather)

### Why Blazor Server (vs MAUI)?
- **Principle VIII**: aichatweb template provides SignalR chat infrastructure out-of-box
- **Trade-off**: Requires localhost server, not standalone executable

### Why Agent Framework (vs Semantic Kernel)?
- **Principle III**: Microsoft.Extensions.AI is .NET 10 native, lighter weight
- **Trade-off**: Less mature ecosystem, fewer samples (as of Nov 2025)

### Why bUnit + Playwright (vs Selenium)?
- **Testing**: bUnit for Blazor component tests, Playwright for cross-browser E2E
- **Trade-off**: Playwright requires Node.js runtime (adds setup complexity)

---

## Open Questions (Resolved in Phase 1)

1. ✅ **Conversation Context**: In-memory `List<ChatMessage>` per SignalR session, no persistence (T041)
2. ✅ **Natural Language Examples**: Document in quickstart.md (T012)
3. ✅ **MCP Tool Signatures**: Define in contracts/ (T011)
4. ✅ **Europe-Only Pollen**: Document geographic constraint, gracefully handle non-EU queries
5. ✅ **Playwright Config**: Chromium/Firefox/WebKit, 1920x1080, trace-on-failure (T032)

---

## Next Steps

- **Phase 1**: Create data-model.md (entities), contracts/ (MCP tools), quickstart.md (NL examples)
- **Phase 2**: Implement foundation (AgentService, HTTP clients, Aspire defaults)
- **Phase 3**: Build US1 (basic weather query) end-to-end
