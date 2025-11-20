using Microsoft.Extensions.AI;
using Phi4WeatherAgent.Agent.Models;
using Phi4WeatherAgent.Agent.Tools;

namespace Phi4WeatherAgent.Agent.Services;

/// <summary>
/// Service for managing agent conversation context and orchestrating weather queries.
/// </summary>
public class AgentService
{
    private readonly ILogger<AgentService> _logger;
    private readonly GeocodeTool _geocodeTool;
    private readonly WeatherTool _weatherTool;
    private readonly AllergenTool _allergenTool;
    
    // T057: Conversation context persistence
    private string? _lastLocationName;
    private Location? _lastLocation;

    public AgentService(
        ILogger<AgentService> logger,
        GeocodeTool geocodeTool,
        WeatherTool weatherTool,
        AllergenTool allergenTool)
    {
        _logger = logger;
        _geocodeTool = geocodeTool;
        _weatherTool = weatherTool;
        _allergenTool = allergenTool;
    }

    /// <summary>
    /// Creates a new in-memory conversation context.
    /// Per zero-cost principle (Constitution VI), no persistence layer.
    /// Context clears on SignalR disconnect per quickstart.md guidance.
    /// </summary>
    public List<ChatMessage> CreateConversationContext()
    {
        _logger.LogInformation("Creating new conversation context");
        return new List<ChatMessage>();
    }

    /// <summary>
    /// Adds a system prompt to the conversation context.
    /// </summary>
    public void AddSystemPrompt(List<ChatMessage> context, string prompt)
    {
        context.Add(new ChatMessage(ChatRole.System, prompt));
        _logger.LogDebug("Added system prompt to conversation context");
    }

    /// <summary>
    /// Adds a user message to the conversation context.
    /// </summary>
    public void AddUserMessage(List<ChatMessage> context, string message)
    {
        context.Add(new ChatMessage(ChatRole.User, message));
        _logger.LogDebug("Added user message to conversation context: {Message}", message);
    }

    /// <summary>
    /// Adds an assistant response to the conversation context.
    /// </summary>
    public void AddAssistantResponse(List<ChatMessage> context, string response)
    {
        context.Add(new ChatMessage(ChatRole.Assistant, response));
        _logger.LogDebug("Added assistant response to conversation context");
    }

    /// <summary>
    /// Gets the current conversation message count.
    /// </summary>
    public int GetMessageCount(List<ChatMessage> context)
    {
        return context.Count;
    }

    /// <summary>
    /// Clears the conversation context (except system prompt).
    /// </summary>
    public void ClearContext(List<ChatMessage> context, bool keepSystemPrompt = true)
    {
        if (keepSystemPrompt && context.Count > 0 && context[0].Role == ChatRole.System)
        {
            var systemPrompt = context[0];
            context.Clear();
            context.Add(systemPrompt);
            _logger.LogInformation("Cleared conversation context (kept system prompt)");
        }
        else
        {
            context.Clear();
            _logger.LogInformation("Cleared conversation context completely");
        }
    }

