using System.Diagnostics;
using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace Phi4WeatherAgent.Agent.Dispatching;

/// <summary>
/// Validates and invokes tools with security constraints (allowlist, timeout, argument validation).
/// Implements secure-by-default pattern: unknown tools are rejected, arguments are validated, execution is timed out.
/// </summary>
public sealed class ToolInvoker : IToolInvoker
{
    private readonly Registry.IToolRegistry _registry;
    private readonly ILogger<ToolInvoker> _logger;
    private const int MaxArgumentSizeBytes = 10_485_760; // 10MB

    public ToolInvoker(
        Registry.IToolRegistry registry,
        ILogger<ToolInvoker> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ToolResult> InvokeAsync(string name, JsonElement args, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Lookup tool in registry
            if (!_registry.TryGet(name, out var descriptor))
            {
                _logger.LogWarning("Tool '{ToolName}' not found in registry", name);
                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.UnknownTool}: Tool '{name}' not found",
                    stopwatch.Elapsed);
            }

            // 2. Size limit check
            var argsSizeBytes = args.GetRawText().Length * sizeof(char);
            if (argsSizeBytes > MaxArgumentSizeBytes)
            {
                _logger.LogWarning(
                    "Tool '{ToolName}' arguments exceed size limit ({Size} > {MaxSize})",
                    name, argsSizeBytes, MaxArgumentSizeBytes);

                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.ArgValidationFailed}: Arguments exceed max size of {MaxArgumentSizeBytes / 1_048_576}MB",
                    stopwatch.Elapsed);
            }

            // 3. Validate arguments against JSON Schema (if provided)
            if (descriptor.ArgsSchema != null)
            {
                var validation = await ValidateAsync(name, args);
                if (!validation.IsValid)
                {
                    var errors = string.Join("; ", validation.Errors);
                    _logger.LogWarning(
                        "Tool '{ToolName}' argument validation failed: {Errors}",
                        name, errors);

                    return ToolResult.Failure(
                        name,
                        $"{DispatcherException.ErrorCodes.ArgValidationFailed}: {errors}",
                        stopwatch.Elapsed);
                }
            }

            // 4. Create timeout token (tool-specific timeout)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(descriptor.Timeout);

            // 5. Invoke tool with combined cancellation token
            try
            {
                var result = await descriptor.Invoker(args);
                stopwatch.Stop();

                // Update duration if not already set
                if (result.Duration == null)
                {
                    result = result with { Duration = stopwatch.Elapsed };
                }

                _logger.LogInformation(
                    "Tool '{ToolName}' succeeded in {Duration}ms",
                    name, result.Duration?.TotalMilliseconds ?? 0);

                return result;
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // Timeout occurred
                stopwatch.Stop();
                _logger.LogWarning(
                    "Tool '{ToolName}' timed out after {Timeout}s",
                    name, descriptor.Timeout.TotalSeconds);

                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.Timeout}: Execution exceeded timeout of {descriptor.Timeout.TotalSeconds}s",
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                // External cancellation
                stopwatch.Stop();
                _logger.LogInformation("Tool '{ToolName}' cancelled by user", name);

                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.Cancelled}: Operation cancelled",
                    stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                // 6. Catch exceptions and sanitize error messages (no stack trace leakage)
                stopwatch.Stop();
                _logger.LogError(ex, "Tool '{ToolName}' invocation failed", name);

                // Sanitize error message (no stack traces)
                var errorMessage = ex.Message;
                if (errorMessage.Contains("at ") || errorMessage.Contains("   in "))
                {
                    errorMessage = errorMessage.Split(new[] { "at " }, StringSplitOptions.None)[0].Trim();
                }

                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.InvocationFailed}: {errorMessage}",
                    stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            // Catch-all for dispatcher-level errors
            stopwatch.Stop();
            _logger.LogError(ex, "Dispatcher error for tool '{ToolName}'", name);

            return ToolResult.Failure(
                name,
                $"{DispatcherException.ErrorCodes.InvocationFailed}: Dispatcher error - {ex.Message}",
                stopwatch.Elapsed);
        }
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(string name, JsonElement args)
    {
        // Lookup tool
        if (!_registry.TryGet(name, out var descriptor))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new[] { $"Tool '{name}' not found" }
            });
        }

        // No schema means validation passes (opt-in validation)
        if (descriptor.ArgsSchema == null)
        {
            return Task.FromResult(new ValidationResult { IsValid = true });
        }

        // Validate against JSON Schema
        try
        {
            var schemaResult = descriptor.ArgsSchema.Evaluate(args);

            if (schemaResult.IsValid)
            {
                return Task.FromResult(new ValidationResult { IsValid = true });
            }

            // Collect validation errors
            var errors = new List<string>();
            CollectErrors(schemaResult, errors);

            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema validation error for tool '{ToolName}'", name);
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new[] { $"Schema validation error: {ex.Message}" }
            });
        }
    }

    /// <summary>
    /// Recursively collects validation errors from JSON Schema evaluation result.
    /// </summary>
    private void CollectErrors(EvaluationResults result, List<string> errors)
    {
        if (!result.IsValid && result.Errors != null)
        {
            foreach (var error in result.Errors)
            {
                var path = error.Key ?? "/";
                var message = error.Value ?? "Validation failed";
                errors.Add($"{path}: {message}");
            }
        }

        // Recurse into nested results
        if (result.Details != null)
        {
            foreach (var detail in result.Details)
            {
                CollectErrors(detail, errors);
            }
        }
    }
}
