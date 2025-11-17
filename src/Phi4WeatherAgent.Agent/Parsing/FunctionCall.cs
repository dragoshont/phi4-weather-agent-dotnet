using System.Text.Json;

namespace Phi4WeatherAgent.Agent.Parsing;

/// <summary>
/// Represents a parsed tool invocation from Phi-4-mini's functools block.
/// Immutable value object for thread safety.
/// </summary>
public sealed record FunctionCall
{
    /// <summary>
    /// Tool name exactly as the model outputs it.
    /// Validation: ^[a-zA-Z][a-zA-Z0-9_]*$, max 100 chars.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Unvalidated JSON arguments from model.
    /// May be empty object {}, never null.
    /// </summary>
    public required JsonElement Arguments { get; init; }

    public FunctionCall()
    {
        // Required for record initialization
    }

    /// <summary>
    /// Validates the function call conforms to data model constraints.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Name cannot be null or whitespace", nameof(Name));

        if (Name.Length > 100)
            throw new ArgumentException("Name exceeds max length of 100 characters", nameof(Name));

        if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"Name '{Name}' must match pattern ^[a-zA-Z][a-zA-Z0-9_]*$", nameof(Name));

        if (Arguments.ValueKind == JsonValueKind.Null || Arguments.ValueKind == JsonValueKind.Undefined)
            throw new ArgumentException("Arguments cannot be null or undefined", nameof(Arguments));
    }
}
