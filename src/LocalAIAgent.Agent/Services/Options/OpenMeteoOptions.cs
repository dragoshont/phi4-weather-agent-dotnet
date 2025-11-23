namespace LocalAIAgent.Agent.Services.Options;

/// <summary>
/// Configuration options for OpenMeteo API clients.
/// </summary>
public class OpenMeteoOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "OpenMeteo";

    /// <summary>
    /// Geocoding API base URL
    /// </summary>
    public string GeocodeBaseUrl { get; set; } = "https://geocoding-api.open-meteo.com";

    /// <summary>
    /// Weather Forecast API base URL
    /// </summary>
    public string WeatherBaseUrl { get; set; } = "https://api.open-meteo.com";

    /// <summary>
    /// Air Quality API base URL (for pollen data)
    /// </summary>
    public string AirQualityBaseUrl { get; set; } = "https://air-quality-api.open-meteo.com";

    /// <summary>
    /// Default request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for transient failures
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
