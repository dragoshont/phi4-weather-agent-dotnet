using System.ComponentModel;
using Microsoft.Extensions.AI;
using LocalAIAgent.Agent.Models;
using LocalAIAgent.Agent.Services;

namespace LocalAIAgent.Agent.Tools;

/// <summary>
/// MCP tool for converting location names to geographic coordinates.
/// Uses OpenMeteo Geocoding API with fuzzy matching.
/// </summary>
public class GeocodeTool
{
    private readonly OpenMeteoGeocodeClient _geocodeClient;
    private readonly ILogger<GeocodeTool> _logger;

    public GeocodeTool(OpenMeteoGeocodeClient geocodeClient, ILogger<GeocodeTool> logger)
    {
        _geocodeClient = geocodeClient ?? throw new ArgumentNullException(nameof(geocodeClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Convert location name to geographic coordinates using geocoding API.
    /// </summary>
    /// <param name="locationName">Location name or postal code (e.g., "Seattle", "Paris, France", "98101")</param>
    /// <param name="count">Maximum number of results to return (1-100, default 10)</param>
    /// <returns>Array of matching locations with coordinates, empty if no matches found</returns>
    [Description("Convert location name to geographic coordinates")]
    public async Task<Location[]> GeocodeLocationAsync(
        [Description("Location name or postal code (e.g., 'Seattle', 'Paris, France', '98101')")] string locationName,
        [Description("Maximum number of results (1-100)")] int count = 10)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GeocodeLocation called with locationName={LocationName}, count={Count}", locationName, count);

            var locations = await _geocodeClient.SearchLocationAsync(locationName, count);

            _logger.LogInformation("MCP Tool: GeocodeLocation returned {Count} results", locations.Length);
            return locations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Tool: GeocodeLocation failed for locationName={LocationName}", locationName);
            throw;
        }
    }
}
