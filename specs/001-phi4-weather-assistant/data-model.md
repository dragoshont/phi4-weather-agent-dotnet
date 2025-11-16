# Data Model: Phi-4 Weather Assistant

**Date**: 2025-11-16  
**Phase**: 1 (Setup)  
**Purpose**: Define domain entities and C# type mappings for weather data

---

## Domain Entities

### 1. Location

Represents a geographic location returned from OpenMeteo Geocoding API.

**C# Type**:
```csharp
namespace Phi4WeatherAgent.Agent.Models;

public record Location
{
    /// <summary>
    /// GeoNames location ID
    /// </summary>
    public required int Id { get; init; }
    
    /// <summary>
    /// Location name (city/place name)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// WGS84 latitude (-90 to 90)
    /// </summary>
    public required double Latitude { get; init; }
    
    /// <summary>
    /// WGS84 longitude (-180 to 180)
    /// </summary>
    public required double Longitude { get; init; }
    
    /// <summary>
    /// Country name (e.g., "United States")
    /// </summary>
    public required string Country { get; init; }
    
    /// <summary>
    /// ISO 3166-1 alpha-2 country code (e.g., "US")
    /// </summary>
    public required string CountryCode { get; init; }
    
    /// <summary>
    /// State/province (e.g., "Washington")
    /// </summary>
    public string? Admin1 { get; init; }
    
    /// <summary>
    /// IANA timezone (e.g., "America/Los_Angeles")
    /// </summary>
    public required string Timezone { get; init; }
    
    /// <summary>
    /// Population count (if available)
    /// </summary>
    public int? Population { get; init; }
}
```

**Example Instance**:
```csharp
var seattle = new Location
{
    Id = 5809844,
    Name = "Seattle",
    Latitude = 47.6062,
    Longitude = -122.3321,
    Country = "United States",
    CountryCode = "US",
    Admin1 = "Washington",
    Timezone = "America/Los_Angeles",
    Population = 737015
};
```

---

### 2. WeatherData

Represents weather forecast data with current conditions and daily forecasts.

**C# Type**:
```csharp
namespace Phi4WeatherAgent.Agent.Models;

public record WeatherData
{
    /// <summary>
    /// Location coordinates
    /// </summary>
    public required double Latitude { get; init; }
    
    /// <summary>
    /// Location coordinates
    /// </summary>
    public required double Longitude { get; init; }
    
    /// <summary>
    /// Current weather conditions (first hourly entry)
    /// </summary>
    public required CurrentConditions Current { get; init; }
    
    /// <summary>
    /// Daily forecasts (up to 16 days, default 7)
    /// </summary>
    public required IReadOnlyList<DailyForecast> Daily { get; init; }
    
    /// <summary>
    /// IANA timezone for timestamps
    /// </summary>
    public required string Timezone { get; init; }
}

public record CurrentConditions
{
    /// <summary>
    /// Observation timestamp (ISO 8601)
    /// </summary>
    public required DateTimeOffset Time { get; init; }
    
    /// <summary>
    /// Air temperature at 2m (°C or °F based on units)
    /// </summary>
    public required double Temperature { get; init; }
    
    /// <summary>
    /// WMO weather code (0-99)
    /// </summary>
    public required int WeatherCode { get; init; }
    
    /// <summary>
    /// Human-readable weather description (derived from WeatherCode)
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// Total precipitation (mm or inches)
    /// </summary>
    public required double Precipitation { get; init; }
    
    /// <summary>
    /// Wind speed at 10m (km/h or mph)
    /// </summary>
    public required double WindSpeed { get; init; }
    
    /// <summary>
    /// Cloud cover percentage (0-100)
    /// </summary>
    public required int CloudCover { get; init; }
}

public record DailyForecast
{
    /// <summary>
    /// Date (ISO 8601, e.g., "2025-11-16")
    /// </summary>
    public required DateOnly Date { get; init; }
    
    /// <summary>
    /// Maximum temperature (°C or °F)
    /// </summary>
    public required double TemperatureMax { get; init; }
    
    /// <summary>
    /// Minimum temperature (°C or °F)
    /// </summary>
    public required double TemperatureMin { get; init; }
    
    /// <summary>
    /// Daily total precipitation (mm or inches)
    /// </summary>
    public required double PrecipitationSum { get; init; }
    
    /// <summary>
    /// Dominant weather code for the day
    /// </summary>
    public required int WeatherCode { get; init; }
    
    /// <summary>
    /// Human-readable weather description
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// Sunrise time (ISO 8601)
    /// </summary>
    public required DateTimeOffset Sunrise { get; init; }
    
    /// <summary>
    /// Sunset time (ISO 8601)
    /// </summary>
    public required DateTimeOffset Sunset { get; init; }
}
```

