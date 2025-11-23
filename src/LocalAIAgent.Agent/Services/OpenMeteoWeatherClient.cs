using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalAIAgent.Agent.Models;

namespace LocalAIAgent.Agent.Services;

/// <summary>
/// HTTP client for OpenMeteo Weather API.
/// Retrieves weather forecasts with current conditions and daily forecasts.
/// </summary>
public class OpenMeteoWeatherClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoWeatherClient> _logger;

    public OpenMeteoWeatherClient(HttpClient httpClient, ILogger<OpenMeteoWeatherClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get weather forecast for coordinates.
    /// </summary>
    /// <param name="latitude">WGS84 latitude (-90 to 90)</param>
    /// <param name="longitude">WGS84 longitude (-180 to 180)</param>
    /// <param name="forecastDays">Number of forecast days (1-16)</param>
    /// <returns>Weather data with current conditions and daily forecasts</returns>
    public async Task<WeatherData> GetForecastAsync(double latitude, double longitude, int forecastDays = 7)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90");
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180");
        }

        if (forecastDays < 1 || forecastDays > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(forecastDays), "Forecast days must be between 1 and 16");
        }

        try
        {
            _logger.LogInformation("Fetching weather for lat={Latitude}, lon={Longitude}, days={ForecastDays}", latitude, longitude, forecastDays);

            var url = $"forecast?latitude={latitude:F4}&longitude={longitude:F4}" +
                      $"&hourly=temperature_2m,precipitation,weather_code,wind_speed_10m,cloud_cover" +
                      $"&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,weather_code,sunrise,sunset" +
                      $"&forecast_days={forecastDays}" +
                      $"&timezone=auto";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var weatherResponse = await response.Content.ReadFromJsonAsync<WeatherApiResponse>();

            if (weatherResponse == null)
            {
                throw new JsonException("Weather API returned null response");
            }

            var weatherData = MapToWeatherData(weatherResponse);
            _logger.LogInformation("Retrieved weather data for lat={Latitude}, lon={Longitude}", latitude, longitude);
            
            return weatherData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching weather for lat={Latitude}, lon={Longitude}", latitude, longitude);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for weather data");
            throw;
        }
    }

    private WeatherData MapToWeatherData(WeatherApiResponse response)
    {
        // Extract current conditions from first hourly entry
        var currentTime = DateTimeOffset.Parse(response.Hourly.Time[0]);
        var current = new CurrentConditions
        {
            Time = currentTime,
            Temperature = response.Hourly.Temperature2m[0],
            WeatherCode = response.Hourly.WeatherCode[0],
            Description = GetWeatherDescription(response.Hourly.WeatherCode[0]),
            Precipitation = response.Hourly.Precipitation[0],
            WindSpeed = response.Hourly.WindSpeed10m[0],
            CloudCover = response.Hourly.CloudCover[0]
        };

        // Map daily forecasts
        var daily = new List<DailyForecast>();
        for (int i = 0; i < response.Daily.Time.Length; i++)
        {
            daily.Add(new DailyForecast
            {
                Date = DateOnly.Parse(response.Daily.Time[i]),
                TemperatureMax = response.Daily.Temperature2mMax[i],
                TemperatureMin = response.Daily.Temperature2mMin[i],
                PrecipitationSum = response.Daily.PrecipitationSum[i],
                WeatherCode = response.Daily.WeatherCode[i],
                Description = GetWeatherDescription(response.Daily.WeatherCode[i]),
                Sunrise = DateTimeOffset.Parse(response.Daily.Sunrise[i]),
                Sunset = DateTimeOffset.Parse(response.Daily.Sunset[i])
            });
        }

        return new WeatherData
        {
            Latitude = response.Latitude,
            Longitude = response.Longitude,
            Current = current,
            Daily = daily.AsReadOnly(),
            Timezone = response.Timezone
        };
    }

    /// <summary>
    /// Map WMO weather codes to human-readable descriptions.
    /// Based on https://open-meteo.com/en/docs
    /// </summary>
    private static string GetWeatherDescription(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Foggy",
        48 => "Depositing rime fog",
        51 => "Light drizzle",
        53 => "Moderate drizzle",
        55 => "Dense drizzle",
        56 => "Light freezing drizzle",
        57 => "Dense freezing drizzle",
        61 => "Slight rain",
        63 => "Moderate rain",
        65 => "Heavy rain",
        66 => "Light freezing rain",
        67 => "Heavy freezing rain",
        71 => "Slight snow fall",
        73 => "Moderate snow fall",
        75 => "Heavy snow fall",
        77 => "Snow grains",
        80 => "Slight rain showers",
        81 => "Moderate rain showers",
        82 => "Violent rain showers",
        85 => "Slight snow showers",
        86 => "Heavy snow showers",
        95 => "Thunderstorm",
        96 => "Thunderstorm with slight hail",
        99 => "Thunderstorm with heavy hail",
        _ => "Unknown"
    };

    #region Response DTOs

    private record WeatherApiResponse(
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude,
        [property: JsonPropertyName("timezone")] string Timezone,
        [property: JsonPropertyName("hourly")] HourlyData Hourly,
        [property: JsonPropertyName("daily")] DailyData Daily
    );

    private record HourlyData(
        [property: JsonPropertyName("time")] string[] Time,
        [property: JsonPropertyName("temperature_2m")] double[] Temperature2m,
        [property: JsonPropertyName("precipitation")] double[] Precipitation,
        [property: JsonPropertyName("weather_code")] int[] WeatherCode,
        [property: JsonPropertyName("wind_speed_10m")] double[] WindSpeed10m,
        [property: JsonPropertyName("cloud_cover")] int[] CloudCover
    );

    private record DailyData(
        [property: JsonPropertyName("time")] string[] Time,
        [property: JsonPropertyName("temperature_2m_max")] double[] Temperature2mMax,
        [property: JsonPropertyName("temperature_2m_min")] double[] Temperature2mMin,
        [property: JsonPropertyName("precipitation_sum")] double[] PrecipitationSum,
        [property: JsonPropertyName("weather_code")] int[] WeatherCode,
        [property: JsonPropertyName("sunrise")] string[] Sunrise,
        [property: JsonPropertyName("sunset")] string[] Sunset
    );

    #endregion
}
