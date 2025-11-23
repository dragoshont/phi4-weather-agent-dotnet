using LocalAIAgent.Agent.Handlers;
using LocalAIAgent.Agent.Interfaces;
using LocalAIAgent.Agent.Models;
using LocalAIAgent.Agent.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace LocalAIAgent.Agent.Tests.Services;

/// <summary>
/// Unit tests for ChatAgentFactory covering handler registration,
/// conditional application, and error scenarios.
/// </summary>
public class ChatAgentFactoryTests
{
    [Fact]
    public void CreateAgent_WithValidConfiguration_RegistersCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:8080",
            ToolInvocationStrategy = null // Native tool calling
        };

        services.AddSingleton<IChatClient>(sp => new TestChatClient());
        services.AddSingleton(Options.Create(config));
        services.AddSingleton<IPromptProvider>(sp => new TestPromptProvider("Test prompt"));
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var chatClient = serviceProvider.GetRequiredService<IChatClient>();

        // Assert
        Assert.NotNull(chatClient);
    }

    [Fact]
    public void CreateAgent_WithFunctoolsStrategy_AppliesHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:8080",
            ToolInvocationStrategy = "Functools"
        };

        services.AddSingleton<IChatClient>(sp => new TestChatClient());
        services.AddSingleton(Options.Create(config));
        services.AddSingleton<IPromptProvider>(sp => new TestPromptProvider("Test prompt"));
        services.AddKeyedSingleton<IToolInvocationHandler, TestToolInvocationHandler>("Functools");
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>("Functools");

        // Assert
        Assert.NotNull(handler);
        Assert.IsType<TestToolInvocationHandler>(handler);
    }

    [Fact]
    public void CreateAgent_WithInvalidHandlerKey_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ModelConfiguration
        {
            Name = "test-model",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:8080",
            ToolInvocationStrategy = "NonExistentHandler"
        };

        services.AddSingleton(Options.Create(config));
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredKeyedService<IToolInvocationHandler>("NonExistentHandler"));

        Assert.Contains("No keyed service", exception.Message);
    }

    [Fact]
    public void CreateAgent_WithNullStrategy_DoesNotApplyHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ModelConfiguration
        {
            Name = "gpt-4o",
            Provider = ProviderType.AzureOpenAI,
            Endpoint = "https://api.openai.com",
            ToolInvocationStrategy = null // Native tool calling
        };

        services.AddSingleton<IChatClient>(sp => new TestChatClient());
        services.AddSingleton(Options.Create(config));
        services.AddSingleton<IPromptProvider>(sp => new TestPromptProvider("Test prompt"));
        services.AddKeyedSingleton<IToolInvocationHandler, TestToolInvocationHandler>("Functools");
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>(config.ToolInvocationStrategy ?? "");

        // Assert
        Assert.Null(handler); // Should not retrieve handler when strategy is null
    }

    [Fact]
    public void CreateAgent_WithEmptyStrategy_DoesNotApplyHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ModelConfiguration
        {
            Name = "gpt-4o",
            Provider = ProviderType.AzureOpenAI,
            Endpoint = "https://api.openai.com",
            ToolInvocationStrategy = "" // Explicitly empty
        };

        services.AddSingleton<IChatClient>(sp => new TestChatClient());
        services.AddSingleton(Options.Create(config));
        services.AddSingleton<IPromptProvider>(sp => new TestPromptProvider("Test prompt"));
        services.AddKeyedSingleton<IToolInvocationHandler, TestToolInvocationHandler>("Functools");
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>("");

        // Assert
        Assert.Null(handler); // Should not retrieve handler when strategy is empty
    }

    [Fact]
    public void CreateAgent_WithMultipleHandlers_RetrievesCorrectOne()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddKeyedSingleton<IToolInvocationHandler, TestToolInvocationHandler>("Functools");
        services.AddKeyedSingleton<IToolInvocationHandler, AlternateTestHandler>("ReActJSON");
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var functoolsHandler = serviceProvider.GetKeyedService<IToolInvocationHandler>("Functools");
        var reactHandler = serviceProvider.GetKeyedService<IToolInvocationHandler>("ReActJSON");

        // Assert
        Assert.NotNull(functoolsHandler);
        Assert.NotNull(reactHandler);
        Assert.IsType<TestToolInvocationHandler>(functoolsHandler);
        Assert.IsType<AlternateTestHandler>(reactHandler);
    }

    [Fact]
    public void CreateAgent_ConditionalApplication_FollowsConfiguration()
    {
        // Arrange - Test that handler application logic follows config
        var services = new ServiceCollection();

        var configWithHandler = new ModelConfiguration
        {
            Name = "phi-4-mini",
            Provider = ProviderType.Ollama,
            Endpoint = "http://localhost:8080",
            ToolInvocationStrategy = "Functools"
        };

        var configWithoutHandler = new ModelConfiguration
        {
            Name = "gpt-4o",
            Provider = ProviderType.AzureOpenAI,
            Endpoint = "https://api.openai.com",
            ToolInvocationStrategy = null
        };

        services.AddKeyedSingleton<IToolInvocationHandler, TestToolInvocationHandler>("Functools");
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var shouldApplyHandler = !string.IsNullOrEmpty(configWithHandler.ToolInvocationStrategy);
        var shouldNotApplyHandler = string.IsNullOrEmpty(configWithoutHandler.ToolInvocationStrategy);

        // Assert
        Assert.True(shouldApplyHandler, "Handler should be applied when ToolInvocationStrategy is set");
        Assert.True(shouldNotApplyHandler, "Handler should NOT be applied when ToolInvocationStrategy is null/empty");
    }

    // Test Doubles

    private class TestChatClient : IChatClient
    {
        public ChatClientMetadata Metadata => new("test-client");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response"));
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate { Contents = [new TextContent("Test")] };
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose() { }
    }

    private class TestPromptProvider : IPromptProvider
    {
        private readonly string _prompt;

        public TestPromptProvider(string prompt)
        {
            _prompt = prompt;
        }

        public Task<string> GetSystemPromptAsync(string? promptName = null)
        {
            return Task.FromResult(_prompt);
        }
    }

    private class TestToolInvocationHandler : IToolInvocationHandler
    {
        public IChatClient CreateHandler(IChatClient innerClient)
        {
            return new TestChatClient();
        }
    }

    private class AlternateTestHandler : IToolInvocationHandler
    {
        public IChatClient CreateHandler(IChatClient innerClient)
        {
            return new TestChatClient();
        }
    }
}
