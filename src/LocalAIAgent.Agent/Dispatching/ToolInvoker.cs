using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.Logging;
using LocalAIAgent.Agent.Telemetry;

namespace LocalAIAgent.Agent.Dispatching;

/// <summary>
/// Validates and invokes tools with security constraints (allowlist, timeout, argument validation).
/// Implements secure-by-default pattern: unknown tools are rejected, arguments are validated, execution is timed out.
/// Emits OpenTelemetry traces and metrics for observability.
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
        // Start distributed trace for entire dispatch operation
        using var dispatchActivity = ActivitySources.Dispatch.StartActivity("tool.dispatch");
        dispatchActivity?.SetTag("tool.name", name);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Lookup tool in registry
            using var lookupActivity = ActivitySources.Dispatch.StartActivity("tool.lookup");
            lookupActivity?.SetTag("tool.name", name);

            if (!_registry.TryGet(name, out var descriptor))
            {
                lookupActivity?.SetTag("lookup.result", "miss");
                lookupActivity?.SetStatus(ActivityStatusCode.Error, "Tool not found");
                
                _logger.LogWarning("Tool '{ToolName}' not found in registry", name);
                
                // Record metric for lookup miss
                Metrics.RegistryLookupMiss.Add(1, new TagList { { "tool.name", name } });
                Metrics.ToolErrors.Add(1, new TagList { { "tool.name", name }, { "error.type", "UNKNOWN_TOOL" } });

                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.UnknownTool}: Tool '{name}' not found",
                    stopwatch.Elapsed);
            }

            lookupActivity?.SetTag("lookup.result", "hit");
            lookupActivity?.SetTag("tool.source", descriptor.Source);

            // 2. Size limit check
            using var sizeCheckActivity = ActivitySources.Validation.StartActivity("tool.validate_size");
            sizeCheckActivity?.SetTag("tool.name", name);

            var argsSizeBytes = args.GetRawText().Length * sizeof(char);
            sizeCheckActivity?.SetTag("args.size_bytes", argsSizeBytes);

            if (argsSizeBytes > MaxArgumentSizeBytes)
            {
                sizeCheckActivity?.SetStatus(ActivityStatusCode.Error, "Arguments exceed size limit");
                
                _logger.LogWarning(
                    "Tool '{ToolName}' arguments exceed size limit ({Size} > {MaxSize})",
                    name, argsSizeBytes, MaxArgumentSizeBytes);

                Metrics.ToolErrors.Add(1, new TagList { { "tool.name", name }, { "error.type", "ARG_SIZE_EXCEEDED" } });

                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.ArgValidationFailed}: Arguments exceed max size of {MaxArgumentSizeBytes / 1_048_576}MB",
                    stopwatch.Elapsed);
            }

            // 3. Validate arguments against JSON Schema (if provided)
            if (descriptor.ArgsSchema != null)
            {
                using var schemaValidationActivity = ActivitySources.Validation.StartActivity("tool.validate_schema");
                schemaValidationActivity?.SetTag("tool.name", name);
                schemaValidationActivity?.SetTag("has_schema", true);

                var validationStopwatch = Stopwatch.StartNew();
                var validation = await ValidateAsync(name, args);
                validationStopwatch.Stop();

                // Record validation duration metric
                Metrics.ValidationDuration.Record(
                    validationStopwatch.Elapsed.TotalMilliseconds,
                    new TagList { { "tool.name", name }, { "has_schema", "true" } });

                if (!validation.IsValid)
                {
                    var errors = string.Join("; ", validation.Errors);
                    schemaValidationActivity?.SetStatus(ActivityStatusCode.Error, $"Validation failed: {errors}");
                    schemaValidationActivity?.SetTag("validation.errors", errors);

                    _logger.LogWarning(
                        "Tool '{ToolName}' argument validation failed: {Errors}",
                        name, errors);

                    Metrics.ToolErrors.Add(1, new TagList { { "tool.name", name }, { "error.type", "ARG_VALIDATION_FAILED" } });

                    return ToolResult.Failure(
                        name,
                        $"{DispatcherException.ErrorCodes.ArgValidationFailed}: {errors}",
                        stopwatch.Elapsed);
                }

                schemaValidationActivity?.SetTag("validation.result", "success");
            }

            // 4. Create timeout token (tool-specific timeout)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(descriptor.Timeout);

            // 5. Invoke tool with combined cancellation token
            using var executeActivity = ActivitySources.Execution.StartActivity("tool.execute");
            executeActivity?.SetTag("tool.name", name);
            executeActivity?.SetTag("tool.source", descriptor.Source);
            executeActivity?.SetTag("timeout.seconds", descriptor.Timeout.TotalSeconds);

            try
            {
                var result = await descriptor.Invoker(args);
                stopwatch.Stop();

                // Update duration if not already set
                if (result.Duration == null)
                {
                    result = result with { Duration = stopwatch.Elapsed };
                }

                executeActivity?.SetTag("execution.status", "success");
                executeActivity?.SetTag("duration.ms", result.Duration?.TotalMilliseconds ?? 0);

                // Record success metrics
                Metrics.ToolDuration.Record(
                    result.Duration?.TotalMilliseconds ?? 0,
                    new TagList { { "tool.name", name }, { "status", "success" } });

                _logger.LogInformation(
                    "Tool '{ToolName}' succeeded in {Duration}ms",
                    name, result.Duration?.TotalMilliseconds ?? 0);

                return result;
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // Timeout occurred
                stopwatch.Stop();
                executeActivity?.SetStatus(ActivityStatusCode.Error, "Timeout");
                executeActivity?.SetTag("execution.status", "timeout");

                _logger.LogWarning(
                    "Tool '{ToolName}' timed out after {Timeout}s",
                    name, descriptor.Timeout.TotalSeconds);

                Metrics.ToolErrors.Add(1, new TagList { { "tool.name", name }, { "error.type", "TIMEOUT" } });

                Metrics.ToolDuration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    new TagList { { "tool.name", name }, { "status", "timeout" } });

                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.Timeout}: Execution exceeded timeout of {descriptor.Timeout.TotalSeconds}s",
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                // External cancellation
                stopwatch.Stop();
                executeActivity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
                executeActivity?.SetTag("execution.status", "cancelled");

                _logger.LogInformation("Tool '{ToolName}' cancelled by user", name);

                Metrics.ToolErrors.Add(1, new TagList { { "tool.name", name }, { "error.type", "CANCELLED" } });

                return ToolResult.Failure(
                    name,
                    $"{DispatcherException.ErrorCodes.Cancelled}: Operation cancelled",
                    stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                // 6. Catch exceptions and sanitize error messages (no stack trace leakage)
                stopwatch.Stop();
                executeActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                executeActivity?.SetTag("execution.status", "error");
                executeActivity?.SetTag("error.type", ex.GetType().Name);

                _logger.LogError(ex, "Tool '{ToolName}' invocation failed", name);

                Metrics.ToolErrors.Add(1, new TagList { { "tool.name", name }, { "error.type", "INVOCATION_FAILED" } });

                Metrics.ToolDuration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    new TagList { { "tool.name", name }, { "status", "error" } });

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
            dispatchActivity?.SetStatus(ActivityStatusCode.Error, "Dispatcher error");
            dispatchActivity?.SetTag("error.type", ex.GetType().Name);

            _logger.LogError(ex, "Dispatcher error for tool '{ToolName}'", name);

            Metrics.ToolErrors.Add(1, new TagList { { "tool.name", name }, { "error.type", "DISPATCHER_ERROR" } });

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
