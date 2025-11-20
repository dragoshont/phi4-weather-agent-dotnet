extern alias web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Phi4WeatherAgent.Agent.Integration;
using Xunit;

namespace Phi4WeatherAgent.Web.Tests.Integration;

/// <summary>
/// Tests to verify the IChatClient DI registration is correctly configured
/// with FunctoolsChatClient decorator.
/// </summary>
public class ChatClientDITests : IClassFixture<WebApplicationFactory<web::Program>>
{
    private readonly WebApplicationFactory<web::Program> _factory;

    public ChatClientDITests(WebApplicationFactory<web::Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void IChatClient_ShouldBeRegistered()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Act
        var chatClient = services.GetService<IChatClient>();

        // Assert
        Assert.NotNull(chatClient);
    }

    [Fact]
    public void IChatClient_ShouldBeFunctoolsChatClient()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Act
        var chatClient = services.GetRequiredService<IChatClient>();

        // Assert - CRITICAL: Verify it's wrapped with FunctoolsChatClient
        Assert.IsType<FunctoolsChatClient>(chatClient);
    }

    [Fact]
    public async Task ChatClient_ShouldStreamResponse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var chatClient = services.GetRequiredService<IChatClient>();

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, "You are a test assistant."),
            new ChatMessage(ChatRole.User, "Say 'test response'")
        };

        // Act
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        // Assert
        Assert.NotEmpty(updates);
        var fullText = string.Concat(updates.Select(u => u.Text));
        Assert.NotEmpty(fullText);
    }
}
