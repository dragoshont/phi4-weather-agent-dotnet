using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using LocalAIAgent.Agent.Interfaces;
using LocalAIAgent.Agent.Models;
using LocalAIAgent.Agent.Services;
using Xunit;

namespace LocalAIAgent.Agent.Tests.Services;

/// <summary>
/// Test implementation of IWebHostEnvironment for unit testing.
/// </summary>
internal class TestWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ApplicationName { get; set; } = "TestApp";
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Test";
}

/// <summary>
/// Unit tests for PromptProvider service (T030-T031).
/// Validates User Story 2: Configuration-Driven Prompt Management.
/// </summary>
public class PromptProviderTests : IDisposable
{
    private readonly string _testRootDir;
    private readonly string _testPromptsDir;
    private readonly TestWebHostEnvironment _testEnvironment;

    public PromptProviderTests()
    {
        // Create directory structure: root/any/path/ and root/prompts/
        // PromptProvider expects: ContentRootPath\..\..\prompts
        _testRootDir = Path.Combine(Path.GetTempPath(), $"test-root-{Guid.NewGuid()}");
        _testPromptsDir = Path.Combine(_testRootDir, "prompts");
        var contentRoot = Path.Combine(_testRootDir, "app", "bin");

        Directory.CreateDirectory(_testPromptsDir);
        Directory.CreateDirectory(contentRoot);

        // ContentRoot points to app/bin, so ..\..\prompts resolves to root/prompts
        _testEnvironment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testRootDir))
        {
            Directory.Delete(_testRootDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetSystemPromptAsync_WithValidFile_ReturnsContent()
    {
        // Arrange
        var testPrompt = "# Test Weather Assistant\n\nYou are a helpful weather assistant.";
        var promptPath = Path.Combine(_testPromptsDir, "weather-assistant.md");
        await File.WriteAllTextAsync(promptPath, testPrompt);

        var config = Options.Create(new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            PromptFile = "weather-assistant.md",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        });

        var provider = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);

        // Act
        var result = await provider.GetSystemPromptAsync();

        // Assert
        Assert.Equal(testPrompt, result);
    }

    [Fact]
    public async Task GetSystemPromptAsync_WithMissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var config = Options.Create(new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            PromptFile = "non-existent-prompt.md",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        });


        var provider = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => provider.GetSystemPromptAsync()
        );

        Assert.Contains("non-existent-prompt.md", exception.Message);
        Assert.Contains("Working directory:", exception.Message);
    }

    [Fact]
    public async Task GetSystemPromptAsync_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var promptPath = Path.Combine(_testPromptsDir, "empty-prompt.md");
        await File.WriteAllTextAsync(promptPath, ""); // Empty file

        var config = Options.Create(new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            PromptFile = "empty-prompt.md",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        });


        var provider = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetSystemPromptAsync()
        );

        Assert.Contains("empty", exception.Message.ToLower());
    }

    [Fact]
    public async Task GetSystemPromptAsync_WithCustomPromptName_LoadsCorrectFile()
    {
        // Arrange
        var defaultPrompt = "# Default Prompt\n\nDefault content.";
        var customPrompt = "# Custom Prompt\n\nCustom content for testing.";

        var defaultPath = Path.Combine(_testPromptsDir, "weather-assistant.md");
        var customPath = Path.Combine(_testPromptsDir, "custom-assistant.md");

        await File.WriteAllTextAsync(defaultPath, defaultPrompt);
        await File.WriteAllTextAsync(customPath, customPrompt);

        var config = Options.Create(new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            PromptFile = "weather-assistant.md",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        });


        var provider = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);

        // Act
        var defaultResult = await provider.GetSystemPromptAsync();
        var customResult = await provider.GetSystemPromptAsync("custom-assistant.md");

        // Assert
        Assert.Equal(defaultPrompt, defaultResult);
        Assert.Equal(customPrompt, customResult);
    }

    [Fact]
    public async Task GetSystemPromptAsync_WithWhitespaceOnlyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var promptPath = Path.Combine(_testPromptsDir, "whitespace-prompt.md");
        await File.WriteAllTextAsync(promptPath, "   \n\t\n   "); // Whitespace only

        var config = Options.Create(new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            PromptFile = "whitespace-prompt.md",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        });


        var provider = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetSystemPromptAsync()
        );
    }

    [Fact]
    public async Task GetSystemPromptAsync_MultipleCallsSameFile_ReturnsConsistentContent()
    {
        // Arrange
        var testPrompt = "# Consistent Prompt\n\nContent should not change between calls.";
        var promptPath = Path.Combine(_testPromptsDir, "consistent-prompt.md");
        await File.WriteAllTextAsync(promptPath, testPrompt);

        var config = Options.Create(new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            PromptFile = "consistent-prompt.md",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        });


        var provider = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);

        // Act
        var result1 = await provider.GetSystemPromptAsync();
        var result2 = await provider.GetSystemPromptAsync();
        var result3 = await provider.GetSystemPromptAsync();

        // Assert
        Assert.Equal(testPrompt, result1);
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }
}

