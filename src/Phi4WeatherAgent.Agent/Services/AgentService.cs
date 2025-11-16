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
}
