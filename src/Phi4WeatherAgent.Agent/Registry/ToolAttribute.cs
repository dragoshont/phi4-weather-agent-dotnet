namespace Phi4WeatherAgent.Agent.Registry;

/// <summary>
/// Decorator attribute for methods that should be registered as invocable tools.
/// Used by ToolDiscoveryService to find and register local C# methods.
/// </summary>
/// <remarks>
/// <para>
/// Usage example:
/// <code>
/// [Tool("GetWeather", 
///       Description = "Gets current weather for a location",
///       InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"location\":{\"type\":\"string\"}},\"required\":[\"location\"]}")]
/// public static async Task&lt;string&gt; GetWeatherAsync(string location)
/// {
///     // Implementation
/// }
/// </code>
/// </para>
/// <para>
/// Method signature requirements:
/// - Must be public and static (or instance with DI-injectable parent class)
/// - Parameters must be deserializable from JsonElement (primitive types, POCOs)
/// - Return type must be string, Task&lt;string&gt;, or ValueTask&lt;string&gt;
/// - Can accept CancellationToken as last parameter (optional)
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolAttribute : Attribute
{
    /// <summary>
    /// Tool name (required). Must match ^[a-zA-Z][a-zA-Z0-9_]*$.
    /// This is the name the model will use in functools blocks.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Human-readable description of what the tool does (optional).
    /// Displayed in tool catalogs and developer documentation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// JSON Schema for input validation (optional).
    /// Must be valid JSON Schema Draft 2020-12.
    /// If null, no argument validation is performed (opt-in validation).
    /// </summary>
    /// <example>
    /// <code>
    /// InputSchemaJson = @"{
    ///     ""type"": ""object"",
    ///     ""properties"": {
    ///         ""location"": { ""type"": ""string"", ""maxLength"": 100 },
    ///         ""units"": { ""type"": ""string"", ""enum"": [""metric"", ""imperial""] }
    ///     },
    ///     ""required"": [""location""]
    /// }"
    /// </code>
    /// </example>
    public string? InputSchemaJson { get; set; }

    /// <summary>
    /// Security classification for allowlist enforcement (optional, defaults to Public).
    /// </summary>
    public SecurityClass SecurityClass { get; set; } = SecurityClass.Public;

    /// <summary>
    /// Timeout in seconds for this tool (optional, defaults to 30).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Creates a new ToolAttribute with the specified name.
    /// </summary>
    /// <param name="name">
    /// Tool name (required). Must match ^[a-zA-Z][a-zA-Z0-9_]*$.
    /// </param>
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
