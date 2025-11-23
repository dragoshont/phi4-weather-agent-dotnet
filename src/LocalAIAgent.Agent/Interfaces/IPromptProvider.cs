namespace LocalAIAgent.Agent.Interfaces;

/// <summary>
/// Interface for loading system prompts from external sources (e.g., markdown files).
/// Enables configuration-driven prompt management without code changes.
/// </summary>
public interface IPromptProvider
{
    /// <summary>
    /// Retrieves the system prompt for the agent.
    /// </summary>
    /// <param name="promptName">Optional name of the prompt to load (defaults to primary prompt)</param>
    /// <returns>System prompt text</returns>
    /// <exception cref="FileNotFoundException">When prompt file does not exist</exception>
    /// <exception cref="InvalidOperationException">When prompt content is empty or invalid</exception>
    Task<string> GetSystemPromptAsync(string? promptName = null);
}
