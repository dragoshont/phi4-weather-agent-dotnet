namespace LocalAIAgent.Agent.Parsing;

/// <summary>
/// Thrown by parser for malformed functools blocks.
/// </summary>
public class ParserException : Exception
{
    /// <summary>
    /// Error code identifying the parsing failure.
    /// Valid values: MALFORMED_BLOCK, INCOMPLETE_STREAM.
    /// </summary>
    public string ErrorCode { get; }

    public ParserException(string errorCode, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("ErrorCode cannot be null or whitespace", nameof(errorCode));

        ErrorCode = errorCode;
    }

    public ParserException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("ErrorCode cannot be null or whitespace", nameof(errorCode));

        ErrorCode = errorCode;
    }

    /// <summary>
    /// Standard error codes for parser failures.
    /// </summary>
    public static class ErrorCodes
    {
        public const string MalformedBlock = "MALFORMED_BLOCK";
        public const string IncompleteStream = "INCOMPLETE_STREAM";
    }
}
