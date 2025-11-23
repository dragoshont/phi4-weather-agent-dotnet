using Microsoft.Extensions.AI;

namespace LocalAIAgent.Agent.Adapters;

/// <summary>
/// Builds ChatOptions with tools populated for Foundry native function calling.
/// </summary>
/// <remarks>
/// This builder queries the ToolRegistry, converts all registered tools to AIFunction
/// format via IAIFunctionAdapter, and populates ChatOptions.Tools for each request.
/// This enables Foundry to inject tool definitions into Phi-4's system prompt template
/// using the native {Tool} placeholder.
/// </remarks>
public interface IChatOptionsBuilder
{
    /// <summary>
    /// Builds ChatOptions with Tools list populated from current ToolRegistry state.
    /// </summary>
    /// <returns>ChatOptions with all registered tools converted to AIFunction format</returns>
    /// <remarks>
    /// This method should be called before each IChatClient.GetStreamingResponseAsync()
    /// invocation to ensure Foundry has the latest tool definitions. If ToolRegistry
    /// is empty, should log a warning and return ChatOptions with empty Tools list.
    /// </remarks>
    ChatOptions BuildWithTools();
}
