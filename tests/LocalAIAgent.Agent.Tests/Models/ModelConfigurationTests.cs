using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using LocalAIAgent.Agent.Models;
using Xunit;

namespace LocalAIAgent.Agent.Tests.Models;

/// <summary>
/// Unit tests for ModelConfiguration and AIConfiguration validation.
/// Tests Options pattern binding, fail-fast behavior, and validation rules.
/// </summary>
public class ModelConfigurationTests
{
    [Fact]
    public void ModelConfiguration_WithValidData_PassesValidation()
    {
        var config = new ModelConfiguration
        {
            Name = "phi-4-mini",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:11434"
        };

        var results = ValidateModel(config);

        Assert.Empty(results);
    }

    [Fact]
    public void ModelConfiguration_WithInvalidEndpoint_FailsValidation()
    {
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = ProviderType.Ollama,
            Endpoint = "not-a-valid-url"
        };

        var results = ValidateModel(config);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("valid URL"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ModelConfiguration_WithOptionalToolInvocationStrategy_PassesValidation(string? strategy)
    {
        var config = new ModelConfiguration
        {
            Name = "gpt-4o",
            Provider = ProviderType.AzureOpenAI,
            Endpoint = "https://api.openai.com",
            ToolInvocationStrategy = strategy
        };

        var results = ValidateModel(config);

        Assert.Empty(results);
    }

    [Fact]
    public void ModelConfiguration_WithEnvironmentVariableApiKey_StoresAsIs()
    {
        var config = new ModelConfiguration
        {
            Name = "gpt-4o",
            Provider = ProviderType.AzureOpenAI,
            Endpoint = "https://api.openai.com",
            ApiKey = "${OPENAI_API_KEY}"
        };

        Assert.Equal("${OPENAI_API_KEY}", config.ApiKey);
        Assert.Empty(ValidateModel(config));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.7)]
    [InlineData(2.0)]
    public void ModelParameters_WithValidTemperature_PassesValidation(double temperature)
    {
        var parameters = new ModelParameters { Temperature = temperature };
        Assert.Empty(ValidateModel(parameters));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    public void ModelParameters_WithInvalidTemperature_FailsValidation(double temperature)
    {
        var parameters = new ModelParameters { Temperature = temperature };
        var results = ValidateModel(parameters);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Temperature must be between"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128000)]
    public void ModelParameters_WithValidMaxTokens_PassesValidation(int maxTokens)
    {
        var parameters = new ModelParameters { MaxTokens = maxTokens };
        Assert.Empty(ValidateModel(parameters));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128001)]
    public void ModelParameters_WithInvalidMaxTokens_FailsValidation(int maxTokens)
    {
        var parameters = new ModelParameters { MaxTokens = maxTokens };
        var results = ValidateModel(parameters);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("MaxTokens must be between"));
    }

    [Fact]
    public void AIConfiguration_WithValidSingleModel_PassesValidation()
    {
        var config = new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        };

        Assert.Empty(ValidateModel(config));
    }

    [Fact]
    public void AIConfiguration_WithEmptyModels_FailsValidation()
    {
        var config = new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            Models = new Dictionary<string, ModelConfiguration>()
        };

        var results = ValidateModel(config);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("At least one model must be configured"));
    }

    [Fact]
    public void AIConfiguration_WithDefaultPromptFile_UsesWeatherAssistant()
    {
        var config = new AIConfiguration
        {
            DefaultModel = "phi-4-mini",
            Models = new Dictionary<string, ModelConfiguration>
            {
                ["phi-4-mini"] = new ModelConfiguration
                {
                    Name = "phi-4-mini",
                    Provider = ProviderType.Ollama,
                    Endpoint = "http://localhost:11434"
                }
            }
        };

        Assert.Equal("weather-assistant.md", config.PromptFile);
    }

    [Fact]
    public void AIConfiguration_BindsFromIConfiguration_Successfully()
    {
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "phi-4-mini",
            ["AI:Models:phi-4-mini:Name"] = "phi-4-mini",
            ["AI:Models:phi-4-mini:Provider"] = "Ollama",
            ["AI:Models:phi-4-mini:Endpoint"] = "http://localhost:11434",
            ["AI:Models:phi-4-mini:ToolInvocationStrategy"] = "Functools"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<AIConfiguration>()
            .Bind(configuration.GetSection("AI"));
        services.AddSingleton<IConfiguration>(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;

        Assert.NotNull(aiConfig);
        Assert.Equal("phi-4-mini", aiConfig.DefaultModel);
        Assert.True(aiConfig.Models.ContainsKey("phi-4-mini"));
        Assert.Equal("phi-4-mini", aiConfig.Models["phi-4-mini"].Name);
        Assert.Equal(ProviderType.Ollama, aiConfig.Models["phi-4-mini"].Provider);
        Assert.Equal("http://localhost:11434", aiConfig.Models["phi-4-mini"].Endpoint);
        Assert.Equal("Functools", aiConfig.Models["phi-4-mini"].ToolInvocationStrategy);
    }

    [Fact]
    public void AIConfiguration_WithEnvironmentVariableSubstitution_StoresRawValue()
    {
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "gpt-4o",
            ["AI:Models:gpt-4o:Name"] = "gpt-4o",
            ["AI:Models:gpt-4o:Provider"] = "AzureOpenAI",
            ["AI:Models:gpt-4o:Endpoint"] = "https://api.openai.com",
            ["AI:Models:gpt-4o:ApiKey"] = "${OPENAI_API_KEY}"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<AIConfiguration>()
            .Bind(configuration.GetSection("AI"));
        services.AddSingleton<IConfiguration>(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;

        Assert.Equal("${OPENAI_API_KEY}", aiConfig.Models["gpt-4o"].ApiKey);
    }

    [Fact]
    public void ModelParameters_BindsFromIConfiguration_Successfully()
    {
        var configData = new Dictionary<string, string?>
        {
            ["AI:DefaultModel"] = "phi-4-mini",
            ["AI:Models:phi-4-mini:Name"] = "phi-4-mini",
            ["AI:Models:phi-4-mini:Provider"] = "Ollama",
            ["AI:Models:phi-4-mini:Endpoint"] = "http://localhost:11434",
            ["AI:Models:phi-4-mini:Parameters:Temperature"] = "0.7",
            ["AI:Models:phi-4-mini:Parameters:MaxTokens"] = "4096"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<AIConfiguration>()
            .Bind(configuration.GetSection("AI"));
        services.AddSingleton<IConfiguration>(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var aiConfig = serviceProvider.GetRequiredService<IOptions<AIConfiguration>>().Value;
        var parameters = aiConfig.Models["phi-4-mini"].Parameters;

        Assert.NotNull(parameters);
        Assert.Equal(0.7, parameters!.Temperature);
        Assert.Equal(4096, parameters.MaxTokens);
    }

    [Theory]
    [InlineData(ProviderType.Ollama)]
    [InlineData(ProviderType.FoundryLocal)]
    [InlineData(ProviderType.AzureOpenAI)]
    [InlineData(ProviderType.OpenAI)]
    [InlineData(ProviderType.Gemini)]
    public void ModelConfiguration_WithAllProviderTypes_PassesValidation(ProviderType provider)
    {
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = provider,
            Endpoint = "http://localhost:11434"
        };

        Assert.Empty(ValidateModel(config));
    }

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