    // T041: Weather query orchestration flow
    /// <summary>
    /// Orchestrates complete weather query: geocode location → fetch forecast.
    /// </summary>
    /// <param name="locationName">Location name or postal code</param>
    /// <param name="forecastDays">Number of forecast days (1-16, default 7)</param>
    /// <returns>Weather data with current conditions and daily forecasts, or null if location not found</returns>
    public async Task<(WeatherData? WeatherData, Location? Location)> GetWeatherAsync(string locationName, int forecastDays = 7)
    {
        try
        {
            _logger.LogInformation("Orchestrating weather query for location: {LocationName}", locationName);

            // Step 1: Geocode location name to coordinates
            var locations = await _geocodeTool.GeocodeLocationAsync(locationName, count: 5);
            
            if (locations.Length == 0)
            {
                _logger.LogWarning("No locations found for: {LocationName}", locationName);
                return (null, null);
            }

            var location = locations[0]; // Use first match (best match from API)
            _logger.LogInformation("Selected location: {Name}, {Country} (lat={Lat}, lon={Lon})", 
                location.Name, location.Country, location.Latitude, location.Longitude);

            // Step 2: Fetch weather forecast for coordinates
            var weatherData = await _weatherTool.GetWeatherForecastAsync(
                location.Latitude, 
                location.Longitude, 
                forecastDays);

            _logger.LogInformation("Successfully retrieved weather for {Name}", location.Name);
            return (weatherData, location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weather query orchestration failed for location: {LocationName}", locationName);
            throw;
        }
    }

    // T050: Allergen query orchestration flow
    /// <summary>
    /// Orchestrates allergen query: geocode location → validate Europe region → fetch pollen levels.
    /// </summary>
    /// <param name="locationName">Location name or postal code</param>
    /// <returns>Allergen data with pollen levels and severity, or null if location not found</returns>
    public async Task<(AllergenData? AllergenData, Location? Location)> GetAllergenLevelsAsync(string locationName)
    {
        try
        {
            _logger.LogInformation("Orchestrating allergen query for location: {LocationName}", locationName);

            // Step 1: Geocode location name to coordinates
            var locations = await _geocodeTool.GeocodeLocationAsync(locationName, count: 5);
            
            if (locations.Length == 0)
            {
                _logger.LogWarning("No locations found for: {LocationName}", locationName);
                return (null, null);
            }

            var location = locations[0]; // Use first match (best match from API)
            _logger.LogInformation("Selected location: {Name}, {Country} (lat={Lat}, lon={Lon})", 
                location.Name, location.Country, location.Latitude, location.Longitude);

            // Step 2: Fetch allergen data for coordinates
            var allergenData = await _allergenTool.GetAllergenLevelsAsync(
                location.Latitude, 
                location.Longitude);

            // Step 3: Log Europe region status
            if (!allergenData.IsEuropeRegion)
            {
                _logger.LogWarning("Location {Name} is outside Europe - pollen data not available", location.Name);
            }

            _logger.LogInformation("Successfully retrieved allergen data for {Name}, severity={Severity}", 
                location.Name, allergenData.Severity);
            return (allergenData, location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Allergen query orchestration failed for location: {LocationName}", locationName);
            throw;
        }
    }

    // T055: Multi-day planning methods
    /// <summary>
    /// Gets weather for weekend (Saturday and Sunday) for planning purposes.
    /// </summary>
    /// <param name="locationName">Location name or postal code</param>
    /// <returns>Weather data for Saturday and Sunday, or null if location not found</returns>
    public async Task<(WeatherData? WeatherData, Location? Location)> GetWeekendWeatherAsync(string locationName)
    {
        try
        {
            _logger.LogInformation("Orchestrating weekend weather query for location: {LocationName}", locationName);

            // Step 1: Geocode location (or use cached location if same as last query)
            Location? location;
            if (_lastLocationName == locationName && _lastLocation != null)
            {
                location = _lastLocation;
                _logger.LogInformation("Using cached location: {Name}", location.Name);
            }
            else
            {
                var locations = await _geocodeTool.GeocodeLocationAsync(locationName, count: 5);
                
                if (locations.Length == 0)
                {
                    _logger.LogWarning("No locations found for: {LocationName}", locationName);
                    return (null, null);
                }

                location = locations[0];
                _lastLocationName = locationName;
                _lastLocation = location;
                _logger.LogInformation("Selected location: {Name}, {Country}", location.Name, location.Country);
            }

            // Step 2: Fetch 7-day forecast (includes upcoming weekend)
            var weatherData = await _weatherTool.GetWeatherForecastAsync(
                location.Latitude, 
                location.Longitude, 
                forecastDays: 7);

            _logger.LogInformation("Successfully retrieved weekend weather for {Name}", location.Name);
            return (weatherData, location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weekend weather query orchestration failed for location: {LocationName}", locationName);
            throw;
        }
    }

    /// <summary>
    /// Gets weather for a specific date range for planning purposes.
    /// </summary>
    /// <param name="locationName">Location name or postal code</param>
    /// <param name="startDate">Start date for forecast range</param>
    /// <param name="endDate">End date for forecast range</param>
    /// <returns>Weather data for date range, or null if location not found</returns>
    public async Task<(WeatherData? WeatherData, Location? Location)> GetDateRangeWeatherAsync(
        string locationName, 
        DateOnly startDate, 
        DateOnly endDate)
    {
        try
        {
            var dayCount = endDate.DayNumber - startDate.DayNumber + 1;
            if (dayCount < 1 || dayCount > 16)
            {
                _logger.LogWarning("Invalid date range: {StartDate} to {EndDate} ({DayCount} days)", 
                    startDate, endDate, dayCount);
                throw new ArgumentException("Date range must be between 1 and 16 days");
            }

            _logger.LogInformation("Orchestrating date range weather query for {LocationName}: {StartDate} to {EndDate}", 
                locationName, startDate, endDate);

            // Step 1: Geocode location (or use cached location)
            Location? location;
            if (_lastLocationName == locationName && _lastLocation != null)
            {
                location = _lastLocation;
                _logger.LogInformation("Using cached location: {Name}", location.Name);
            }
            else
            {
                var locations = await _geocodeTool.GeocodeLocationAsync(locationName, count: 5);
                
                if (locations.Length == 0)
                {
                    _logger.LogWarning("No locations found for: {LocationName}", locationName);
                    return (null, null);
                }

                location = locations[0];
                _lastLocationName = locationName;
                _lastLocation = location;
                _logger.LogInformation("Selected location: {Name}, {Country}", location.Name, location.Country);
            }

            // Step 2: Fetch forecast for date range
            var weatherData = await _weatherTool.GetWeatherForecastAsync(
                location.Latitude, 
                location.Longitude, 
                forecastDays: Math.Min(dayCount, 16));

            _logger.LogInformation("Successfully retrieved date range weather for {Name}", location.Name);
            return (weatherData, location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Date range weather query orchestration failed for location: {LocationName}", locationName);
            throw;
        }
    }

    /// <summary>
    /// Gets the last used location name from conversation context.
    /// </summary>
    public string? GetLastLocationName() => _lastLocationName;

    /// <summary>
    /// Gets the last geocoded location from conversation context.
    /// </summary>
    public Location? GetLastLocation() => _lastLocation;

    /// <summary>
    /// Clears the conversation context memory (location cache).
    /// </summary>
    public void ClearContextMemory()
    {
        _lastLocationName = null;
        _lastLocation = null;
        _logger.LogInformation("Cleared conversation context memory");
    }
}
