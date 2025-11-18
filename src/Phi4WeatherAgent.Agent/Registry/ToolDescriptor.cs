using System.Text.Json;
using Json.Schema;
using Phi4WeatherAgent.Tools;

namespace Phi4WeatherAgent.Agent.Registry;

/// <summary>
/// Registry entry for a discovered/registered tool (local C# method or MCP-discovered).
/// Immutable after registration for thread safety.
/// </summary>
public sealed record ToolDescriptor
{
    /// <summary>
    /// Canonical tool name (case-insensitive lookup).
    /// Unique in registry, matches ^[a-zA-Z][a-zA-Z0-9_]*$.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Origin of tool.
    /// Format: "Local:{TypeName}" or "MCP:{ServerUrl}" or "Generated".
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Optional JSON Schema for argument validation.
    /// Valid JSON Schema Draft 2020-12 (from JsonSchema.Net).
    /// </summary>
    public JsonSchema? ArgsSchema { get; init; }

    /// <summary>
    /// Async function to execute tool.
    /// Non-null delegate, may throw exceptions (caught by dispatcher).
    /// </summary>
    public required Func<JsonElement, ValueTask<Phi4WeatherAgent.Agent.Dispatching.ToolResult>> Invoker { get; init; }

    /// <summary>
    /// Classification for allowlist filtering.
    /// </summary>
    public SecurityClass SecurityClass { get; init; } = SecurityClass.Public;

    /// <summary>
    /// Per-tool timeout override. Must be >0, defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public ToolDescriptor()
    {
        // Required for record initialization
    }

    /// <summary>
    /// Validates the descriptor conforms to data model constraints.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Name cannot be null or whitespace", nameof(Name));

        if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"Name '{Name}' must match pattern ^[a-zA-Z][a-zA-Z0-9_]*$", nameof(Name));

        if (string.IsNullOrWhiteSpace(Source))
            throw new ArgumentException("Source cannot be null or whitespace", nameof(Source));

        if (!Source.StartsWith("Local:") && !Source.StartsWith("MCP:") && Source != "Generated")
            throw new ArgumentException($"Source '{Source}' must match format 'Local:*', 'MCP:*', or 'Generated'", nameof(Source));

        if (Invoker == null)
            throw new ArgumentNullException(nameof(Invoker), "Invoker cannot be null");

        if (Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be greater than zero");
    }
}
