namespace LocalAIAgent.Agent.Models;

/// <summary>
/// Represents a geographic location returned from OpenMeteo Geocoding API.
/// </summary>
public record Location
{
    /// <summary>
    /// GeoNames location ID
    /// </summary>
    public required int Id { get; init; }
    
    /// <summary>
    /// Location name (city/place name)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// WGS84 latitude (-90 to 90)
    /// </summary>
    public required double Latitude { get; init; }
    
    /// <summary>
    /// WGS84 longitude (-180 to 180)
    /// </summary>
    public required double Longitude { get; init; }
    
    /// <summary>
    /// Country name (e.g., "United States")
    /// </summary>
    public required string Country { get; init; }
    
    /// <summary>
    /// ISO 3166-1 alpha-2 country code (e.g., "US")
    /// </summary>
    public required string CountryCode { get; init; }
    
    /// <summary>
    /// State/province (e.g., "Washington")
    /// </summary>
    public string? Admin1 { get; init; }
    
    /// <summary>
    /// IANA timezone (e.g., "America/Los_Angeles")
    /// </summary>
    public required string Timezone { get; init; }
    
    /// <summary>
    /// Population count (if available)
    /// </summary>
    public int? Population { get; init; }
}
