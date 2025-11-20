using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Phi4WeatherAgent.Agent.Models;

namespace Phi4WeatherAgent.Agent.Services;

/// <summary>
/// HTTP client for OpenMeteo Air Quality API.
/// Retrieves pollen/allergen levels (Europe only).
/// </summary>
public class OpenMeteoAllergenClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoAllergenClient> _logger;

    public OpenMeteoAllergenClient(HttpClient httpClient, ILogger<OpenMeteoAllergenClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get pollen/allergen levels for coordinates.
    /// </summary>
    /// <param name="latitude">WGS84 latitude (-90 to 90)</param>
    /// <param name="longitude">WGS84 longitude (-180 to 180)</param>
    /// <returns>Allergen data with pollen levels and severity</returns>
    public async Task<AllergenData> GetAllergenLevelsAsync(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90");
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180");
        }

        try
        {
            _logger.LogInformation("Fetching allergen data for lat={Latitude}, lon={Longitude}", latitude, longitude);

            var url = $"air-quality?latitude={latitude:F4}&longitude={longitude:F4}" +
                      $"&hourly=alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,olive_pollen,ragweed_pollen" +
                      $"&forecast_days=4" +
                      $"&timezone=auto";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var allergenResponse = await response.Content.ReadFromJsonAsync<AllergenApiResponse>();

            if (allergenResponse == null)
            {
                throw new JsonException("Allergen API returned null response");
            }

            var allergenData = MapToAllergenData(allergenResponse, latitude, longitude);
            _logger.LogInformation("Retrieved allergen data for lat={Latitude}, lon={Longitude}, severity={Severity}", 
                latitude, longitude, allergenData.Severity);
            
            return allergenData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching allergen data for lat={Latitude}, lon={Longitude}", latitude, longitude);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for allergen data");
            throw;
        }
    }

    private AllergenData MapToAllergenData(AllergenApiResponse response, double latitude, double longitude)
    {
        // Check if location is in Europe (pollen data availability)
        var isEurope = IsEuropeRegion(latitude, longitude);

        // Extract current pollen levels (first hourly entry)
        var pollen = new PollenLevels
        {
            Alder = GetCurrentPollenLevel(response.Hourly.AlderPollen),
            Birch = GetCurrentPollenLevel(response.Hourly.BirchPollen),
            Grass = GetCurrentPollenLevel(response.Hourly.GrassPollen),
            Mugwort = GetCurrentPollenLevel(response.Hourly.MugwortPollen),
            Olive = GetCurrentPollenLevel(response.Hourly.OlivePollen),
            Ragweed = GetCurrentPollenLevel(response.Hourly.RagweedPollen)
        };

        // Calculate overall severity from all pollen types
        var severity = CalculateSeverity(pollen, isEurope);

        return new AllergenData
        {
            Latitude = latitude,
            Longitude = longitude,
            Pollen = pollen,
            Severity = severity,
            Timezone = response.Timezone,
            IsEuropeRegion = isEurope
        };
    }

    private double? GetCurrentPollenLevel(double?[] pollenArray)
    {
        if (pollenArray == null || pollenArray.Length == 0)
        {
            return null;
        }

        // Return first non-null value (current reading)
        return pollenArray.FirstOrDefault(p => p.HasValue);
    }

    private AllergySeverity CalculateSeverity(PollenLevels pollen, bool isEurope)
    {
        if (!isEurope)
        {
            return AllergySeverity.Unknown;
        }

        // Get all non-null pollen values
        var pollenValues = new[] 
        { 
            pollen.Alder, 
            pollen.Birch, 
            pollen.Grass, 
            pollen.Mugwort, 
            pollen.Olive, 
            pollen.Ragweed 
        }
        .Where(p => p.HasValue)
        .Select(p => p!.Value)
        .ToList();

        if (pollenValues.Count == 0)
        {
            return AllergySeverity.Unknown;
        }

        // Use max pollen value for severity rating
        var maxPollen = pollenValues.Max();

        return maxPollen switch
        {
            <= 20 => AllergySeverity.Low,
            <= 50 => AllergySeverity.Moderate,
            <= 100 => AllergySeverity.High,
            _ => AllergySeverity.VeryHigh
        };
    }

    /// <summary>
    /// Check if coordinates are in Europe (approximate bounds).
    /// Europe: 35-71°N latitude, -10-40°E longitude
    /// </summary>
    private bool IsEuropeRegion(double latitude, double longitude)
    {
        return latitude >= 35 && latitude <= 71 && longitude >= -10 && longitude <= 40;
    }

    #region Response DTOs

    private record AllergenApiResponse(
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude,
        [property: JsonPropertyName("timezone")] string Timezone,
        [property: JsonPropertyName("hourly")] HourlyPollenData Hourly
    );

    private record HourlyPollenData(
        [property: JsonPropertyName("time")] string[] Time,
        [property: JsonPropertyName("alder_pollen")] double?[] AlderPollen,
        [property: JsonPropertyName("birch_pollen")] double?[] BirchPollen,
        [property: JsonPropertyName("grass_pollen")] double?[] GrassPollen,
        [property: JsonPropertyName("mugwort_pollen")] double?[] MugwortPollen,
        [property: JsonPropertyName("olive_pollen")] double?[] OlivePollen,
        [property: JsonPropertyName("ragweed_pollen")] double?[] RagweedPollen
    );

    #endregion
}
