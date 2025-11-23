namespace LocalAIAgent.Agent.Parsing;

using System;
using System.Collections.Generic;

/// <summary>
/// Parses functools blocks from Phi-4-mini model responses.
/// Detects "functools[...]" format and extracts FunctionCall objects.
/// </summary>
public interface IFunctoolsParser
{
    /// <summary>
    /// Parses functools blocks from a text chunk (typically streaming response).
    /// </summary>
    /// <param name="chunk">Text chunk from model response (may contain partial blocks)</param>
    /// <returns>
    /// Enumerable of FunctionCall objects for each complete functools block found.
    /// Returns empty if no complete blocks detected in this chunk.
    /// </returns>
    /// <exception cref="ParserException">
    /// Thrown when a complete functools block is malformed (invalid JSON, missing fields).
    /// Error codes: MALFORMED_BLOCK, INCOMPLETE_STREAM
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is designed for streaming scenarios where chunks arrive incrementally.
    /// It maintains internal state to accumulate partial blocks across Parse() calls.
    /// </para>
    /// <para>
    /// Performance: <50ms for 1MB chunk (NFR-001 requirement).
    /// </para>
    /// <para>
    /// Example input:
    /// <code>
    /// "The weather is... functools[{\"name\": \"GetWeather\", \"arguments\": {\"location\": \"Seattle\"}}]"
    /// </code>
    /// </para>
    /// <para>
    /// Example output:
    /// <code>
    /// FunctionCall { Name = "GetWeather", Arguments = JsonElement: {\"location\": \"Seattle\"} }
    /// </code>
    /// </para>
    /// </remarks>
    IEnumerable<FunctionCall> Parse(ReadOnlySpan<char> chunk);
    
    /// <summary>
    /// Resets parser state (clears any buffered partial blocks).
    /// Call this when starting a new conversation turn or when cancelling a streaming response.
    /// </summary>
    void Reset();
}
