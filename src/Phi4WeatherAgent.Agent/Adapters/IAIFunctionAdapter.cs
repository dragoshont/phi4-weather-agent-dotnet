using Microsoft.Extensions.AI;
using Phi4WeatherAgent.Agent.Registry;

namespace Phi4WeatherAgent.Agent.Adapters;

/// <summary>
/// Converts ToolMetadata from ToolRegistry to Microsoft.Extensions.AI AIFunction format
/// for Foundry's native function calling via ChatOptions.Tools.
/// </summary>
/// <remarks>
/// This adapter bridges the gap between our internal ToolRegistry format and
/// Foundry's expected AIFunction format with JSON Schema. It enables zero-code
/// extensibility by converting discovered tools ([Tool] attributes, MCP servers)
/// into the format Foundry expects for native template injection.
/// </remarks>
public interface IAIFunctionAdapter
{
    /// <summary>
    /// Converts a ToolDescriptor to an AIFunctionDeclaration with JSON Schema.
    /// </summary>
    /// <param name="descriptor">Tool descriptor from ToolRegistry containing metadata</param>
    /// <returns>AIFunctionDeclaration with name, description, and parameter schema for Foundry injection</returns>
    /// <remarks>
    /// The returned AIFunctionDeclaration is metadata-only (no execution delegate) since our custom
    /// ToolInvoker handles execution after parsing functools responses. Foundry only
    /// needs the schema for prompt template injection via ChatOptions.Tools.
    /// </remarks>
    AIFunctionDeclaration ConvertToAIFunction(ToolDescriptor descriptor);
}
