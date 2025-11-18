using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Phi4WeatherAgent.Agent.Registry;

/// <summary>
/// Thread-safe registry for discovered and registered tools.
/// Provides fast lookup (target: <1μs per operation) using case-insensitive tool names.
/// Implemented with ConcurrentDictionary for lock-free operations.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ToolDescriptor> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool TryGet(string name, [NotNullWhen(true)] out ToolDescriptor? descriptor)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            descriptor = null;
            return false;
        }

        return _tools.TryGetValue(name, out descriptor);
    }

    /// <inheritdoc />
    public void Register(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        // Validate descriptor before registration
        descriptor.Validate();

        // Try to add, throw if duplicate exists
        if (!_tools.TryAdd(descriptor.Name, descriptor))
        {
            throw new InvalidOperationException(
                $"Tool '{descriptor.Name}' is already registered (case-insensitive). " +
                "Unregister the existing tool before registering a new one.");
        }
    }

    /// <inheritdoc />
    public bool Unregister(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _tools.TryRemove(name, out _);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ToolDescriptor> ListAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var kvp in _tools)
        {
            ct.ThrowIfCancellationRequested();
            yield return kvp.Value;
            
            // Allow cooperative multitasking
            await Task.Yield();
        }
    }

    /// <summary>
    /// Gets all registered tool descriptors as a list (synchronous version for testing).
    /// </summary>
    /// <returns>List of all registered tool descriptors.</returns>
    public IReadOnlyList<ToolDescriptor> GetAll()
    {
        return _tools.Values.ToList();
    }

    /// <inheritdoc />
    public int Count => _tools.Count;
}
