namespace LocalAIAgent.Agent.Models;

/// <summary>
/// Represents pollen/allergen forecast data from Air Quality API.
/// </summary>
public record AllergenData
{
    /// <summary>
    /// Location coordinates
    /// </summary>
    public required double Latitude { get; init; }
    
    /// <summary>
    /// Location coordinates
    /// </summary>
    public required double Longitude { get; init; }
    
    /// <summary>
    /// Pollen levels by type
    /// </summary>
    public required PollenLevels Pollen { get; init; }
    
    /// <summary>
    /// Overall severity rating
    /// </summary>
    public required AllergySeverity Severity { get; init; }
    
    /// <summary>
    /// IANA timezone for timestamps
    /// </summary>
    public required string Timezone { get; init; }
    
    /// <summary>
    /// Geographic constraint flag (Europe only)
    /// </summary>
    public required bool IsEuropeRegion { get; init; }
}

/// <summary>
/// Pollen levels by allergen type (grains/m³).
/// </summary>
public record PollenLevels
{
    /// <summary>
    /// Alder pollen (grains/m³)
    /// </summary>
    public double? Alder { get; init; }
    
    /// <summary>
    /// Birch pollen (grains/m³)
    /// </summary>
    public double? Birch { get; init; }
    
    /// <summary>
    /// Grass pollen (grains/m³)
    /// </summary>
    public double? Grass { get; init; }
    
    /// <summary>
    /// Mugwort pollen (grains/m³)
    /// </summary>
    public double? Mugwort { get; init; }
    
    /// <summary>
    /// Olive pollen (grains/m³)
    /// </summary>
    public double? Olive { get; init; }
    
    /// <summary>
    /// Ragweed pollen (grains/m³)
    /// </summary>
    public double? Ragweed { get; init; }
}

/// <summary>
/// Allergy severity rating based on pollen concentration.
/// </summary>
public enum AllergySeverity
{
    /// <summary>
    /// 0-20 grains/m³ (any pollen type)
    /// </summary>
    Low,
    
    /// <summary>
    /// 21-50 grains/m³
    /// </summary>
    Moderate,
    
    /// <summary>
    /// 51-100 grains/m³
    /// </summary>
    High,
    
    /// <summary>
    /// >100 grains/m³
    /// </summary>
    VeryHigh,
    
    /// <summary>
    /// No data available (outside Europe or pollen season)
    /// </summary>
    Unknown
}
