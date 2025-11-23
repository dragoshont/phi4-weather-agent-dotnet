namespace LocalAIAgent.Agent.Models;

/// <summary>
/// Enumeration of supported AI provider types.
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// Ollama local inference server (Linux/macOS, supports Qwen, Llama, etc.)
    /// </summary>
    Ollama,

    /// <summary>
    /// Azure Foundry Local (Windows/macOS, supports Phi-4 Mini, etc.)
    /// </summary>
    FoundryLocal,

    /// <summary>
    /// Azure Foundry Cloud (Azure-hosted models)
    /// </summary>
    FoundryCloud,

    /// <summary>
    /// Azure OpenAI Service
    /// </summary>
    AzureOpenAI,

    /// <summary>
    /// OpenAI Platform (OpenAI.com)
    /// </summary>
    OpenAI,

    /// <summary>
    /// Google Gemini
    /// </summary>
    Gemini
}
