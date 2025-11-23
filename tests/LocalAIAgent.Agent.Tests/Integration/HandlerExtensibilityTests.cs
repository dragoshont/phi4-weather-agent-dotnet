using LocalAIAgent.Agent.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LocalAIAgent.Agent.Tests.Integration;

/// <summary>
/// Integration tests verifying that new tool invocation handlers
/// can be added via keyed DI registration without modifying core code.
/// Demonstrates extensibility of the handler system.
/// </summary>
public class HandlerExtensibilityTests
{
    [Fact]
    public void AddNewHandler_ViaKeyedDI_IntegratesWithoutCoreChanges()
    {
        // Arrange - Simulate adding a new handler type
        var services = new ServiceCollection();

        // Register existing handlers (would be in Startup/Program.cs)
        services.AddKeyedSingleton<IToolInvocationHandler, FunctoolsHandlerStub>("Functools");

        // NEW: Add custom handler without modifying core code
        services.AddKeyedSingleton<IToolInvocationHandler, CustomReActHandler>("ReActJSON");

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var functoolsHandler = serviceProvider.GetKeyedService<IToolInvocationHandler>("Functools");
        var reactHandler = serviceProvider.GetKeyedService<IToolInvocationHandler>("ReActJSON");

        // Assert
        Assert.NotNull(functoolsHandler);
        Assert.NotNull(reactHandler);
        Assert.IsType<FunctoolsHandlerStub>(functoolsHandler);
        Assert.IsType<CustomReActHandler>(reactHandler);
    }

    [Fact]
    public void MultipleHandlers_CanCoexist_WithoutConflicts()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddKeyedSingleton<IToolInvocationHandler, FunctoolsHandlerStub>("Functools");
        services.AddKeyedSingleton<IToolInvocationHandler, CustomReActHandler>("ReActJSON");
        services.AddKeyedSingleton<IToolInvocationHandler, CustomXMLHandler>("ReActXML");

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var allHandlers = new[]
        {
            serviceProvider.GetKeyedService<IToolInvocationHandler>("Functools"),
            serviceProvider.GetKeyedService<IToolInvocationHandler>("ReActJSON"),
            serviceProvider.GetKeyedService<IToolInvocationHandler>("ReActXML")
        };

        // Assert
        Assert.All(allHandlers, handler => Assert.NotNull(handler));
        Assert.Equal(3, allHandlers.Length);
    }

    [Fact]
    public void HandlerDiscovery_SupportsRuntimeRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Simulate runtime handler registration (e.g., plugin system)
        var handlerTypes = new[]
        {
            ("Functools", typeof(FunctoolsHandlerStub)),
            ("ReActJSON", typeof(CustomReActHandler)),
            ("ReActXML", typeof(CustomXMLHandler))
        };

        foreach (var (key, type) in handlerTypes)
        {
            services.AddKeyedSingleton(typeof(IToolInvocationHandler), key, type);
        }

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var registeredHandlers = handlerTypes
            .Select(ht => serviceProvider.GetKeyedService<IToolInvocationHandler>(ht.Item1))
            .ToList();

        // Assert
        Assert.Equal(3, registeredHandlers.Count);
        Assert.All(registeredHandlers, handler => Assert.NotNull(handler));
    }

    [Fact]
    public void CustomHandler_ImplementsInterface_IntegratesSeamlessly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, CustomReActHandler>("ReActJSON");

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>("ReActJSON");
        var testClient = new StubChatClient("inner");
        var wrappedClient = handler!.CreateHandler(testClient);

        // Assert
        Assert.NotNull(handler);
        Assert.NotNull(wrappedClient);
        // Verify the handler created a wrapped client
        Assert.IsAssignableFrom<IChatClient>(wrappedClient);
    }

    [Fact]
    public void HandlerSelection_BasedOnConfiguration_WorksDynamically()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IToolInvocationHandler, FunctoolsHandlerStub>("Functools");
        services.AddKeyedSingleton<IToolInvocationHandler, CustomReActHandler>("ReActJSON");

        var serviceProvider = services.BuildServiceProvider();

        // Simulate configuration-driven selection
        var strategies = new[] { "Functools", "ReActJSON", null };

        // Act & Assert
        foreach (var strategy in strategies)
        {
            if (string.IsNullOrEmpty(strategy))
            {
                var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>(strategy ?? "");
                Assert.Null(handler); // No handler for null/empty strategy
            }
            else
            {
                var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>(strategy);
                Assert.NotNull(handler);
            }
        }
    }

    [Fact]
    public void NewHandler_DoesNotRequire_CoreCodeModification()
    {
        // This test documents the extensibility contract:
        // Adding a new handler requires ONLY:
        // 1. Implement IToolInvocationHandler
        // 2. Register with keyed DI
        // 3. Reference in configuration

        // Arrange - Simulate a third-party handler
        var services = new ServiceCollection();

        // Step 1 & 2: Implement and register (no changes to existing code)
        services.AddKeyedSingleton<IToolInvocationHandler, ThirdPartyHandler>("ThirdPartyFormat");

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var handler = serviceProvider.GetKeyedService<IToolInvocationHandler>("ThirdPartyFormat");

        // Assert
        Assert.NotNull(handler);
        Assert.IsType<ThirdPartyHandler>(handler);

        // Step 3 would be: Set ToolInvocationStrategy = "ThirdPartyFormat" in config
    }

    // Test Handler Implementations (Stubs)

    private class FunctoolsHandlerStub : IToolInvocationHandler
    {
        public IChatClient CreateHandler(IChatClient innerClient)
        {
            return new StubChatClient("Functools");
        }
    }

    private class CustomReActHandler : IToolInvocationHandler
    {
        public IChatClient CreateHandler(IChatClient innerClient)
        {
            return new StubChatClient("ReActJSON");
        }
    }

    private class CustomXMLHandler : IToolInvocationHandler
    {
        public IChatClient CreateHandler(IChatClient innerClient)
        {
            return new StubChatClient("ReActXML");
        }
    }

    private class ThirdPartyHandler : IToolInvocationHandler
    {
        public IChatClient CreateHandler(IChatClient innerClient)
        {
            return new StubChatClient("ThirdParty");
        }
    }

    private class StubChatClient : IChatClient
    {
        private readonly string _handlerType;

        public StubChatClient(string handlerType)
        {
            _handlerType = handlerType;
        }

        public ChatClientMetadata Metadata => new(_handlerType);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, $"{_handlerType} response"));
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate { Contents = [new TextContent($"{_handlerType}")] };
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose() { }
    }
}
