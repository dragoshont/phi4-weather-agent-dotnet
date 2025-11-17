namespace Phi4WeatherAgent.Tools;

/// <summary>
/// Decorator attribute for methods that should be registered as invocable tools.
/// Used by ToolDiscoveryService to find and register local C# methods.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolAttribute : Attribute
{
    public string Name { get; }
    public string? Description { get; set; }
    public string? InputSchemaJson { get; set; }
    public SecurityClass SecurityClass { get; set; } = SecurityClass.Public;
    public int TimeoutSeconds { get; set; } = 30;

    public ToolAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tool name cannot be null or whitespace", nameof(name));

        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"Tool name '{name}' must match pattern ^[a-zA-Z][a-zA-Z0-9_]*$", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Tool name exceeds max length of 100 characters", nameof(name));

        Name = name;
    }
}

/// <summary>
/// Classification for tool allowlist filtering.
/// </summary>
public enum SecurityClass
{
    Public = 0,
    Internal = 1,
    Admin = 2
}
