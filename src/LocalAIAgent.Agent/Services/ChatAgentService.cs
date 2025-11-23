using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using LocalAIAgent.Agent.Models;

namespace LocalAIAgent.Agent.Services;

/// <summary>
/// Service for managing ChatClientAgent with Agent Framework.
/// Replaces direct IChatClient usage with agent abstractions and AgentThread state management.
/// </summary>
public class ChatAgentService
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger<ChatAgentService> _logger;

    public ChatAgentService(
        ChatClientAgent agent,
        ILogger<ChatAgentService> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new AgentThread for conversation state management.
    /// </summary>
    /// <returns>New AgentThread instance for this conversation</returns>
    public AgentThread CreateThread()
    {
        _logger.LogInformation("[ChatAgentService] CreateThread() called");

        try
        {
            _logger.LogInformation("[ChatAgentService] Calling _agent.GetNewThread()");
            var thread = _agent.GetNewThread();
            _logger.LogInformation($"[ChatAgentService] Thread created successfully: {(thread != null ? "NOT NULL" : "NULL")}");
            return thread;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChatAgentService] EXCEPTION in CreateThread()");
            throw;
        }
    }

    /// <summary>
    /// Runs the agent with a user message on the specified thread.
    /// </summary>
    /// <param name="userMessage">User's input message</param>
    /// <param name="thread">AgentThread managing conversation state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent's response text</returns>
    public async Task<string> RunAsync(
        string userMessage,
        AgentThread thread,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Running agent with message: {Message}", userMessage);

        // Create ChatMessage from string input
        var message = new ChatMessage(ChatRole.User, userMessage);

        // Run agent and get response
        var response = await _agent.RunAsync(message, thread, options: null, cancellationToken);

        _logger.LogDebug("Agent run completed");

        // Extract text from response
        return response?.Text ?? string.Empty;
    }

    /// <summary>
    /// Runs the agent with streaming response on the specified thread.
    /// </summary>
    /// <param name="userMessage">User's input message</param>
    /// <param name="thread">AgentThread managing conversation state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>IAsyncEnumerable of AgentRunResponseUpdate chunks</returns>
    public async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        string userMessage,
        AgentThread thread,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting streaming agent run with message: {Message}", userMessage);

        // Create ChatMessage from string input
        var message = new ChatMessage(ChatRole.User, userMessage);

        // Run agent with streaming - RunStreamingAsync overload accepts single ChatMessage
        await foreach (var update in _agent.RunStreamingAsync(message, thread, options: null, cancellationToken))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Resets the agent thread, clearing conversation history.
    /// </summary>
    /// <param name="thread">AgentThread to reset</param>
    public void ResetThread(AgentThread thread)
    {
        _logger.LogInformation("Resetting AgentThread - caller should create new thread");
        // Note: AgentThread history is immutable, so to reset, create a new thread
    }
}