**Example Instance**:
```csharp
var weather = new WeatherData
{
    Latitude = 47.6062,
    Longitude = -122.3321,
    Timezone = "America/Los_Angeles",
    Current = new CurrentConditions
    {
        Time = DateTimeOffset.Parse("2025-11-16T10:00:00-08:00"),
        Temperature = 10.5,
        WeatherCode = 2,
        Description = "Partly cloudy",
        Precipitation = 0.1,
        WindSpeed = 12.5,
        CloudCover = 45
    },
    Daily = new[]
    {
        new DailyForecast
        {
            Date = new DateOnly(2025, 11, 16),
            TemperatureMax = 15.2,
            TemperatureMin = 8.1,
            PrecipitationSum = 2.5,
            WeatherCode = 61,
            Description = "Light rain",
            Sunrise = DateTimeOffset.Parse("2025-11-16T07:15:00-08:00"),
            Sunset = DateTimeOffset.Parse("2025-11-16T16:45:00-08:00")
        }
    }
};
```

---

### 3. AllergenData

Represents pollen/allergen forecast data from Air Quality API.

**C# Type**:
```csharp
namespace Phi4WeatherAgent.Agent.Models;

public record AllergenData
{
    /// <summary>
    /// Location coordinates
    /// </summary>
    public required double Latitude { get; init; }
    
    /// <summary>
    /// Location coordinates
    /// </summary>
    public required double Longitude { get; init; }
    
    /// <summary>
    /// Pollen levels by type
    /// </summary>
    public required PollenLevels Pollen { get; init; }
    
    /// <summary>
    /// Overall severity rating
    /// </summary>
    public required AllergySeverity Severity { get; init; }
    
    /// <summary>
    /// IANA timezone for timestamps
    /// </summary>
    public required string Timezone { get; init; }
    
    /// <summary>
    /// Geographic constraint flag (Europe only)
    /// </summary>
    public required bool IsEuropeRegion { get; init; }
}

public record PollenLevels
{
    /// <summary>
    /// Alder pollen (grains/m³)
    /// </summary>
    public double? Alder { get; init; }
    
    /// <summary>
    /// Birch pollen (grains/m³)
    /// </summary>
    public double? Birch { get; init; }
    
    /// <summary>
    /// Grass pollen (grains/m³)
    /// </summary>
    public double? Grass { get; init; }
    
    /// <summary>
    /// Mugwort pollen (grains/m³)
    /// </summary>
    public double? Mugwort { get; init; }
    
    /// <summary>
    /// Olive pollen (grains/m³)
    /// </summary>
    public double? Olive { get; init; }
    
    /// <summary>
    /// Ragweed pollen (grains/m³)
    /// </summary>
    public double? Ragweed { get; init; }
}

public enum AllergySeverity
{
    /// <summary>
    /// 0-20 grains/m³ (any pollen type)
    /// </summary>
    Low,
    
    /// <summary>
    /// 21-50 grains/m³
    /// </summary>
    Moderate,
    
    /// <summary>
    /// 51-100 grains/m³
    /// </summary>
    High,
    
    /// <summary>
    /// >100 grains/m³
    /// </summary>
    VeryHigh,
    
    /// <summary>
    /// No data available (outside Europe or pollen season)
    /// </summary>
    Unknown
}
```

**Example Instance**:
```csharp
var allergen = new AllergenData
{
    Latitude = 48.8566, // Paris, France
    Longitude = 2.3522,
    Timezone = "Europe/Paris",
    IsEuropeRegion = true,
    Pollen = new PollenLevels
    {
        Grass = 34.8,
        Birch = 12.5,
        Ragweed = 0.0,
        Alder = null, // Out of season
        Mugwort = null,
        Olive = null
    },
    Severity = AllergySeverity.Moderate
};
```

---

### 4. ChatMessage (Structured Data)

Represents a chat message with optional structured weather/allergen data.

**C# Type**:
```csharp
namespace Phi4WeatherAgent.Agent.Models;

public record ChatMessage
{
    /// <summary>
    /// Message unique identifier
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// Message role (user, assistant, system)
    /// </summary>
    public required ChatRole Role { get; init; }
    
    /// <summary>
    /// Text content
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Timestamp (UTC)
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
    
    /// <summary>
    /// Structured weather data (if applicable)
    /// </summary>
    public WeatherData? Weather { get; init; }
    
    /// <summary>
    /// Structured allergen data (if applicable)
    /// </summary>
    public AllergenData? Allergen { get; init; }
    
    /// <summary>
    /// MCP tool invocation metadata
    /// </summary>
    public ToolInvocation? ToolCall { get; init; }
}

public enum ChatRole
{
    User,
    Assistant,
    System,
    Tool
}

public record ToolInvocation
{
    /// <summary>
    /// MCP tool name (e.g., "geocode_location")
    /// </summary>
    public required string ToolName { get; init; }
    
    /// <summary>
    /// Tool arguments (JSON serialized)
    /// </summary>
    public required string Arguments { get; init; }
    
    /// <summary>
    /// Tool result (JSON serialized)
    /// </summary>
    public string? Result { get; init; }
}
```

