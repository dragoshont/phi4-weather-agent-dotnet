namespace Phi4WeatherAgent.Agent.Dispatching;

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Validates and invokes tools with security constraints (allowlist, timeout, argument validation).
/// Implements secure-by-default pattern: unknown tools are rejected, arguments are validated, execution is timed out.
/// </summary>
public interface IToolInvoker
{
    /// <summary>
    /// Invokes a tool by name with provided arguments.
    /// </summary>
    /// <param name="name">
    /// Tool name (must exist in registry, case-insensitive).
    /// </param>
    /// <param name="args">
    /// JSON arguments from model (JsonElement for zero-copy deserialization).
    /// Will be validated against tool's JSON Schema if provided.
    /// </param>
    /// <param name="ct">
    /// Cancellation token (respects tool-specific timeout from ToolDescriptor).
    /// </param>
    /// <returns>
    /// ToolResult with either:
    /// - Content (success): Serialized result from tool execution
    /// - Error (failure): Error code and message (UNKNOWN_TOOL, ARG_VALIDATION_FAILED, INVOCATION_FAILED, TIMEOUT, CANCELLED)
    /// </returns>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// 1. Lookup tool in registry (TryGet)
    /// 2. Check allowlist (security constraint: reject unknown tools)
    /// 3. Validate arguments against JSON Schema (if schema provided)
    /// 4. Create timeout token (tool-specific timeout from descriptor)
    /// 5. Invoke tool with combined cancellation token
    /// 6. Catch exceptions and sanitize error messages (no stack trace leakage)
    /// 7. Record telemetry (duration, success/failure, error type)
    /// </para>
    /// <para>
    /// Performance: <5ms validation overhead (NFR-002 requirement).
    /// </para>
    /// <para>
    /// Security constraints enforced:
    /// - Unknown tool rejection (explicit allowlist)
    /// - Argument validation (JSON Schema)
    /// - Size limit (max 10MB per argument)
    /// - Timeout enforcement (default 30s, configurable per tool)
    /// - Rate limiting (max 10 calls per turn, tracked externally)
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var args = JsonSerializer.Deserialize&lt;JsonElement&gt;("{\"location\": \"Seattle\"}");
    /// var result = await invoker.InvokeAsync("GetWeather", args, ct);
    /// 
    /// if (result.Error != null)
    /// {
    ///     logger.LogError("Tool failed: {Error}", result.Error);
    /// }
    /// else
    /// {
    ///     logger.LogInformation("Tool succeeded: {Content}", result.Content);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    Task<ToolResult> InvokeAsync(string name, JsonElement args, CancellationToken ct);
    
    /// <summary>
    /// Validates tool arguments against JSON Schema without invoking the tool.
    /// Useful for pre-validation or testing.
    /// </summary>
    /// <param name="name">Tool name (must exist in registry)</param>
    /// <param name="args">JSON arguments to validate</param>
    /// <returns>
    /// Validation result:
    /// - IsValid: true if arguments pass validation
    /// - Errors: List of validation errors (empty if valid)
    /// </returns>
    /// <remarks>
    /// <para>
    /// If tool has no JSON Schema, validation always passes (opt-in validation).
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var validation = await invoker.ValidateAsync("GetWeather", args);
    /// if (!validation.IsValid)
    /// {
    ///     foreach (var error in validation.Errors)
    /// {
    ///         Console.WriteLine($"Validation error: {error}");
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    Task<ValidationResult> ValidateAsync(string name, JsonElement args);
}

/// <summary>
/// Result of argument validation (from ValidateAsync method).
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// True if arguments pass validation, false otherwise.
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// List of validation errors (empty if IsValid = true).
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
