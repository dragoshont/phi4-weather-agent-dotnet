using System.Diagnostics.Metrics;

namespace Phi4WeatherAgent.Agent.Telemetry;

/// <summary>
/// Centralized metrics definitions for OpenTelemetry monitoring.
/// Provides histograms and counters for tool invocation performance and errors.
/// </summary>
public static class Metrics
{
    private const string MeterName = "Phi4WeatherAgent.Agent";
    private static readonly Meter _meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Histogram of tool execution durations (milliseconds).
    /// Dimensions: tool_name, status (success/failure)
    /// </summary>
    public static readonly Histogram<double> ToolDuration = _meter.CreateHistogram<double>(
        name: "tool.duration",
        unit: "ms",
        description: "Duration of tool execution in milliseconds");

    /// <summary>
    /// Counter of tool execution errors by error type.
    /// Dimensions: tool_name, error_type (UNKNOWN_TOOL, TIMEOUT, ARG_VALIDATION_FAILED, etc.)
    /// </summary>
    public static readonly Counter<long> ToolErrors = _meter.CreateCounter<long>(
        name: "tool.errors",
        unit: "{error}",
        description: "Count of tool execution errors by type");

    /// <summary>
    /// Counter of registry lookup misses (unknown tool attempts).
    /// Dimensions: tool_name
    /// </summary>
    public static readonly Counter<long> RegistryLookupMiss = _meter.CreateCounter<long>(
        name: "registry.lookup.miss",
        unit: "{lookup}",
        description: "Count of registry lookup misses for unknown tools");

    /// <summary>
    /// Histogram of functools parsing durations (milliseconds).
    /// Dimensions: status (success/failure), block_size_kb
    /// </summary>
    public static readonly Histogram<double> ParsingDuration = _meter.CreateHistogram<double>(
        name: "functools.parsing.duration",
        unit: "ms",
        description: "Duration of functools parsing in milliseconds");

    /// <summary>
    /// Histogram of argument validation durations (milliseconds).
    /// Dimensions: tool_name, has_schema (true/false)
    /// </summary>
    public static readonly Histogram<double> ValidationDuration = _meter.CreateHistogram<double>(
        name: "tool.validation.duration",
        unit: "ms",
        description: "Duration of tool argument validation in milliseconds");
}
