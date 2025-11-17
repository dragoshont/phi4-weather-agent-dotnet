namespace Phi4WeatherAgent.Agent.Registry;

/// <summary>
/// Classification for tool allowlist filtering.
/// </summary>
public enum SecurityClass
{
    /// <summary>
    /// Available to all users (e.g., GetWeather, GetForecast).
    /// </summary>
    Public = 0,

    /// <summary>
    /// Requires authentication (e.g., ReadUserProfile).
    /// </summary>
    Internal = 1,

    /// <summary>
    /// Requires admin role (e.g., DeleteAllData).
    /// </summary>
    Admin = 2
}
