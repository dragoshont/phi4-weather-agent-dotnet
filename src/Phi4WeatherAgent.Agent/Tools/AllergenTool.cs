using System.ComponentModel;
using Microsoft.Extensions.AI;
using Phi4WeatherAgent.Agent.Models;
using Phi4WeatherAgent.Agent.Services;

namespace Phi4WeatherAgent.Agent.Tools;

/// <summary>
/// MCP tool for retrieving pollen/allergen levels.
/// Uses OpenMeteo Air Quality API (Europe only).
/// </summary>
public class AllergenTool
{
    private readonly OpenMeteoAllergenClient _allergenClient;
    private readonly ILogger<AllergenTool> _logger;

    public AllergenTool(OpenMeteoAllergenClient allergenClient, ILogger<AllergenTool> logger)
    {
        _allergenClient = allergenClient ?? throw new ArgumentNullException(nameof(allergenClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get pollen and allergen levels for geographic coordinates.
    /// </summary>
    /// <param name="latitude">WGS84 latitude (-90 to 90)</param>
    /// <param name="longitude">WGS84 longitude (-180 to 180)</param>
    /// <returns>Allergen data with pollen levels and severity (Europe only, returns Unknown for other regions)</returns>
    [Description("Get pollen and allergen levels for coordinates")]
    public async Task<AllergenData> GetAllergenLevelsAsync(
        [Description("WGS84 latitude (-90 to 90)")] double latitude,
        [Description("WGS84 longitude (-180 to 180)")] double longitude)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GetAllergenLevels called with lat={Latitude}, lon={Longitude}", 
                latitude, longitude);

            var allergenData = await _allergenClient.GetAllergenLevelsAsync(latitude, longitude);

            _logger.LogInformation("MCP Tool: GetAllergenLevels returned severity={Severity}, isEurope={IsEurope}", 
                allergenData.Severity, allergenData.IsEuropeRegion);
            return allergenData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Tool: GetAllergenLevels failed for lat={Latitude}, lon={Longitude}", 
                latitude, longitude);
            throw;
        }
    }
}
