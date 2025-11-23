using System.Net.Http.Json;
using System.Text.Json;

namespace LocalAIAgent.OpenMeteo.Tools;

/// <summary>
/// Weather-related tools using Open-Meteo Weather Forecast API.
/// Free API, no key required: https://open-meteo.com/en/docs
///
/// ZERO-CODE PATTERN:
/// 1. Add [Tool] attribute with metadata
/// 2. ToolDiscoveryService automatically discovers this method at startup via reflection
/// 3. Creates ToolDescriptor with invoker delegate
/// 4. Registers in ToolRegistry
/// 5. No invoker code changes needed!
/// </summary>
internal static class WeatherTools
{
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Get current weather for a location using Open-Meteo API.
    /// API: GET https://api.open-meteo.com/v1/forecast
    /// </summary>
    [Tool(
        "GetWeather",
        Description = "Get current weather conditions for a location (temperature, humidity, wind, conditions). Requires latitude and longitude coordinates.",
        InputSchemaJson = """
        {
            "type": "object",
            "properties": {
                "latitude": {
                    "type": "number",
                    "description": "Latitude coordinate (-90 to 90)",
                    "minimum": -90,
                    "maximum": 90
                },
                "longitude": {
                    "type": "number",
                    "description": "Longitude coordinate (-180 to 180)",
                    "minimum": -180,
                    "maximum": 180
                }
            },
            "required": ["latitude", "longitude"]
        }
        """,
        SecurityClass = SecurityClass.Public,
        TimeoutSeconds = 10
    )]
    public static async Task<string> GetWeather(double latitude, double longitude)
    {
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?" +
                     $"latitude={latitude:F2}&longitude={longitude:F2}" +
                     $"&current=temperature_2m,relative_humidity_2m,apparent_temperature," +
                     $"precipitation,rain,showers,snowfall,weather_code,cloud_cover," +
                     $"pressure_msl,surface_pressure,wind_speed_10m,wind_direction_10m,wind_gusts_10m" +
                     $"&temperature_unit=celsius&wind_speed_unit=kmh";

            var response = await _httpClient.GetFromJsonAsync<JsonDocument>(url);
            if (response == null)
                return JsonSerializer.Serialize(new { error = "No response from weather API" });

            var current = response.RootElement.GetProperty("current");
            var weatherCode = current.GetProperty("weather_code").GetInt32();

            var result = new
            {
                location = new { latitude, longitude },
                time = current.GetProperty("time").GetString(),
                temperature = new
                {
                    current = current.GetProperty("temperature_2m").GetDouble(),
                    feels_like = current.GetProperty("apparent_temperature").GetDouble(),
                    unit = "°C"
                },
                humidity = current.GetProperty("relative_humidity_2m").GetInt32(),
                precipitation = new
                {
                    total = current.GetProperty("precipitation").GetDouble(),
                    rain = current.GetProperty("rain").GetDouble(),
                    showers = current.GetProperty("showers").GetDouble(),
                    snow = current.GetProperty("snowfall").GetDouble(),
                    unit = "mm"
                },
                wind = new
                {
                    speed = current.GetProperty("wind_speed_10m").GetDouble(),
                    direction = current.GetProperty("wind_direction_10m").GetInt32(),
                    gusts = current.GetProperty("wind_gusts_10m").GetDouble(),
                    unit = "km/h"
                },
                pressure = new
                {
                    msl = current.GetProperty("pressure_msl").GetDouble(),
                    surface = current.GetProperty("surface_pressure").GetDouble(),
                    unit = "hPa"
                },
                cloud_cover = current.GetProperty("cloud_cover").GetInt32(),
                conditions = GetWeatherDescription(weatherCode)
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get 7-day weather forecast using Open-Meteo API.
    /// API: GET https://api.open-meteo.com/v1/forecast
    /// </summary>
    [Tool(
        "GetForecast",
        Description = "Get 7-day weather forecast with daily temperature highs/lows, precipitation, and conditions. Requires latitude and longitude coordinates.",
        InputSchemaJson = """
        {
            "type": "object",
            "properties": {
                "latitude": {
                    "type": "number",
                    "description": "Latitude coordinate (-90 to 90)",
                    "minimum": -90,
                    "maximum": 90
                },
                "longitude": {
                    "type": "number",
                    "description": "Longitude coordinate (-180 to 180)",
                    "minimum": -180,
                    "maximum": 180
                },
                "days": {
                    "type": "integer",
                    "description": "Number of forecast days (1-16, default 7)",
                    "minimum": 1,
                    "maximum": 16,
                    "default": 7
                }
            },
            "required": ["latitude", "longitude"]
        }
        """,
        SecurityClass = SecurityClass.Public,
        TimeoutSeconds = 10
    )]
    public static async Task<string> GetForecast(double latitude, double longitude, int days = 7)
    {
        try
        {
            days = Math.Clamp(days, 1, 16);

            var url = $"https://api.open-meteo.com/v1/forecast?" +
                     $"latitude={latitude:F2}&longitude={longitude:F2}" +
                     $"&daily=temperature_2m_max,temperature_2m_min,temperature_2m_mean," +
                     $"apparent_temperature_max,apparent_temperature_min," +
                     $"precipitation_sum,rain_sum,showers_sum,snowfall_sum," +
                     $"precipitation_hours,precipitation_probability_max," +
                     $"weather_code,sunrise,sunset,daylight_duration,sunshine_duration," +
                     $"uv_index_max,wind_speed_10m_max,wind_gusts_10m_max,wind_direction_10m_dominant" +
                     $"&forecast_days={days}" +
                     $"&temperature_unit=celsius&wind_speed_unit=kmh&precipitation_unit=mm";

            var response = await _httpClient.GetFromJsonAsync<JsonDocument>(url);
            if (response == null)
                return JsonSerializer.Serialize(new { error = "No response from weather API" });

            var daily = response.RootElement.GetProperty("daily");
            var times = daily.GetProperty("time").EnumerateArray().Select(x => x.GetString()).ToArray();
            var tempMax = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var tempMin = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var tempMean = daily.GetProperty("temperature_2m_mean").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var precipSum = daily.GetProperty("precipitation_sum").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var precipProb = daily.GetProperty("precipitation_probability_max").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var weatherCodes = daily.GetProperty("weather_code").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var sunrise = daily.GetProperty("sunrise").EnumerateArray().Select(x => x.GetString()).ToArray();
            var sunset = daily.GetProperty("sunset").EnumerateArray().Select(x => x.GetString()).ToArray();
            var uvIndex = daily.GetProperty("uv_index_max").EnumerateArray().Select(x => x.GetDouble()).ToArray();

            var forecast = times.Select((date, i) => new
            {
                date,
                temperature = new
                {
                    max = tempMax[i],
                    min = tempMin[i],
                    mean = tempMean[i],
                    unit = "°C"
                },
                precipitation = new
                {
                    sum = precipSum[i],
                    probability = precipProb[i],
                    unit = "mm"
                },
                sun = new
                {
                    sunrise = sunrise[i],
                    sunset = sunset[i]
                },
                uv_index = uvIndex[i],
                conditions = GetWeatherDescription(weatherCodes[i])
            }).ToArray();

            return JsonSerializer.Serialize(new { location = new { latitude, longitude }, forecast },
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Converts WMO weather code to human-readable description.
    /// Based on WMO code standard: https://open-meteo.com/en/docs
    /// </summary>
    private static string GetWeatherDescription(int code)
    {
        return code switch
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
            _ => $"Unknown (code {code})"
        };
    }
}
