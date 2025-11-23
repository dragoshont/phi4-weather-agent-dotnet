using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalAIAgent.Agent.Models;

namespace LocalAIAgent.Agent.Services;

/// <summary>
/// HTTP client for OpenMeteo Geocoding API.
/// Converts location names to geographic coordinates with fuzzy matching.
/// </summary>
public class OpenMeteoGeocodeClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoGeocodeClient> _logger;

    public OpenMeteoGeocodeClient(HttpClient httpClient, ILogger<OpenMeteoGeocodeClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Search for locations by name or postal code.
    /// </summary>
    /// <param name="locationName">Location name or postal code (e.g., "Seattle", "Paris, France", "98101")</param>
    /// <param name="count">Maximum number of results (1-100)</param>
    /// <returns>Array of matching locations, empty if no results found</returns>
    public async Task<Location[]> SearchLocationAsync(string locationName, int count = 10)
    {
        if (string.IsNullOrWhiteSpace(locationName))
        {
            throw new ArgumentException("Location name cannot be empty", nameof(locationName));
        }

        if (count < 1 || count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 100");
        }

        try
        {
            _logger.LogInformation("Searching for location: {LocationName}, count: {Count}", locationName, count);

            var response = await _httpClient.GetAsync($"search?name={Uri.EscapeDataString(locationName)}&count={count}&language=en");
            response.EnsureSuccessStatusCode();

            var geocodeResponse = await response.Content.ReadFromJsonAsync<GeocodeApiResponse>();

            if (geocodeResponse?.Results == null || geocodeResponse.Results.Length == 0)
            {
                _logger.LogInformation("No locations found for: {LocationName}", locationName);
                return [];
            }

            var locations = geocodeResponse.Results
                .Select(r => new Location
                {
                    Id = r.Id,
                    Name = r.Name,
                    Latitude = r.Latitude,
                    Longitude = r.Longitude,
                    Country = r.Country,
                    CountryCode = r.CountryCode,
                    Admin1 = r.Admin1,
                    Timezone = r.Timezone,
                    Population = r.Population
                })
                .ToArray();

            _logger.LogInformation("Found {Count} locations for: {LocationName}", locations.Length, locationName);
            return locations;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error searching for location: {LocationName}", locationName);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for location: {LocationName}", locationName);
            throw;
        }
    }

    #region Response DTOs

    private record GeocodeApiResponse(
        [property: JsonPropertyName("results")] GeocodeResult[]? Results,
        [property: JsonPropertyName("generationtime_ms")] double? GenerationTimeMs
    );

    private record GeocodeResult(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude,
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("country_code")] string CountryCode,
        [property: JsonPropertyName("admin1")] string? Admin1,
        [property: JsonPropertyName("timezone")] string Timezone,
        [property: JsonPropertyName("population")] int? Population
    );

    #endregion
}
