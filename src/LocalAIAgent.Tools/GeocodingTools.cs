using System.Net.Http.Json;
using System.Text.Json;

namespace LocalAIAgent.Tools;

/// <summary>
/// Geocoding tools using Open-Meteo Geocoding API.
/// Free API, no key required: https://open-meteo.com/en/docs/geocoding-api
/// Automatically discovered by ToolDiscoveryService via [Tool] attribute.
/// </summary>
public static class GeocodingTools
{
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Convert location name to coordinates using Open-Meteo Geocoding API.
    /// API: GET https://geocoding-api.open-meteo.com/v1/search
    /// </summary>
    [Tool(
        "GeocodeLocation",
        Description = "Convert a location name (city, postal code, address) to latitude/longitude coordinates. Returns multiple results if available.",
        InputSchemaJson = """
        {
            "type": "object",
            "properties": {
                "location": {
                    "type": "string",
                    "description": "City name, postal code, or address to geocode (minimum 2 characters)",
                    "minLength": 2
                },
                "count": {
                    "type": "integer",
                    "description": "Number of results to return (1-10, default 5)",
                    "minimum": 1,
                    "maximum": 10,
                    "default": 5
                },
                "language": {
                    "type": "string",
                    "description": "Language for results (en, es, fr, de, etc.)",
                    "default": "en"
                }
            },
            "required": ["location"]
        }
        """,
        SecurityClass = SecurityClass.Public,
        TimeoutSeconds = 10
    )]
    public static async Task<string> GeocodeLocation(string location, int count = 5, string language = "en")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(location) || location.Length < 2)
                return JsonSerializer.Serialize(new { error = "Location must be at least 2 characters" });

            count = Math.Clamp(count, 1, 10);
            var encodedLocation = Uri.EscapeDataString(location);
            var url = $"https://geocoding-api.open-meteo.com/v1/search?" +
                     $"name={encodedLocation}" +
                     $"&count={count}" +
                     $"&language={language}" +
                     $"&format=json";

            var response = await _httpClient.GetFromJsonAsync<JsonDocument>(url);
            if (response == null)
                return JsonSerializer.Serialize(new { error = "No response from geocoding API" });

            // Check if results exist
            if (!response.RootElement.TryGetProperty("results", out var resultsElement))
                return JsonSerializer.Serialize(new { error = $"No results found for '{location}'" });

            var results = resultsElement.EnumerateArray().Select(r => new
            {
                id = r.GetProperty("id").GetInt64(),
                name = r.GetProperty("name").GetString(),
                latitude = r.GetProperty("latitude").GetDouble(),
                longitude = r.GetProperty("longitude").GetDouble(),
                elevation = r.TryGetProperty("elevation", out var elev) ? elev.GetDouble() : (double?)null,
                timezone = r.TryGetProperty("timezone", out var tz) ? tz.GetString() : null,
                population = r.TryGetProperty("population", out var pop) ? pop.GetInt32() : (int?)null,
                country = r.TryGetProperty("country", out var country) ? country.GetString() : null,
                country_code = r.TryGetProperty("country_code", out var cc) ? cc.GetString() : null,
                admin1 = r.TryGetProperty("admin1", out var a1) ? a1.GetString() : null,
                admin2 = r.TryGetProperty("admin2", out var a2) ? a2.GetString() : null,
                admin3 = r.TryGetProperty("admin3", out var a3) ? a3.GetString() : null,
                admin4 = r.TryGetProperty("admin4", out var a4) ? a4.GetString() : null,
                postcodes = r.TryGetProperty("postcodes", out var pc) 
                    ? pc.EnumerateArray().Select(x => x.GetString()).ToArray() 
                    : null
            }).ToArray();

            return JsonSerializer.Serialize(new
            {
                query = location,
                count = results.Length,
                results
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
