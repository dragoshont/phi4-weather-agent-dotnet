using Microsoft.Extensions.AI;
using LocalAIAgent.Agent.Interfaces;
using LocalAIAgent.Agent.Integration;
using LocalAIAgent.Agent.Parsing;
using LocalAIAgent.Agent.Dispatching;

namespace LocalAIAgent.Agent.Handlers;

/// <summary>
/// Tool invocation handler for models using functools format.
/// Wraps the existing FunctoolsChatClient to implement the IToolInvocationHandler interface.
/// </summary>
public sealed class FunctoolsHandler : IToolInvocationHandler
{
    private readonly IFunctoolsParser _parser;
    private readonly IToolInvoker _invoker;
    private readonly ILogger<FunctoolsChatClient> _logger;

    public FunctoolsHandler(
        IFunctoolsParser parser,
        IToolInvoker invoker,
        ILogger<FunctoolsChatClient> logger)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a FunctoolsChatClient wrapper around the inner client.
    /// </summary>
    public IChatClient CreateHandler(IChatClient innerClient)
    {
        ArgumentNullException.ThrowIfNull(innerClient);
        return new FunctoolsChatClient(innerClient, _parser, _invoker, _logger);
    }
}
