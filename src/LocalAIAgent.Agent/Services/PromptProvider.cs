using LocalAIAgent.Agent.Interfaces;
using Microsoft.Extensions.Options;
using LocalAIAgent.Agent.Models;

namespace LocalAIAgent.Agent.Services;

/// <summary>
/// Service for loading system prompts from markdown files.
/// </summary>
public sealed class PromptProvider : IPromptProvider
{
    private readonly AIConfiguration _config;
    private readonly ILogger<PromptProvider> _logger;
    private readonly string _promptsBasePath;

    public PromptProvider(
        IOptions<AIConfiguration> config,
        IWebHostEnvironment environment,
        ILogger<PromptProvider> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Prompts directory is at repository root
        _promptsBasePath = Path.Combine(environment.ContentRootPath, "..", "..", "prompts");
        
        if (!Directory.Exists(_promptsBasePath))
        {
            _logger.LogWarning("Prompts directory not found at {Path}, will create if needed", _promptsBasePath);
        }
    }

    public async Task<string> GetSystemPromptAsync(string? promptName = null)
    {
        var fileName = promptName ?? _config.PromptFile;
        var fullPath = Path.Combine(_promptsBasePath, fileName);

        if (!File.Exists(fullPath))
        {
            var message = $"Prompt file not found: {fullPath} (Working directory: {Directory.GetCurrentDirectory()})";
            _logger.LogError("{Message}", message);
            throw new FileNotFoundException(message, fullPath);
        }

        try
        {
            var content = await File.ReadAllTextAsync(fullPath);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException($"Prompt file is empty: {fullPath}");
            }

            _logger.LogInformation("Loaded system prompt from {Path} ({Length} characters)", fullPath, content.Length);
            return content;
        }
        catch (Exception ex) when (ex is not FileNotFoundException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to read prompt file: {Path}", fullPath);
            throw new InvalidOperationException($"Failed to read prompt file: {fullPath}", ex);
        }
    }
}
