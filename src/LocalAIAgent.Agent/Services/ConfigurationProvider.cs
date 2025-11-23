using Microsoft.Extensions.Options;
using LocalAIAgent.Agent.Models;
using System.Text.RegularExpressions;

namespace LocalAIAgent.Agent.Services;

/// <summary>
/// Service for resolving configuration values with environment variable substitution.
/// </summary>
public sealed partial class ConfigurationProvider
{
    private readonly ILogger<ConfigurationProvider> _logger;

    [GeneratedRegex(@"\$\{([^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex EnvVarPattern();

    public ConfigurationProvider(ILogger<ConfigurationProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves environment variable substitutions in configuration values.
    /// Supports ${ENV_VAR_NAME} syntax.
    /// </summary>
    public string ResolveValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var resolved = EnvVarPattern().Replace(value, match =>
        {
            var envVarName = match.Groups[1].Value;
            var envValue = Environment.GetEnvironmentVariable(envVarName);

            if (envValue == null)
            {
                _logger.LogWarning("Environment variable '{EnvVar}' not found, keeping placeholder", envVarName);
                return match.Value; // Keep original ${...} if not found
            }

            _logger.LogDebug("Resolved environment variable '{EnvVar}' (value length: {Length})",
                envVarName, envValue.Length);
            return envValue;
        });

        if (resolved != value)
        {
            _logger.LogInformation("Configuration value resolved with environment variables (precedence: env vars > appsettings)");
        }

        return resolved;
    }

    /// <summary>
    /// Resolves environment variables in model configuration.
    /// </summary>
    public ModelConfiguration ResolveModelConfiguration(ModelConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new ModelConfiguration
        {
            Name = config.Name,
            Provider = config.Provider,
            Endpoint = ResolveValue(config.Endpoint),
            ApiKey = ResolveValue(config.ApiKey),
            DeploymentName = ResolveValue(config.DeploymentName),
            ToolInvocationStrategy = config.ToolInvocationStrategy,
            Parameters = config.Parameters
        };
    }
}
