# MCP Tool Contracts

**Date**: 2025-11-16  
**Phase**: 1 (Setup)  
**Purpose**: Define MCP tool signatures and HTTP endpoint contracts for OpenMeteo APIs

---

## MCP Tool Definitions

### 1. GeocodeTool

Converts location name to geographic coordinates using OpenMeteo Geocoding API.

**Function Signature**:
```csharp
public async Task<Location[]> GeocodeLocationAsync(
    string locationName,
    int count = 10
)
```

**Parameters**:
| Name | Type | Required | Description | Validation |
|------|------|----------|-------------|------------|
| `locationName` | `string` | ✅ Yes | Location name or postal code | Non-empty, 3+ chars for fuzzy match |
| `count` | `int` | No | Max results to return | 1-100, default 10 |

**Return Type**: `Location[]` (array of Location records from data-model.md)

**MCP Annotation**:
```csharp
[Description("Convert location name to geographic coordinates")]
public async Task<Location[]> GeocodeLocationAsync(
    [Description("Location name or postal code (e.g., 'Seattle', 'Paris, France', '98101')")]
    string locationName,
    
    [Description("Maximum number of results (1-100)")]
    int count = 10
)
{
    // Implementation in OpenMeteoGeocodeClient
}
```

**HTTP Endpoint Contract**:
```http
GET https://geocoding-api.open-meteo.com/v1/search?name={locationName}&count={count}&language=en
```

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
  ],
  "generationtime_ms": 0.123
}
```

**Error Handling**:
- **No results found**: Return empty array `[]`
- **HTTP 4xx/5xx**: Throw `HttpRequestException` (caught by Polly retry)
- **Malformed response**: Throw `JsonException`

**Example Usage**:
```csharp
// User asks: "What's the weather in Seattle?"
var locations = await GeocodeLocationAsync("Seattle", count: 10);
var firstMatch = locations.FirstOrDefault(); // Seattle, WA, USA
```

---

### 2. WeatherTool

Retrieves weather forecast for given coordinates using OpenMeteo Weather API.

**Function Signature**:
```csharp
public async Task<WeatherData> GetWeatherForecastAsync(
    double latitude,
    double longitude,
    int forecastDays = 7
)
```

**Parameters**:
| Name | Type | Required | Description | Validation |
|------|------|----------|-------------|------------|
| `latitude` | `double` | ✅ Yes | WGS84 latitude | -90 to 90 |
| `longitude` | `double` | ✅ Yes | WGS84 longitude | -180 to 180 |
| `forecastDays` | `int` | No | Forecast duration | 1-16, default 7 |

**Return Type**: `WeatherData` (record from data-model.md)

**MCP Annotation**:
```csharp
[Description("Get weather forecast for coordinates")]
public async Task<WeatherData> GetWeatherForecastAsync(
    [Description("WGS84 latitude (-90 to 90)")]
    double latitude,
    
    [Description("WGS84 longitude (-180 to 180)")]
    double longitude,
    
    [Description("Number of forecast days (1-16)")]
    int forecastDays = 7
)
{
    // Implementation in OpenMeteoWeatherClient
}
```

**HTTP Endpoint Contract**:
```http
GET https://api.open-meteo.com/v1/forecast
  ?latitude={latitude}
  &longitude={longitude}
  &hourly=temperature_2m,precipitation,weather_code,wind_speed_10m,cloud_cover
  &daily=temperature_2m_max,temperature_2m_min,precipitation_sum,weather_code,sunrise,sunset
  &forecast_days={forecastDays}
  &timezone=auto
