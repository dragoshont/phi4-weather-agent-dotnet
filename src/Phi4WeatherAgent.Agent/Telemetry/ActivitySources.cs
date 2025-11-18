using System.Diagnostics;

namespace Phi4WeatherAgent.Agent.Telemetry;

/// <summary>
/// Centralized ActivitySource definitions for OpenTelemetry tracing.
/// Each source represents a logical subsystem (parsing, validation, dispatch, execution).
/// </summary>
public static class ActivitySources
{
    /// <summary>
    /// Activity source name for the entire agent subsystem.
    /// Used as parent for all tool-related operations.
    /// </summary>
    public const string AgentSourceName = "Phi4WeatherAgent.Agent";

    /// <summary>
    /// ActivitySource for functools parsing operations.
    /// Traces: functools.parse (per chunk), functools.block_complete (per block)
    /// </summary>
    public static readonly ActivitySource Parsing = new(AgentSourceName + ".Parsing", "1.0.0");

    /// <summary>
    /// ActivitySource for tool validation operations (schema, allowlist, rate limits).
    /// Traces: tool.validate_schema, tool.validate_allowlist, tool.validate_ratelimit
    /// </summary>
    public static readonly ActivitySource Validation = new(AgentSourceName + ".Validation", "1.0.0");

    /// <summary>
    /// ActivitySource for tool dispatch operations (registry lookup, invoker setup).
    /// Traces: tool.dispatch, tool.lookup
    /// </summary>
    public static readonly ActivitySource Dispatch = new(AgentSourceName + ".Dispatch", "1.0.0");

    /// <summary>
    /// ActivitySource for actual tool execution.
    /// Traces: tool.execute (per tool invocation)
    /// </summary>
    public static readonly ActivitySource Execution = new(AgentSourceName + ".Execution", "1.0.0");
}
