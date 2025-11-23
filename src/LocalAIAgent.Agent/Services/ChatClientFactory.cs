using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using LocalAIAgent.Agent.Interfaces;
using LocalAIAgent.Agent.Models;
using OpenAI;
using Azure.AI.OpenAI;
using System.ClientModel;

namespace LocalAIAgent.Agent.Services;

/// <summary>
/// Factory for creating IChatClient instances with conditional handler registration.
/// </summary>
public sealed class ChatClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AIConfiguration _config;
    private readonly ConfigurationProvider _configProvider;
    private readonly ILogger<ChatClientFactory> _logger;

    public ChatClientFactory(
        IServiceProvider serviceProvider,
        IOptions<AIConfiguration> config,
        ConfigurationProvider configProvider,
        ILogger<ChatClientFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates an IChatClient for the specified model with conditional handler application.
    /// </summary>
    public IChatClient CreateChatClient(string? modelKey = null)
    {
        var key = modelKey ?? _config.DefaultModel;

        if (!_config.Models.TryGetValue(key, out var modelConfig))
        {
            var availableModels = string.Join(", ", _config.Models.Keys);
            throw new InvalidOperationException(
                $"Model '{key}' not found in configuration. Available models: {availableModels}");
        }

        // Resolve environment variables in configuration
        modelConfig = _configProvider.ResolveModelConfiguration(modelConfig);

        _logger.LogInformation("Creating chat client for model '{ModelKey}': Provider={Provider}, Endpoint={Endpoint}",
            key, modelConfig.Provider, modelConfig.Endpoint);

        // Create base client based on provider
        IChatClient baseClient = modelConfig.Provider switch
        {
            ProviderType.Ollama => CreateOllamaClient(modelConfig),
            ProviderType.FoundryLocal => CreateFoundryClient(modelConfig),
            ProviderType.FoundryCloud => CreateFoundryClient(modelConfig),
            ProviderType.AzureOpenAI => CreateAzureOpenAIClient(modelConfig),
            ProviderType.OpenAI => CreateOpenAIClient(modelConfig),
            ProviderType.Gemini => throw new NotImplementedException("Gemini provider not yet implemented"),
            _ => throw new NotSupportedException($"Provider '{modelConfig.Provider}' is not supported")
        };

        // Apply tool invocation handler if configured
        if (!string.IsNullOrEmpty(modelConfig.ToolInvocationStrategy))
        {
            _logger.LogInformation("Applying tool invocation handler: {Strategy}", modelConfig.ToolInvocationStrategy);

            try
            {
                var handler = _serviceProvider.GetRequiredKeyedService<IToolInvocationHandler>(
                    modelConfig.ToolInvocationStrategy);
                baseClient = handler.CreateHandler(baseClient);
            }
            catch (InvalidOperationException ex)
            {
                var message = $"Tool invocation handler '{modelConfig.ToolInvocationStrategy}' not registered. " +
                             "Ensure the handler is registered with AddKeyedSingleton<IToolInvocationHandler>.";
                _logger.LogError(ex, "{Message}", message);
                throw new InvalidOperationException(message, ex);
            }
        }
        else
        {
            _logger.LogInformation("No tool invocation handler configured (native tool support assumed)");

            // Performance warning if handler applied to native tool model
            if (modelConfig.Provider is ProviderType.AzureOpenAI or ProviderType.OpenAI or ProviderType.Gemini)
            {
                _logger.LogWarning("Cloud provider '{Provider}' typically supports native tools. " +
                                  "Consider setting ToolInvocationStrategy=null for better performance.",
                                  modelConfig.Provider);
            }
        }

        return baseClient;
    }

    private IChatClient CreateOllamaClient(ModelConfiguration config)
    {
        _logger.LogDebug("Creating Ollama client: Endpoint={Endpoint}, Model={Model}",
            config.Endpoint, config.Name);
        return new OllamaChatClient(new Uri(config.Endpoint), config.Name);
    }

    private IChatClient CreateFoundryClient(ModelConfiguration config)
    {
        _logger.LogDebug("Creating Foundry client: Endpoint={Endpoint}, Model={Model}",
            config.Endpoint, config.Name);

        var client = new OpenAIClient(
            new ApiKeyCredential(config.ApiKey ?? "not-used"),
            new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) });

        return client.GetChatClient(config.Name).AsIChatClient();
    }

    private IChatClient CreateAzureOpenAIClient(ModelConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException($"API key is required for AzureOpenAI provider (model: {config.Name})");
        }

        if (string.IsNullOrWhiteSpace(config.DeploymentName))
        {
            throw new InvalidOperationException($"DeploymentName is required for AzureOpenAI provider (model: {config.Name})");
        }

        _logger.LogDebug("Creating Azure OpenAI client: Endpoint={Endpoint}, Deployment={Deployment}",
            config.Endpoint, config.DeploymentName);

        var client = new AzureOpenAIClient(
            new Uri(config.Endpoint),
            new ApiKeyCredential(config.ApiKey));

        return client.GetChatClient(config.DeploymentName).AsIChatClient();
    }

    private IChatClient CreateOpenAIClient(ModelConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException($"API key is required for OpenAI provider (model: {config.Name})");
        }

        _logger.LogDebug("Creating OpenAI client: Model={Model}", config.Name);

        var client = new OpenAIClient(new ApiKeyCredential(config.ApiKey));
        return client.GetChatClient(config.Name).AsIChatClient();
    }
}
