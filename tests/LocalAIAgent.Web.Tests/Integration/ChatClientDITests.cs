extern alias web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI;
using LocalAIAgent.Agent.Services;
using Xunit;

namespace LocalAIAgent.Web.Tests.Integration;

/// <summary>
/// Tests to verify Agent Framework services are correctly registered:
/// ChatClientAgent, ChatAgentService, and AgentThread lifecycle.
/// </summary>
public class ChatClientDITests : IClassFixture<WebApplicationFactory<web::Program>>
{
    private readonly WebApplicationFactory<web::Program> _factory;

    public ChatClientDITests(WebApplicationFactory<web::Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ChatClientAgent_ShouldBeRegistered()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Act
        var agent = services.GetService<ChatClientAgent>();

        // Assert
        Assert.NotNull(agent);
    }

    [Fact]
    public void ChatAgentService_ShouldBeRegistered()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Act
        var chatService = services.GetRequiredService<ChatAgentService>();

        // Assert
        Assert.NotNull(chatService);
        Assert.IsType<ChatAgentService>(chatService);
    }

    [Fact]
    public void ChatAgentService_ShouldCreateThread()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var chatService = services.GetRequiredService<ChatAgentService>();

        // Act
        var thread = chatService.CreateThread();

        // Assert
        Assert.NotNull(thread);
    }

    [Fact]
    public async Task ChatAgentService_ShouldHandleUserMessage()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var chatService = services.GetRequiredService<ChatAgentService>();
        var thread = chatService.CreateThread();

        // Act
        var response = await chatService.RunAsync("Say 'test response'", thread);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response);
    }
}
