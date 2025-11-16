namespace Phi4WeatherAgent.Agent.Models;

/// <summary>
/// Represents weather forecast data with current conditions and daily forecasts.
/// </summary>
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

/// <summary>
/// Current weather conditions.
/// </summary>
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

/// <summary>
/// Daily weather forecast.
/// </summary>
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
