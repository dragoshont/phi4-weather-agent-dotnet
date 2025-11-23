using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using LocalAIAgent.Agent.Registry;

namespace LocalAIAgent.Agent.Adapters;

/// <summary>
/// Builds ChatOptions with tools populated from ToolRegistry for Foundry native function calling.
/// </summary>
/// <remarks>
/// This builder queries the ToolRegistry, converts all registered tools to AIFunctionDeclaration
/// format via IAIFunctionAdapter, and populates ChatOptions.Tools for each request.
/// This enables Foundry to inject tool definitions into Phi-4's system prompt template
/// using the native {Tool} placeholder.
/// </remarks>
public sealed class ChatOptionsBuilder : IChatOptionsBuilder
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IAIFunctionAdapter _adapter;
    private readonly ILogger<ChatOptionsBuilder> _logger;

    public ChatOptionsBuilder(
        IToolRegistry toolRegistry,
        IAIFunctionAdapter adapter,
        ILogger<ChatOptionsBuilder> logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Builds ChatOptions with Tools list populated from current ToolRegistry state.
    /// </summary>
    /// <returns>ChatOptions with all registered tools converted to AIFunctionDeclaration format</returns>
    /// <remarks>
    /// This method should be called before each IChatClient.GetStreamingResponseAsync()
    /// invocation to ensure Foundry has the latest tool definitions. If ToolRegistry
    /// is empty, logs a warning and returns ChatOptions with empty Tools list.
    /// </remarks>
    public async Task<ChatOptions> BuildWithToolsAsync(CancellationToken ct = default)
    {
        try
        {
            // Query all registered tools from ToolRegistry
            var tools = new List<ToolDescriptor>();
            await foreach (var tool in _toolRegistry.ListAsync(ct))
            {
                tools.Add(tool);
            }

            // Check if registry is empty
            if (tools.Count == 0)
            {
                _logger.LogWarning(
                    "[FOUNDRY] No tools registered in ToolRegistry - ChatOptions.Tools will be empty. " +
                    "Verify ToolDiscoveryService has run successfully.");
                
                return new ChatOptions
                {
                    Tools = new List<AITool>()
                };
            }

            // Convert each ToolDescriptor to AIFunctionDeclaration via adapter
            var aiFunctions = new List<AITool>();
            
            foreach (var tool in tools)
            {
                try
                {
                    var aiFunction = _adapter.ConvertToAIFunction(tool);
                    aiFunctions.Add(aiFunction);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[FOUNDRY] Failed to convert tool {ToolName} to AIFunctionDeclaration - skipping tool",
                        tool.Name);
                    // Continue processing other tools instead of failing the entire build
                }
            }

            // Build and return ChatOptions with Tools populated
            var chatOptions = new ChatOptions
            {
                Tools = aiFunctions
            };

            _logger.LogInformation(
                "[FOUNDRY] Built ChatOptions with {ToolCount} tools: {ToolNames}",
                aiFunctions.Count,
                string.Join(", ", tools.Select(t => t.Name)));

            return chatOptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[FOUNDRY] Failed to build ChatOptions with tools - returning empty ChatOptions");
            
            // Return empty ChatOptions as fallback to avoid breaking chat functionality
            return new ChatOptions
            {
                Tools = new List<AITool>()
            };
        }
    }
}
