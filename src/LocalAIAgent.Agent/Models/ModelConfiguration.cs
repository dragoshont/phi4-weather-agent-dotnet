using System.ComponentModel.DataAnnotations;

namespace LocalAIAgent.Agent.Models;

/// <summary>
/// Configuration for a single AI model.
/// Maps to "AI:Models:{ModelKey}" configuration section.
/// </summary>
public sealed record ModelConfiguration
{
    /// <summary>
    /// Unique identifier for the model (e.g., "phi-4-mini", "qwen2.5-vl-3b")
    /// </summary>
    [Required(ErrorMessage = "Model name is required")]
    public required string Name { get; init; }

    /// <summary>
    /// AI provider type (Ollama, FoundryLocal, AzureOpenAI, etc.)
    /// </summary>
    [Required(ErrorMessage = "Provider is required")]
    public required ProviderType Provider { get; init; }

    /// <summary>
    /// Provider endpoint URL
    /// </summary>
    [Required(ErrorMessage = "Endpoint is required")]
    [Url(ErrorMessage = "Endpoint must be a valid URL")]
    public required string Endpoint { get; init; }

    /// <summary>
    /// Tool invocation strategy (optional).
    /// - "Functools": Apply FunctoolsHandler middleware
    /// - "ReActJSON": Apply ReActJSONHandler middleware (example)
    /// - null/empty: Native tool support, no handler applied
    /// </summary>
    public string? ToolInvocationStrategy { get; init; }

    /// <summary>
    /// API key for cloud providers (optional for local models).
    /// Supports environment variable substitution: ${ENV_VAR_NAME}
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Deployment name for Azure OpenAI (required for AzureOpenAI provider)
    /// </summary>
    public string? DeploymentName { get; init; }

    /// <summary>
    /// Model-specific parameters (temperature, max tokens, etc.)
    /// </summary>
    public ModelParameters? Parameters { get; init; }
}

/// <summary>
/// Model-specific inference parameters.
/// </summary>
public sealed record ModelParameters
{
    /// <summary>
    /// Sampling temperature (0.0 - 2.0, default 0.7)
    /// </summary>
    [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0.0 and 2.0")]
    public double? Temperature { get; init; }

    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    [Range(1, 128000, ErrorMessage = "MaxTokens must be between 1 and 128000")]
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Top-p nucleus sampling (0.0 - 1.0)
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "TopP must be between 0.0 and 1.0")]
    public double? TopP { get; init; }

    /// <summary>
    /// Frequency penalty (-2.0 - 2.0)
    /// </summary>
    [Range(-2.0, 2.0, ErrorMessage = "FrequencyPenalty must be between -2.0 and 2.0")]
    public double? FrequencyPenalty { get; init; }

    /// <summary>
    /// Presence penalty (-2.0 - 2.0)
    /// </summary>
    [Range(-2.0, 2.0, ErrorMessage = "PresencePenalty must be between -2.0 and 2.0")]
    public double? PresencePenalty { get; init; }
}

/// <summary>
/// Root AI configuration containing all models and default selection.
/// Maps to "AI" configuration section.
/// </summary>
public sealed record AIConfiguration
{
    /// <summary>
    /// Dictionary of available models, keyed by model identifier.
    /// Maps to "AI:Models" configuration section.
    /// </summary>
    [Required(ErrorMessage = "At least one model must be configured")]
    [MinLength(1, ErrorMessage = "At least one model must be configured")]
    public required Dictionary<string, ModelConfiguration> Models { get; init; }

    /// <summary>
    /// Default model identifier (must exist in Models dictionary).
    /// Maps to "AI:DefaultModel" configuration value.
    /// </summary>
    [Required(ErrorMessage = "DefaultModel is required")]
    public required string DefaultModel { get; init; }

    /// <summary>
    /// Prompt file name (relative to prompts/ directory, defaults to "weather-assistant.md")
    /// </summary>
    public string PromptFile { get; init; } = "weather-assistant.md";
}