```

**Response Schema**:
```json
{
  "latitude": 47.6062,
  "longitude": -122.3321,
  "timezone": "America/Los_Angeles",
  "timezone_abbreviation": "PST",
  "elevation": 56.0,
  "hourly_units": {
    "temperature_2m": "°F",
    "precipitation": "inch"
  },
  "hourly": {
    "time": ["2025-11-16T00:00", "2025-11-16T01:00", ...],
    "temperature_2m": [52.0, 51.5, ...],
    "precipitation": [0.0, 0.02, ...],
    "weather_code": [2, 2, 3, ...],
    "wind_speed_10m": [8.5, 9.2, ...],
    "cloud_cover": [45, 50, ...]
  },
  "daily_units": {
    "temperature_2m_max": "°F"
  },
  "daily": {
    "time": ["2025-11-16", "2025-11-17", ...],
    "temperature_2m_max": [58.0, 60.5, ...],
    "temperature_2m_min": [45.0, 47.2, ...],
    "precipitation_sum": [0.5, 0.0, ...],
    "weather_code": [61, 2, ...],
    "sunrise": ["2025-11-16T07:15:00", ...],
    "sunset": ["2025-11-16T16:45:00", ...]
  }
}
```

**Error Handling**:
- **Invalid coordinates**: HTTP 400 (validate before calling)
- **HTTP 5xx**: Polly retry with exponential backoff
- **Network timeout**: After 30s, throw `TaskCanceledException`

**Example Usage**:
```csharp
// After geocoding "Seattle" → lat=47.6062, lon=-122.3321
var weather = await GetWeatherForecastAsync(47.6062, -122.3321, forecastDays: 7);
// Returns WeatherData with current conditions + 7-day forecast
```

---

### 3. AllergenTool

Retrieves pollen/allergen levels for given coordinates using OpenMeteo Air Quality API.

**Function Signature**:
```csharp
public async Task<AllergenData> GetAllergenLevelsAsync(
    double latitude,
    double longitude
)
```

**Parameters**:
| Name | Type | Required | Description | Validation |
|------|------|----------|-------------|------------|
| `latitude` | `double` | ✅ Yes | WGS84 latitude | -90 to 90 |
| `longitude` | `double` | ✅ Yes | WGS84 longitude | -180 to 180 |

**Return Type**: `AllergenData` (record from data-model.md)

**MCP Annotation**:
```csharp
[Description("Get pollen and allergen levels for coordinates")]
public async Task<AllergenData> GetAllergenLevelsAsync(
    [Description("WGS84 latitude (-90 to 90)")]
    double latitude,
    
    [Description("WGS84 longitude (-180 to 180)")]
    double longitude
)
{
    // Implementation in OpenMeteoAllergenClient
}
```

**HTTP Endpoint Contract**:
```http
GET https://air-quality-api.open-meteo.com/v1/air-quality
  ?latitude={latitude}
  &longitude={longitude}
  &hourly=alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,olive_pollen,ragweed_pollen
  &forecast_days=4
  &timezone=auto
```

**Response Schema**:
```json
{
  "latitude": 48.8566,
  "longitude": 2.3522,
  "timezone": "Europe/Paris",
  "timezone_abbreviation": "CET",
  "elevation": 42.0,
  "hourly_units": {
    "grass_pollen": "grains/m³",
    "birch_pollen": "grains/m³"
  },
  "hourly": {
    "time": ["2025-11-16T00:00", "2025-11-16T01:00", ...],
    "alder_pollen": [0.0, 0.0, ...],
    "birch_pollen": [12.5, 14.1, ...],
    "grass_pollen": [34.8, 38.2, ...],
    "mugwort_pollen": [0.0, 0.0, ...],
    "olive_pollen": [null, null, ...],
    "ragweed_pollen": [2.1, 2.3, ...]
  }
}
```

**Geographic Constraints**:
- **Europe Only**: Pollen data available for coordinates in Europe (approx 35-71°N, -10-40°E)
- **Outside Europe**: API returns `null` for pollen values → set `IsEuropeRegion = false`, `Severity = Unknown`

**Seasonal Constraints**:
- **Pollen Season**: Data available during pollen season (spring/summer)
- **Off-Season**: API returns `0.0` or `null` → handle gracefully, display "No current pollen data"

**Error Handling**:
- **Non-Europe coordinates**: Return `AllergenData` with `IsEuropeRegion = false`, all pollen null
- **Off-season**: Return `AllergenData` with `Severity = Unknown`, pollen values 0 or null
- **HTTP 5xx**: Polly retry, then graceful degradation message

**Example Usage**:
```csharp
// After geocoding "Paris" → lat=48.8566, lon=2.3522
var allergen = await GetAllergenLevelsAsync(48.8566, 2.3522);
// Returns AllergenData with pollen levels (Europe region)

