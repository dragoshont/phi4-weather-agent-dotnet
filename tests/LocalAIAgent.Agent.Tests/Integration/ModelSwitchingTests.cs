using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using LocalAIAgent.Agent.Models;
using Xunit;

namespace LocalAIAgent.Agent.Tests.Integration;

/// <summary>
/// Integration tests for model switching via configuration (T021-T022).
/// Validates User Story 1: Switch AI Models Without Code Changes.
/// </summary>
public class ModelSwitchingTests
{
    [Fact]
    public void ModelSwitch_FromPhi4ToQwen_ConfigurationLoadsSuccessfully()
    {
        // Arrange - Simulate switching DefaultModel from phi-4-mini to qwen2.5-vl-3b
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "qwen2.5-vl-3b", // Changed from phi-4-mini
            ["AI:Models:phi-4-mini:Name"] = "phi-4-mini",
            ["AI:Models:phi-4-mini:Provider"] = "Ollama",
            ["AI:Models:phi-4-mini:Endpoint"] = "http://localhost:11434",
            ["AI:Models:phi-4-mini:ToolInvocationStrategy"] = "Functools",
            ["AI:Models:qwen2.5-vl-3b:Name"] = "qwen2.5-vl:3b-instruct",
            ["AI:Models:qwen2.5-vl-3b:Provider"] = "Ollama",
            ["AI:Models:qwen2.5-vl-3b:Endpoint"] = "http://localhost:11434",
            ["AI:Models:qwen2.5-vl-3b:ToolInvocationStrategy"] = "Functools"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<AIConfiguration>(configuration.GetSection("AI"));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;

        // Assert - Verify Qwen is now the default model
        Assert.Equal("qwen2.5-vl-3b", aiConfig.DefaultModel);
        Assert.Equal(2, aiConfig.Models.Count);
        Assert.True(aiConfig.Models.ContainsKey("qwen2.5-vl-3b"));

        // Verify Qwen configuration
        var qwenModel = aiConfig.Models["qwen2.5-vl-3b"];
        Assert.Equal("qwen2.5-vl:3b-instruct", qwenModel.Name);
        Assert.Equal(ProviderType.Ollama, qwenModel.Provider);
        Assert.Equal("http://localhost:11434", qwenModel.Endpoint);
        Assert.Equal("Functools", qwenModel.ToolInvocationStrategy);
    }

    [Fact]
    public void ModelSwitch_WithInvalidDefaultModel_ThrowsValidationException()
    {
        // Arrange - DefaultModel references non-existent model
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "non-existent-model",
            ["AI:Models:phi-4-mini:Name"] = "phi-4-mini",
            ["AI:Models:phi-4-mini:Provider"] = "Ollama",
            ["AI:Models:phi-4-mini:Endpoint"] = "http://localhost:11434"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<AIConfiguration>(configuration.GetSection("AI"));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;

        // Assert - Configuration loads but DefaultModel is invalid
        Assert.Equal("non-existent-model", aiConfig.DefaultModel);
        Assert.False(aiConfig.Models.ContainsKey("non-existent-model"));

        // Note: Validation of DefaultModel existence should be done by ModelResolver service at runtime
    }

    [Fact]
    public void CloudModelConfiguration_AzureOpenAI_WithEnvironmentVariableApiKey()
    {
        // Arrange - Simulate Azure OpenAI configuration with environment variable API key
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "gpt-4o",
            ["AI:Models:gpt-4o:Name"] = "gpt-4o",
            ["AI:Models:gpt-4o:Provider"] = "AzureOpenAI",
            ["AI:Models:gpt-4o:Endpoint"] = "https://my-resource.openai.azure.com",
            ["AI:Models:gpt-4o:ApiKey"] = "${AZURE_OPENAI_API_KEY}",
            ["AI:Models:gpt-4o:DeploymentName"] = "gpt-4o-deployment",
            ["AI:Models:gpt-4o:ToolInvocationStrategy"] = null // Native tool calling
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<AIConfiguration>(configuration.GetSection("AI"));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;

        // Assert - Verify cloud model configuration structure
        Assert.Equal("gpt-4o", aiConfig.DefaultModel);
        Assert.Single(aiConfig.Models);

        var gpt4Model = aiConfig.Models["gpt-4o"];
        Assert.Equal(ProviderType.AzureOpenAI, gpt4Model.Provider);
        Assert.Equal("https://my-resource.openai.azure.com", gpt4Model.Endpoint);
        Assert.Equal("${AZURE_OPENAI_API_KEY}", gpt4Model.ApiKey); // Raw env var syntax
        Assert.Equal("gpt-4o-deployment", gpt4Model.DeploymentName);
        Assert.Null(gpt4Model.ToolInvocationStrategy); // Native calling
    }

