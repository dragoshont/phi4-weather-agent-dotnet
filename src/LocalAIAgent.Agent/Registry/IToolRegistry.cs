namespace LocalAIAgent.Agent.Registry;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

/// <summary>
/// Thread-safe registry for discovered and registered tools.
/// Provides fast lookup (target: <1μs per operation) using case-insensitive tool names.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Attempts to retrieve a tool descriptor by name (case-insensitive).
    /// </summary>
    /// <param name="name">Tool name to lookup (e.g., "GetWeather", "getweather")</param>
    /// <param name="descriptor">
    /// Output parameter: ToolDescriptor if found, null otherwise.
    /// </param>
    /// <returns>
    /// True if tool exists in registry, false otherwise.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Performance: <1μs per lookup (NFR-003 requirement).
    /// Implemented using ConcurrentDictionary with case-insensitive comparer.
    /// </para>
    /// <para>
    /// Thread-safe: Multiple concurrent calls supported without locking.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// if (registry.TryGet("GetWeather", out var descriptor))
    /// {
    ///     Console.WriteLine($"Found: {descriptor.Name} from {descriptor.Source}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("Tool not found");
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    bool TryGet(string name, [NotNullWhen(true)] out ToolDescriptor? descriptor);
    
    /// <summary>
    /// Registers a new tool in the registry.
    /// </summary>
    /// <param name="descriptor">ToolDescriptor to register (Name must be unique)</param>
    /// <exception cref="ArgumentNullException">Thrown if descriptor is null</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a tool with the same name already exists (case-insensitive).
    /// </exception>
    /// <remarks>
    /// <para>
    /// Tools are typically registered at app startup via:
    /// 1. Attribute discovery (scanning for [Tool] attributes)
    /// 2. MCP server discovery (calling ListTools HTTP endpoint)
    /// 3. Manual registration (via DI configuration)
    /// </para>
    /// <para>
    /// Thread-safe: Multiple concurrent Register() calls supported.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var descriptor = new ToolDescriptor
    /// {
    ///     Name = "GetWeather",
    ///     Source = "Local:WeatherTools",
    ///     Invoker = async (args) => { /* ... */ }
    /// };
    /// registry.Register(descriptor);
    /// </code>
    /// </para>
    /// </remarks>
    void Register(ToolDescriptor descriptor);
    
    /// <summary>
    /// Removes a tool from the registry by name (case-insensitive).
    /// </summary>
    /// <param name="name">Tool name to unregister</param>
    /// <returns>True if tool was found and removed, false if not found</returns>
    /// <remarks>
    /// <para>
    /// Used for dynamic tool removal (e.g., MCP server goes offline).
    /// Typically not needed during normal operation.
    /// </para>
    /// <para>
    /// Thread-safe: Concurrent calls supported.
    /// </para>
    /// </remarks>
    bool Unregister(string name);
    
    /// <summary>
    /// Lists all registered tools asynchronously.
    /// </summary>
    /// <param name="ct">Cancellation token (optional)</param>
    /// <returns>
    /// Async enumerable of all ToolDescriptor entries in the registry.
    /// Order is not guaranteed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Used for:
    /// 1. Debugging (list all available tools)
    /// 2. Telemetry (count registered tools)
    /// 3. API endpoints (expose tool catalog)
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// await foreach (var descriptor in registry.ListAsync())
    /// {
    ///     Console.WriteLine($"{descriptor.Name} ({descriptor.Source})");
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    IAsyncEnumerable<ToolDescriptor> ListAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Gets the total count of registered tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Performance: O(1) operation (backed by dictionary count).
    /// </para>
    /// <para>
    /// Used for telemetry metrics (e.g., "registry.tool.count" gauge).
    /// </para>
    /// </remarks>
    int Count { get; }
}
