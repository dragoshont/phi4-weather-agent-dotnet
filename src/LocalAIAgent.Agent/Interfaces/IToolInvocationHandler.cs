using Microsoft.Extensions.AI;

namespace LocalAIAgent.Agent.Interfaces;

/// <summary>
/// Interface for model-specific tool invocation handling.
/// Implementations parse custom tool formats (e.g., functools, ReAct JSON)
/// and execute tools via delegating chat client pattern.
/// </summary>
public interface IToolInvocationHandler
{
    /// <summary>
    /// Creates a chat client wrapper that handles model-specific tool invocation formats.
    /// </summary>
    /// <param name="innerClient">The underlying chat client to wrap</param>
    /// <returns>Wrapped chat client with handler logic applied</returns>
    IChatClient CreateHandler(IChatClient innerClient);
}