// After geocoding "Seattle" → lat=47.6062, lon=-122.3321
var allergen = await GetAllergenLevelsAsync(47.6062, -122.3321);
// Returns AllergenData with IsEuropeRegion=false, pollen null (not in Europe)
```

---

## MCP Tool Registration

Tools are registered in `src/Phi4WeatherAgent.Agent/Program.cs` using `AIFunctionFactory`:

```csharp
// Register MCP tools
var geocodeTool = AIFunctionFactory.Create(
    method: geocodeClient.GeocodeLocationAsync,
    name: "geocode_location",
    description: "Convert location name to geographic coordinates"
);

var weatherTool = AIFunctionFactory.Create(
    method: weatherClient.GetWeatherForecastAsync,
    name: "get_weather_forecast",
    description: "Get weather forecast for coordinates"
);

var allergenTool = AIFunctionFactory.Create(
    method: allergenClient.GetAllergenLevelsAsync,
    name: "get_allergen_levels",
    description: "Get pollen and allergen levels for coordinates"
);

// Add to FunctionInvokingChatClient
var toolClient = new FunctionInvokingChatClient(
    innerClient: chatClient,
    tools: new[] { geocodeTool, weatherTool, allergenTool }
);
```

---

## Tool Invocation Flow

Typical multi-tool workflow for weather query:

```mermaid
graph TD
    A[User: "What's the weather in Seattle?"] --> B[Agent Framework]
    B --> C[GeocodeTool: locationName="Seattle"]
    C --> D[OpenMeteo Geocoding API]
    D --> E[Location: lat=47.6062, lon=-122.3321]
    E --> F[WeatherTool: lat=47.6062, lon=-122.3321]
    F --> G[OpenMeteo Weather API]
    G --> H[WeatherData: current + 7-day forecast]
    H --> I[Agent generates response]
    I --> J[Blazor UI renders WeatherCard]
```

---

## Testing Contracts

### Unit Tests (T042, T043)

```csharp
[Fact]
public async Task GeocodeLocationAsync_ValidLocation_ReturnsResults()
{
    // Arrange
    var client = new OpenMeteoGeocodeClient(httpClient, logger);
    
    // Act
    var results = await client.GeocodeLocationAsync("Seattle");
    
    // Assert
    Assert.NotEmpty(results);
    Assert.Contains(results, loc => loc.Name == "Seattle" && loc.CountryCode == "US");
}

[Fact]
public async Task GetWeatherForecastAsync_ValidCoordinates_ReturnsWeatherData()
{
    // Arrange
    var client = new OpenMeteoWeatherClient(httpClient, logger);
    
    // Act
    var weather = await client.GetWeatherForecastAsync(47.6062, -122.3321);
    
    // Assert
    Assert.Equal(47.6062, weather.Latitude, precision: 2);
    Assert.NotNull(weather.Current);
    Assert.InRange(weather.Daily.Count, 1, 7);
}

[Fact]
public async Task GetAllergenLevelsAsync_EuropeCoordinates_ReturnsPollenData()
{
    // Arrange
    var client = new OpenMeteoAllergenClient(httpClient, logger);
    
    // Act
    var allergen = await client.GetAllergenLevelsAsync(48.8566, 2.3522); // Paris
    
    // Assert
    Assert.True(allergen.IsEuropeRegion);
    Assert.NotNull(allergen.Pollen.Grass);
}
```

---

## Next Steps

- **T033-T034**: Implement HTTP clients using these contracts
- **T036-T037**: Build MCP tools with AIFunctionFactory
- **T046-T047**: Add allergen tool implementation
- **T042-T043**: Write unit tests validating contracts
