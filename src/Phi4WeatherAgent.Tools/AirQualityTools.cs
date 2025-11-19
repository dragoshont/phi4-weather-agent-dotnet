using System.Net.Http.Json;
using System.Text.Json;

namespace Phi4WeatherAgent.Tools;

/// <summary>
/// Air quality and pollen tools using Open-Meteo Air Quality API.
/// Free API, no key required: https://open-meteo.com/en/docs/air-quality-api
/// Demonstrates zero-code tool registration pattern.
/// </summary>
public static class AirQualityTools
{
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Get current air quality data including pollutants and air quality indices.
    /// API: GET https://air-quality-api.open-meteo.com/v1/air-quality
    /// </summary>
    [Tool(
        "GetAirQuality",
        Description = "Get current air quality data including PM2.5, PM10, CO, NO2, SO2, O3, and air quality indices (US AQI, European AQI). Available worldwide for any location. Use this for US/non-European locations when user asks about air quality or pollutants. Requires latitude and longitude coordinates.",
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
    public static async Task<string> GetAirQuality(double latitude, double longitude)
    {
        try
        {
            var url = $"https://air-quality-api.open-meteo.com/v1/air-quality?" +
                     $"latitude={latitude:F2}&longitude={longitude:F2}" +
                     $"&current=pm10,pm2_5,carbon_monoxide,nitrogen_dioxide,sulphur_dioxide,ozone," +
                     $"aerosol_optical_depth,dust,uv_index,uv_index_clear_sky,ammonia," +
                     $"european_aqi,european_aqi_pm2_5,european_aqi_pm10,european_aqi_nitrogen_dioxide,european_aqi_ozone," +
                     $"us_aqi,us_aqi_pm2_5,us_aqi_pm10,us_aqi_carbon_monoxide,us_aqi_nitrogen_dioxide,us_aqi_ozone";

            var response = await _httpClient.GetFromJsonAsync<JsonDocument>(url);
            if (response == null)
                return JsonSerializer.Serialize(new { error = "No response from air quality API" });

            var current = response.RootElement.GetProperty("current");

            var result = new
            {
                location = new { latitude, longitude },
                time = current.GetProperty("time").GetString(),
                pollutants = new
                {
                    pm10 = new { value = current.GetProperty("pm10").GetDouble(), unit = "μg/m³", description = "Particulate matter < 10μm" },
                    pm2_5 = new { value = current.GetProperty("pm2_5").GetDouble(), unit = "μg/m³", description = "Particulate matter < 2.5μm" },
                    carbon_monoxide = new { value = current.GetProperty("carbon_monoxide").GetDouble(), unit = "μg/m³" },
                    nitrogen_dioxide = new { value = current.GetProperty("nitrogen_dioxide").GetDouble(), unit = "μg/m³" },
                    sulphur_dioxide = new { value = current.GetProperty("sulphur_dioxide").GetDouble(), unit = "μg/m³" },
                    ozone = new { value = current.GetProperty("ozone").GetDouble(), unit = "μg/m³" },
                    ammonia = current.TryGetProperty("ammonia", out var nh3) ? nh3.GetDouble() : (double?)null,
                    dust = new { value = current.GetProperty("dust").GetDouble(), unit = "μg/m³", description = "Saharan dust" }
                },
                indices = new
                {
                    us_aqi = new
                    {
                        overall = current.GetProperty("us_aqi").GetInt32(),
                        pm2_5 = current.GetProperty("us_aqi_pm2_5").GetInt32(),
                        pm10 = current.GetProperty("us_aqi_pm10").GetInt32(),
                        co = current.GetProperty("us_aqi_carbon_monoxide").GetInt32(),
                        no2 = current.GetProperty("us_aqi_nitrogen_dioxide").GetInt32(),
                        o3 = current.GetProperty("us_aqi_ozone").GetInt32(),
                        category = GetUSAQICategory(current.GetProperty("us_aqi").GetInt32())
                    },
                    european_aqi = new
                    {
                        overall = current.GetProperty("european_aqi").GetInt32(),
                        pm2_5 = current.GetProperty("european_aqi_pm2_5").GetInt32(),
                        pm10 = current.GetProperty("european_aqi_pm10").GetInt32(),
                        no2 = current.GetProperty("european_aqi_nitrogen_dioxide").GetInt32(),
                        o3 = current.GetProperty("european_aqi_ozone").GetInt32(),
                        category = GetEuropeanAQICategory(current.GetProperty("european_aqi").GetInt32())
                    }
                },
                uv = new
                {
                    index = current.GetProperty("uv_index").GetDouble(),
                    clear_sky_index = current.GetProperty("uv_index_clear_sky").GetDouble(),
                    risk_level = GetUVRiskLevel(current.GetProperty("uv_index").GetDouble())
                },
                aerosol_optical_depth = current.GetProperty("aerosol_optical_depth").GetDouble()
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get pollen forecast (European locations only).
    /// API: GET https://air-quality-api.open-meteo.com/v1/air-quality
    /// </summary>
    [Tool(
        "GetPollenForecast",
        Description = "Get detailed pollen forecast for European locations ONLY (alder, birch, grass, mugwort, olive, ragweed). NOT available for US, Asia, or other non-European regions. Only use for coordinates in Europe. For non-European locations, use GetAirQuality instead which provides air quality data. Requires latitude and longitude coordinates.",
        InputSchemaJson = """
        {
            "type": "object",
            "properties": {
                "latitude": { 
                    "type": "number", 
                    "description": "Latitude coordinate in Europe (-90 to 90)",
                    "minimum": -90,
                    "maximum": 90
                },
                "longitude": { 
                    "type": "number", 
                    "description": "Longitude coordinate in Europe (-180 to 180)",
                    "minimum": -180,
                    "maximum": 180
                },
                "days": {
                    "type": "integer",
                    "description": "Number of forecast days (1-4, default 4)",
                    "minimum": 1,
                    "maximum": 4,
                    "default": 4
                }
            },
            "required": ["latitude", "longitude"]
        }
        """,
        SecurityClass = SecurityClass.Public,
        TimeoutSeconds = 10
    )]
    public static async Task<string> GetPollenForecast(double latitude, double longitude, int days = 4)
    {
        try
        {
            days = Math.Clamp(days, 1, 4);

            var url = $"https://air-quality-api.open-meteo.com/v1/air-quality?" +
                     $"latitude={latitude:F2}&longitude={longitude:F2}" +
                     $"&hourly=alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,olive_pollen,ragweed_pollen" +
                     $"&forecast_days={days}" +
                     $"&domains=cams_europe";

            var response = await _httpClient.GetFromJsonAsync<JsonDocument>(url);
            if (response == null)
                return JsonSerializer.Serialize(new { error = "No response from air quality API" });

            // Check if pollen data is available (Europe only)
            if (!response.RootElement.TryGetProperty("hourly", out var hourly))
                return JsonSerializer.Serialize(new { error = "Pollen data not available for this location (Europe only)" });

            var times = hourly.GetProperty("time").EnumerateArray().Select(x => x.GetString()).ToArray();
            var alder = hourly.TryGetProperty("alder_pollen", out var a) ? a.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null;
            var birch = hourly.TryGetProperty("birch_pollen", out var b) ? b.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null;
            var grass = hourly.TryGetProperty("grass_pollen", out var g) ? g.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null;
            var mugwort = hourly.TryGetProperty("mugwort_pollen", out var m) ? m.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null;
            var olive = hourly.TryGetProperty("olive_pollen", out var o) ? o.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null;
            var ragweed = hourly.TryGetProperty("ragweed_pollen", out var r) ? r.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null;

            // Group by day and calculate daily max
            var dailyData = times
                .Select((time, i) => new
                {
                    DateTime = DateTime.Parse(time!),
                    Index = i
                })
                .GroupBy(x => x.DateTime.Date)
                .Select(day => new
                {
                    date = day.Key.ToString("yyyy-MM-dd"),
                    pollen = new
                    {
                        alder = alder != null ? new { max = day.Max(x => alder[x.Index]), unit = "grains/m³", level = GetPollenLevel(day.Max(x => alder[x.Index])) } : null,
                        birch = birch != null ? new { max = day.Max(x => birch[x.Index]), unit = "grains/m³", level = GetPollenLevel(day.Max(x => birch[x.Index])) } : null,
                        grass = grass != null ? new { max = day.Max(x => grass[x.Index]), unit = "grains/m³", level = GetPollenLevel(day.Max(x => grass[x.Index])) } : null,
                        mugwort = mugwort != null ? new { max = day.Max(x => mugwort[x.Index]), unit = "grains/m³", level = GetPollenLevel(day.Max(x => mugwort[x.Index])) } : null,
                        olive = olive != null ? new { max = day.Max(x => olive[x.Index]), unit = "grains/m³", level = GetPollenLevel(day.Max(x => olive[x.Index])) } : null,
                        ragweed = ragweed != null ? new { max = day.Max(x => ragweed[x.Index]), unit = "grains/m³", level = GetPollenLevel(day.Max(x => ragweed[x.Index])) } : null
                    }
                })
                .ToArray();

            return JsonSerializer.Serialize(new
            {
                location = new { latitude, longitude },
                note = "Pollen data only available for European locations during pollen season",
                forecast = dailyData
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static string GetUSAQICategory(int aqi)
    {
        return aqi switch
        {
            <= 50 => "Good",
            <= 100 => "Moderate",
            <= 150 => "Unhealthy for Sensitive Groups",
            <= 200 => "Unhealthy",
            <= 300 => "Very Unhealthy",
            _ => "Hazardous"
        };
    }

    private static string GetEuropeanAQICategory(int aqi)
    {
        return aqi switch
        {
            <= 20 => "Good",
            <= 40 => "Fair",
            <= 60 => "Moderate",
            <= 80 => "Poor",
            <= 100 => "Very Poor",
            _ => "Extremely Poor"
        };
    }

    private static string GetUVRiskLevel(double uvIndex)
    {
        return uvIndex switch
        {
            < 3 => "Low",
            < 6 => "Moderate",
            < 8 => "High",
            < 11 => "Very High",
            _ => "Extreme"
        };
    }

    private static string GetPollenLevel(double count)
    {
        // Approximate thresholds (vary by pollen type)
        return count switch
        {
            < 10 => "Low",
            < 50 => "Moderate",
            < 100 => "High",
            _ => "Very High"
        };
    }
}
