using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using LocalAIAgent.Agent.Registry;

namespace LocalAIAgent.Agent.Adapters;

/// <summary>
/// Adapter for converting ToolDescriptor (MCP/ToolRegistry format) to Microsoft.Extensions.AI.AIFunction format
/// required by Foundry's native function calling template.
/// 
/// This adapter converts tool metadata only (name, description, parameters schema).
/// It does NOT include execution logic - the ToolInvoker continues to handle actual tool execution.
/// 
/// Design: Metadata-only AIFunction creation for Foundry template population
/// </summary>
public sealed class AIFunctionAdapter : IAIFunctionAdapter
{
    private readonly ILogger<AIFunctionAdapter> _logger;

    public AIFunctionAdapter(ILogger<AIFunctionAdapter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Converts a ToolDescriptor to an AIFunctionDeclaration for use in ChatOptions.Tools.
    /// 
    /// The returned AIFunctionDeclaration contains metadata only (name, description, parameters JsonSchema).
    /// No execution delegate is included since ToolInvoker handles tool execution separately.
    /// 
    /// This enables Foundry's native function calling template to populate the {Tool} placeholder
    /// with properly formatted OpenAI-compatible function definitions.
    /// </summary>
    /// <param name="descriptor">The tool descriptor from ToolRegistry or MCP server discovery</param>
    /// <returns>AIFunctionDeclaration instance with metadata populated from the descriptor</returns>
    public AIFunctionDeclaration ConvertToAIFunction(ToolDescriptor descriptor)
    {
        try
        {
            _logger.LogDebug(
                "[FOUNDRY] Converting ToolDescriptor to AIFunction: Name={ToolName}, ArgsSchema={HasSchema}",
                descriptor.Name,
                descriptor.ArgsSchema != null);

            // Extract name and description
            var name = descriptor.Name;
            var description = ExtractDescription(descriptor.ArgsSchema) ?? $"Tool: {name}";

            // Convert JsonSchema to JsonElement for AIFunctionFactory.CreateDeclaration
            var jsonSchemaElement = ConvertJsonSchemaToElement(descriptor.ArgsSchema);

            // Create AIFunctionDeclaration using CreateDeclaration (metadata-only, no invocation delegate)
            // This is perfect for our use case: Foundry template needs metadata only, ToolInvoker handles execution
            var aiFunction = AIFunctionFactory.CreateDeclaration(
                name: name,
                description: description,
                jsonSchema: jsonSchemaElement,
                returnJsonSchema: null); // Return schema not needed for this use case

            _logger.LogDebug(
                "[FOUNDRY] Successfully converted {ToolName} to AIFunctionDeclaration",
                name);

            return aiFunction;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[FOUNDRY] Failed to convert ToolDescriptor {ToolName} to AIFunction",
                descriptor.Name);
            throw;
        }
    }

    /// <summary>
    /// Extracts description from ArgsSchema if available.
    /// Looks for root-level "description" property in the JSON Schema.
    /// </summary>
    private string? ExtractDescription(JsonSchema? argsSchema)
    {
        if (argsSchema == null) return null;

        try
        {
            // Serialize JsonSchema to JSON to extract description
            var json = JsonSerializer.Serialize(argsSchema);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("description", out var descProp))
            {
                return descProp.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[FOUNDRY] Failed to extract description from ArgsSchema");
        }

        return null;
    }

    /// <summary>
    /// Converts JsonSchema object to JsonElement for use with AIFunctionFactory.CreateDeclaration.
    /// 
    /// AIFunctionFactory.CreateDeclaration expects JsonElement for the jsonSchema parameter.
    /// We serialize our JsonSchema object and parse it back as JsonElement.
    /// </summary>
    private JsonElement ConvertJsonSchemaToElement(JsonSchema? argsSchema)
    {
        if (argsSchema == null)
        {
            // No schema provided - return empty object schema
            return JsonDocument.Parse("{}").RootElement;
        }

        try
        {
            // Serialize JsonSchema to JSON string
            var json = JsonSerializer.Serialize(argsSchema);

            // Parse back as JsonElement
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone(); // Clone to detach from document lifetime
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FOUNDRY] Failed to convert JsonSchema to JsonElement");
            return JsonDocument.Parse("{}").RootElement;
        }
    }
}