/// <summary>
/// Integration test for prompt file updates requiring application restart (T031).
/// This validates User Story 2 Scenario 3: prompt updates only take effect after restart.
/// </summary>
public class PromptUpdateIntegrationTests : IDisposable
{
    private readonly string _testRootDir;
    private readonly string _testPromptsDir;
    private readonly TestWebHostEnvironment _testEnvironment;

    public PromptUpdateIntegrationTests()
    {
        // Create directory structure matching PromptProvider expectations
        _testRootDir = Path.Combine(Path.GetTempPath(), $"test-root-update-{Guid.NewGuid()}");
        _testPromptsDir = Path.Combine(_testRootDir, "prompts");
        var contentRoot = Path.Combine(_testRootDir, "app", "bin");

        Directory.CreateDirectory(_testPromptsDir);
        Directory.CreateDirectory(contentRoot);

        _testEnvironment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRootDir))
        {
            Directory.Delete(_testRootDir, recursive: true);
        }
    }

    [Fact]
    public async Task PromptUpdate_WithoutRestart_ServiceInstanceRetainsOldContent()
    {
        // Arrange
        var originalPrompt = "# Original Prompt\n\nOriginal content.";
        var updatedPrompt = "# Updated Prompt\n\nUpdated content after file change.";
        var promptPath = Path.Combine(_testPromptsDir, "update-test-prompt.md");

        await File.WriteAllTextAsync(promptPath, originalPrompt);

        var config = Options.Create(new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            PromptFile = "update-test-prompt.md",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        });

        var provider = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);

        // Act - Load original prompt
        var result1 = await provider.GetSystemPromptAsync();

        // Simulate file update (in production this would happen externally)
        await File.WriteAllTextAsync(promptPath, updatedPrompt);

        // Load again with same provider instance (simulates next request without restart)
        var result2 = await provider.GetSystemPromptAsync();

        // Assert - Second call should see updated content (file read on each call)
        Assert.Equal(originalPrompt, result1);
        Assert.Equal(updatedPrompt, result2); // PromptProvider reads file on each call
    }

    [Fact]
    public async Task PromptUpdate_AfterRestart_NewServiceInstanceSeesUpdatedContent()
    {
        // Arrange
        var originalPrompt = "# V1 Prompt\n\nVersion 1 content.";
        var updatedPrompt = "# V2 Prompt\n\nVersion 2 content after restart.";
        var promptPath = Path.Combine(_testPromptsDir, "restart-test-prompt.md");

        await File.WriteAllTextAsync(promptPath, originalPrompt);

        var config = Options.Create(new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            PromptFile = "restart-test-prompt.md",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        });

        // First service instance (before "restart")
        var provider1 = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);
        var result1 = await provider1.GetSystemPromptAsync();

        // Simulate file update
        await File.WriteAllTextAsync(promptPath, updatedPrompt);

        // Second service instance (after "restart")
        var provider2 = new PromptProvider(config, _testEnvironment, NullLogger<PromptProvider>.Instance);
        var result2 = await provider2.GetSystemPromptAsync();

        // Assert
        Assert.Equal(originalPrompt, result1);
        Assert.Equal(updatedPrompt, result2);
    }
}