**Example Instance**:
```csharp
var message = new ChatMessage
{
    Id = Guid.NewGuid(),
    Role = ChatRole.Assistant,
    Content = "The weather in Seattle is currently 52°F and partly cloudy. High of 58°F, low of 45°F.",
    Timestamp = DateTimeOffset.UtcNow,
    Weather = seattleWeather, // WeatherData instance
    ToolCall = new ToolInvocation
    {
        ToolName = "get_weather_forecast",
        Arguments = "{\"latitude\": 47.6062, \"longitude\": -122.3321, \"forecastDays\": 7}",
        Result = "{\"current\": {...}, \"daily\": [...]}"
    }
};
```

---

## WMO Weather Code Mapping

Standard weather code descriptions for UI rendering:

```csharp
public static class WeatherCodeMapper
{
    public static string GetDescription(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Fog",
        48 => "Depositing rime fog",
        51 => "Light drizzle",
        53 => "Moderate drizzle",
        55 => "Dense drizzle",
        61 => "Light rain",
        63 => "Moderate rain",
        65 => "Heavy rain",
        71 => "Light snow",
        73 => "Moderate snow",
        75 => "Heavy snow",
        80 => "Light rain showers",
        81 => "Moderate rain showers",
        82 => "Violent rain showers",
        95 => "Thunderstorm",
        96 => "Thunderstorm with light hail",
        99 => "Thunderstorm with heavy hail",
        _ => "Unknown"
    };
}
```

---

## API Response Mapping

### OpenMeteo Geocoding → Location

```csharp
// JSON response from API
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

// Maps to Location record
new Location
{
    Id = json["id"],
    Name = json["name"],
    Latitude = json["latitude"],
    Longitude = json["longitude"],
    Country = json["country"],
    CountryCode = json["country_code"],
    Admin1 = json["admin1"],
    Timezone = json["timezone"],
    Population = json["population"]
}
```

### OpenMeteo Weather → WeatherData

```csharp
// JSON response from API
{
  "latitude": 47.6062,
  "longitude": -122.3321,
  "timezone": "America/Los_Angeles",
  "hourly": {
    "time": ["2025-11-16T00:00", "2025-11-16T01:00", ...],
    "temperature_2m": [10.5, 10.2, ...],
    "weather_code": [2, 2, ...]
  },
  "daily": {
    "time": ["2025-11-16", "2025-11-17", ...],
    "temperature_2m_max": [15.2, 16.8, ...],
    "temperature_2m_min": [8.1, 9.4, ...]
  }
}

// Maps to WeatherData record
new WeatherData
{
    Latitude = json["latitude"],
    Longitude = json["longitude"],
    Timezone = json["timezone"],
    Current = new CurrentConditions
    {
        Time = json["hourly"]["time"][0],
        Temperature = json["hourly"]["temperature_2m"][0],
        WeatherCode = json["hourly"]["weather_code"][0],
        Description = WeatherCodeMapper.GetDescription(json["hourly"]["weather_code"][0]),
        // ... other fields from hourly[0]
    },
    Daily = json["daily"]["time"]
        .Zip(json["daily"]["temperature_2m_max"], ...)
        .Select(day => new DailyForecast { ... })
        .ToList()
}
```

### OpenMeteo Air Quality → AllergenData

```csharp
// JSON response from API
{
  "latitude": 48.8566,
  "longitude": 2.3522,
  "timezone": "Europe/Paris",
  "hourly": {
    "time": ["2025-11-16T00:00", ...],
    "grass_pollen": [34.8, 38.2, ...],
    "birch_pollen": [12.5, 14.1, ...]
  }
}

// Maps to AllergenData record
new AllergenData
{
    Latitude = json["latitude"],
    Longitude = json["longitude"],
    Timezone = json["timezone"],
    IsEuropeRegion = IsInEurope(latitude, longitude),
    Pollen = new PollenLevels
    {
        Grass = json["hourly"]["grass_pollen"]?.FirstOrDefault(),
        Birch = json["hourly"]["birch_pollen"]?.FirstOrDefault(),
        // ... other pollen types
    },
    Severity = CalculateSeverity(pollen)
}
```

---

## Validation Rules

### Location
- `Latitude`: -90 to 90
- `Longitude`: -180 to 180
- `Name`: Non-empty string

### WeatherData
- `Daily`: 1-16 forecasts (per OpenMeteo limit)
- `WeatherCode`: 0-99 (WMO standard)
- `Temperature`: Reasonable range (-50°C to 60°C)

### AllergenData
- `IsEuropeRegion`: `true` if lat/lon in Europe bounding box (approx 35-71°N, -10-40°E)
- `Severity`: Calculated from max pollen value across all types

---

## Next Steps

- **T011**: Define MCP tool contracts using these entities
- **T024-T026**: Implement these models in `src/Phi4WeatherAgent.Agent/Models/`
- **T033-T034**: Use models in HTTP client deserialization
- **T038-T048**: Bind models to Blazor UI components