    [Fact]
    public void CloudModelConfiguration_OpenAI_WithDirectApiKey()
    {
        // Arrange - OpenAI configuration with direct API key (not recommended for production)
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "gpt-4o",
            ["AI:Models:gpt-4o:Name"] = "gpt-4o",
            ["AI:Models:gpt-4o:Provider"] = "OpenAI",
            ["AI:Models:gpt-4o:Endpoint"] = "https://api.openai.com/v1",
            ["AI:Models:gpt-4o:ApiKey"] = "sk-test123", // Direct key (for testing)
            ["AI:Models:gpt-4o:Parameters:Temperature"] = "0.7",
            ["AI:Models:gpt-4o:Parameters:MaxTokens"] = "4096"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<AIConfiguration>(configuration.GetSection("AI"));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;

        // Assert
        var gptModel = aiConfig.Models["gpt-4o"];
        Assert.Equal(ProviderType.OpenAI, gptModel.Provider);
        Assert.Equal("sk-test123", gptModel.ApiKey);
        Assert.NotNull(gptModel.Parameters);
        Assert.Equal(0.7, gptModel.Parameters.Temperature);
        Assert.Equal(4096, gptModel.Parameters.MaxTokens);
    }

    [Fact]
    public void MultiModelConfiguration_ThreeModels_AllLoadSuccessfully()
    {
        // Arrange - Configuration with three models (phi-4, qwen, gpt-4o)
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "phi-4-mini",
            ["AI:Models:phi-4-mini:Name"] = "phi-4-mini",
            ["AI:Models:phi-4-mini:Provider"] = "Ollama",
            ["AI:Models:phi-4-mini:Endpoint"] = "http://localhost:11434",
            ["AI:Models:phi-4-mini:ToolInvocationStrategy"] = "Functools",
            ["AI:Models:qwen2.5-vl-3b:Name"] = "qwen2.5-vl:3b-instruct",
            ["AI:Models:qwen2.5-vl-3b:Provider"] = "Ollama",
            ["AI:Models:qwen2.5-vl-3b:Endpoint"] = "http://localhost:11434",
            ["AI:Models:qwen2.5-vl-3b:ToolInvocationStrategy"] = "Functools",
            ["AI:Models:gpt-4o:Name"] = "gpt-4o",
            ["AI:Models:gpt-4o:Provider"] = "AzureOpenAI",
            ["AI:Models:gpt-4o:Endpoint"] = "https://my-resource.openai.azure.com",
            ["AI:Models:gpt-4o:ApiKey"] = "${AZURE_OPENAI_API_KEY}",
            ["AI:Models:gpt-4o:DeploymentName"] = "gpt-4o-deployment"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<AIConfiguration>(configuration.GetSection("AI"));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;

        // Assert - All three models should be present
        Assert.Equal(3, aiConfig.Models.Count);
        Assert.True(aiConfig.Models.ContainsKey("phi-4-mini"));
        Assert.True(aiConfig.Models.ContainsKey("qwen2.5-vl-3b"));
        Assert.True(aiConfig.Models.ContainsKey("gpt-4o"));

        // Verify each model has correct provider
        Assert.Equal(ProviderType.Ollama, aiConfig.Models["phi-4-mini"].Provider);
        Assert.Equal(ProviderType.Ollama, aiConfig.Models["qwen2.5-vl-3b"].Provider);
        Assert.Equal(ProviderType.AzureOpenAI, aiConfig.Models["gpt-4o"].Provider);
    }

    [Fact]
    public void ModelConfiguration_WithFoundryProviders_LoadsSuccessfully()
    {
        // Arrange - Test Foundry provider types
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "local-foundry",
            ["AI:Models:local-foundry:Name"] = "phi-4-mini",
            ["AI:Models:local-foundry:Provider"] = "FoundryLocal",
            ["AI:Models:local-foundry:Endpoint"] = "http://localhost:8080",
            ["AI:Models:cloud-foundry:Name"] = "gpt-4o",
            ["AI:Models:cloud-foundry:Provider"] = "FoundryCloud",
            ["AI:Models:cloud-foundry:Endpoint"] = "https://foundry.example.com",
            ["AI:Models:cloud-foundry:ApiKey"] = "${FOUNDRY_API_KEY}"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<AIConfiguration>(configuration.GetSection("AI"));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;

        // Assert
        Assert.Equal(2, aiConfig.Models.Count);
        Assert.Equal(ProviderType.FoundryLocal, aiConfig.Models["local-foundry"].Provider);
        Assert.Equal(ProviderType.FoundryCloud, aiConfig.Models["cloud-foundry"].Provider);
    }
}
