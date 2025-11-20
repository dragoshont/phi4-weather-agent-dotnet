using System.ComponentModel;
using Microsoft.Extensions.AI;
using Phi4WeatherAgent.Agent.Models;
using Phi4WeatherAgent.Agent.Services;

namespace Phi4WeatherAgent.Agent.Tools;

/// <summary>
/// MCP tool for retrieving weather forecasts.
/// Uses OpenMeteo Weather API with current conditions and daily forecasts.
/// </summary>
public class WeatherTool
{
    private readonly OpenMeteoWeatherClient _weatherClient;
    private readonly ILogger<WeatherTool> _logger;

    public WeatherTool(OpenMeteoWeatherClient weatherClient, ILogger<WeatherTool> logger)
    {
        _weatherClient = weatherClient ?? throw new ArgumentNullException(nameof(weatherClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get weather forecast for geographic coordinates.
    /// </summary>
    /// <param name="latitude">WGS84 latitude (-90 to 90)</param>
    /// <param name="longitude">WGS84 longitude (-180 to 180)</param>
    /// <param name="forecastDays">Number of forecast days (1-16, default 7)</param>
    /// <returns>Weather data with current conditions and daily forecasts</returns>
    [Description("Get weather forecast for coordinates")]
    public async Task<WeatherData> GetWeatherForecastAsync(
        [Description("WGS84 latitude (-90 to 90)")] double latitude,
        [Description("WGS84 longitude (-180 to 180)")] double longitude,
        [Description("Number of forecast days (1-16)")] int forecastDays = 7)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GetWeatherForecast called with lat={Latitude}, lon={Longitude}, days={ForecastDays}", 
                latitude, longitude, forecastDays);

            var weatherData = await _weatherClient.GetForecastAsync(latitude, longitude, forecastDays);

            _logger.LogInformation("MCP Tool: GetWeatherForecast returned data for lat={Latitude}, lon={Longitude}", 
                latitude, longitude);
            return weatherData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Tool: GetWeatherForecast failed for lat={Latitude}, lon={Longitude}", 
                latitude, longitude);
            throw;
        }
    }
}
