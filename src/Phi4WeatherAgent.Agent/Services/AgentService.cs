using Microsoft.Extensions.AI;

namespace Phi4WeatherAgent.Agent.Services;

/// <summary>
/// Service for managing agent conversation context and orchestrating weather queries.
/// </summary>
public class AgentService
{
    private readonly ILogger<AgentService> _logger;

    public AgentService(ILogger<AgentService> logger)
    {
        _logger = logger;
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

    // TODO T041: Add weather query orchestration flow
    // public async Task<WeatherData> GetWeatherAsync(string locationName, int forecastDays = 7)
    // {
    //     // 1. Call GeocodeTool to get coordinates
    //     // 2. Call WeatherTool with coordinates
    //     // 3. Return WeatherData with structured response
    // }

    // TODO T050: Add allergen query orchestration flow
    // public async Task<AllergenData> GetAllergenLevelsAsync(string locationName)
    // {
    //     // 1. Call GeocodeTool to get coordinates
    //     // 2. Validate Europe region (IsEuropeRegion check)
    //     // 3. Call AllergenTool with coordinates
    //     // 4. Return AllergenData with severity calculation
    // }
}
