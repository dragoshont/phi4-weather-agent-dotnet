using LocalAIAgent.Agent.Interfaces;
using LocalAIAgent.Agent.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace LocalAIAgent.Agent.Tests.Services;

/// <summary>
/// Tests for ToolInvocationStrategy → IToolInvocationHandler resolution logic.
/// Validates keyed service lookup behavior:
/// - null/empty strategy = no handler
/// - "Functools" = FunctoolsHandler
/// - invalid key = exception
/// </summary>
public class HandlerResolutionTests
{
    [Fact]
    public void ResolveHandler_WithNullStrategy_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, TestHandler>("Functools");

        var serviceProvider = services.BuildServiceProvider();
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            ToolInvocationStrategy = null // No handler
        };

        // Act
        var handler = string.IsNullOrEmpty(config.ToolInvocationStrategy)
            ? null
            : serviceProvider.GetKeyedService<IToolInvocationHandler>(config.ToolInvocationStrategy);

        // Assert
        Assert.Null(handler);
    }

    [Fact]
    public void ResolveHandler_WithEmptyStrategy_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, TestHandler>("Functools");

        var serviceProvider = services.BuildServiceProvider();
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            ToolInvocationStrategy = string.Empty // No handler
        };

        // Act
        var handler = string.IsNullOrEmpty(config.ToolInvocationStrategy)
            ? null
            : serviceProvider.GetKeyedService<IToolInvocationHandler>(config.ToolInvocationStrategy);

        // Assert
        Assert.Null(handler);
    }

    [Fact]
    public void ResolveHandler_WithFunctoolsStrategy_ReturnsFunctoolsHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, TestHandler>("Functools");

        var serviceProvider = services.BuildServiceProvider();
        var config = new ModelConfiguration
        {
            Name = "phi-4-mini",
            Provider = ProviderType.FoundryLocal,
            Endpoint = "http://localhost:5272",
            ToolInvocationStrategy = "Functools"
        };

        // Act
        var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>(config.ToolInvocationStrategy!);

        // Assert
        Assert.NotNull(handler);
        Assert.IsType<TestHandler>(handler);
    }

    [Fact]
    public void ResolveHandler_WithInvalidKey_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, TestHandler>("Functools");

        var serviceProvider = services.BuildServiceProvider();
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            ToolInvocationStrategy = "InvalidHandler"
        };

        // Act
        var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>(config.ToolInvocationStrategy!);

        // Assert
        // GetKeyedService returns null for missing keys (vs GetRequiredKeyedService which throws)
        Assert.Null(handler);
    }

    [Fact]
    public void ResolveHandler_WithInvalidKey_GetRequiredThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, TestHandler>("Functools");

        var serviceProvider = services.BuildServiceProvider();
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            ToolInvocationStrategy = "InvalidHandler"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredKeyedService<IToolInvocationHandler>(config.ToolInvocationStrategy!));

        Assert.NotNull(exception);
        Assert.Contains("No keyed service", exception.Message);
    }

    [Fact]
    public void ResolveHandler_WithMultipleHandlers_RetrievesCorrectOne()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, TestHandler>("Functools");
        services.AddKeyedSingleton<IToolInvocationHandler, AlternateHandler>("ReActJSON");

        var serviceProvider = services.BuildServiceProvider();

        // Act - Resolve Functools
        var functoolsHandler = serviceProvider.GetKeyedService<IToolInvocationHandler>("Functools");

        // Act - Resolve ReActJSON
        var reactHandler = serviceProvider.GetKeyedService<IToolInvocationHandler>("ReActJSON");

        // Assert
        Assert.NotNull(functoolsHandler);
        Assert.NotNull(reactHandler);
        Assert.IsType<TestHandler>(functoolsHandler);
        Assert.IsType<AlternateHandler>(reactHandler);
        Assert.NotEqual(functoolsHandler.GetType(), reactHandler.GetType());
    }

    [Fact]
    public void ResolveHandler_ConfigDrivenSelection_WorksDynamically()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, TestHandler>("Functools");
        services.AddKeyedSingleton<IToolInvocationHandler, AlternateHandler>("ReActJSON");

        var serviceProvider = services.BuildServiceProvider();

        var configs = new[]
        {
            new ModelConfiguration
            {
                Name = "phi-4-mini",
                Provider = ProviderType.FoundryLocal,
                Endpoint = "http://localhost:5272",
                ToolInvocationStrategy = "Functools"
            },
            new ModelConfiguration
            {
                Name = "custom-model",
                Provider = ProviderType.Ollama,
                Endpoint = "http://localhost:11434",
                ToolInvocationStrategy = "ReActJSON"
            },
            new ModelConfiguration
            {
                Name = "gpt-4o",
                Provider = ProviderType.AzureOpenAI,
                Endpoint = "https://api.openai.azure.com",
                ToolInvocationStrategy = null // Native tools
            }
        };

        // Act & Assert
        foreach (var config in configs)
        {
            IToolInvocationHandler? handler = null;
            if (!string.IsNullOrEmpty(config.ToolInvocationStrategy))
            {
                handler = serviceProvider.GetKeyedService<IToolInvocationHandler>(config.ToolInvocationStrategy);
            }

            if (config.ToolInvocationStrategy == "Functools")
            {
                Assert.NotNull(handler);
                Assert.IsType<TestHandler>(handler);
            }
            else if (config.ToolInvocationStrategy == "ReActJSON")
            {
                Assert.NotNull(handler);
                Assert.IsType<AlternateHandler>(handler);
            }
            else
            {
                Assert.Null(handler); // Native tool support
            }
        }
    }

    // Test doubles
    private class TestHandler : IToolInvocationHandler
    {
        public IChatClient CreateHandler(IChatClient innerClient)
        {
            return innerClient; // Passthrough for testing
        }
    }

    private class AlternateHandler : IToolInvocationHandler
    {
        public IChatClient CreateHandler(IChatClient innerClient)
        {
            return innerClient; // Passthrough for testing
        }
    }
}
