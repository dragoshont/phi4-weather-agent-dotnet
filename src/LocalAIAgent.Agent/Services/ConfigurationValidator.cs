using Microsoft.Extensions.Options;
using LocalAIAgent.Agent.Models;

namespace LocalAIAgent.Agent.Services;

/// <summary>
/// Service for validating AI configuration at startup.
/// </summary>
public sealed class ConfigurationValidator : IHostedService
{
    private readonly AIConfiguration _config;
    private readonly ILogger<ConfigurationValidator> _logger;

    public ConfigurationValidator(
        IOptions<AIConfiguration> config,
        ILogger<ConfigurationValidator> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating AI configuration...");

        // Validate DefaultModel exists in Models dictionary
        if (!_config.Models.ContainsKey(_config.DefaultModel))
        {
            var availableModels = string.Join(", ", _config.Models.Keys);
            var message = $"DefaultModel '{_config.DefaultModel}' not found in Models configuration. " +
                         $"Available models: {availableModels}";
            _logger.LogError("{Message}", message);
            throw new InvalidOperationException(message);
        }

        // Validate each model configuration
        foreach (var (modelKey, modelConfig) in _config.Models)
        {
            _logger.LogInformation("Validating model '{ModelKey}'", modelKey);

            // Validate endpoint URL
            if (!Uri.TryCreate(modelConfig.Endpoint, UriKind.Absolute, out var endpointUri))
            {
                throw new InvalidOperationException($"Model '{modelKey}' has invalid endpoint URL: {modelConfig.Endpoint}");
            }

            // Validate cloud provider has API key
            if (modelConfig.Provider is ProviderType.AzureOpenAI or ProviderType.OpenAI or ProviderType.Gemini)
            {
                if (string.IsNullOrWhiteSpace(modelConfig.ApiKey))
                {
                    _logger.LogWarning("Model '{ModelKey}' is a cloud provider but has no API key configured", modelKey);
                }
            }

            // Validate AzureOpenAI has deployment name
            if (modelConfig.Provider == ProviderType.AzureOpenAI && string.IsNullOrWhiteSpace(modelConfig.DeploymentName))
            {
                throw new InvalidOperationException($"Model '{modelKey}' is AzureOpenAI provider but has no DeploymentName configured");
            }

            _logger.LogInformation("Model '{ModelKey}' validated: Provider={Provider}, Endpoint={Endpoint}, Strategy={Strategy}",
                modelKey, modelConfig.Provider, modelConfig.Endpoint, modelConfig.ToolInvocationStrategy ?? "None");
        }

        _logger.LogInformation("AI configuration validation completed. Default model: {DefaultModel}", _config.DefaultModel);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
