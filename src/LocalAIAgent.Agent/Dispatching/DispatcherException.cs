namespace LocalAIAgent.Agent.Dispatching;

/// <summary>
/// Thrown by dispatcher for validation/invocation errors.
/// </summary>
public class DispatcherException : Exception
{
    /// <summary>
    /// Error code identifying the dispatch failure.
    /// Valid values: UNKNOWN_TOOL, ARG_VALIDATION_FAILED, TIMEOUT.
    /// </summary>
    public string ErrorCode { get; }

    public DispatcherException(string errorCode, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("ErrorCode cannot be null or whitespace", nameof(errorCode));

        ErrorCode = errorCode;
    }

    public DispatcherException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("ErrorCode cannot be null or whitespace", nameof(errorCode));

        ErrorCode = errorCode;
    }

    /// <summary>
    /// Standard error codes for dispatcher failures.
    /// </summary>
    public static class ErrorCodes
    {
        public const string UnknownTool = "UNKNOWN_TOOL";
        public const string ArgValidationFailed = "ARG_VALIDATION_FAILED";
        public const string Timeout = "TIMEOUT";
        public const string InvocationFailed = "INVOCATION_FAILED";
        public const string Cancelled = "CANCELLED";
    }
}
