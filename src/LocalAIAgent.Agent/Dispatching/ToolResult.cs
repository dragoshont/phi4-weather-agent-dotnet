namespace LocalAIAgent.Agent.Dispatching;

/// <summary>
/// Output of tool invocation returned to model (as `tool` role message).
/// Immutable for thread safety.
/// </summary>
public sealed record ToolResult
{
    /// <summary>
    /// Tool name (matches FunctionCall.Name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Successful result content (JSON, text, etc.).
    /// Null if Error is set. Exactly one of Content or Error must be non-null.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Error message if invocation failed.
    /// Error codes: MALFORMED_BLOCK, UNKNOWN_TOOL, ARG_VALIDATION_FAILED, 
    /// INVOCATION_FAILED, TIMEOUT, CANCELLED.
    /// Null if Content is set. Exactly one of Content or Error must be non-null.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Actual tool execution time (for telemetry).
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Additional metadata (truncation flags, retry counts, etc.).
    /// </summary>
    public IDictionary<string, string>? Meta { get; init; }

    public ToolResult()
    {
        // Required for record initialization
    }

    /// <summary>
    /// Validates XOR constraint: exactly one of Content or Error must be non-null.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Name cannot be null or whitespace", nameof(Name));

        bool hasContent = Content != null;
        bool hasError = Error != null;

        if (hasContent == hasError)
            throw new InvalidOperationException(
                "Exactly one of Content or Error must be non-null. " +
                $"Content: {(hasContent ? "set" : "null")}, Error: {(hasError ? "set" : "null")}");
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ToolResult Success(string name, string content, TimeSpan? duration = null, IDictionary<string, string>? meta = null)
    {
        return new ToolResult
        {
            Name = name,
            Content = content,
            Duration = duration,
            Meta = meta
        };
    }

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static ToolResult Failure(string name, string error, TimeSpan? duration = null, IDictionary<string, string>? meta = null)
    {
        return new ToolResult
        {
            Name = name,
            Error = error,
            Duration = duration,
            Meta = meta
        };
    }
}
