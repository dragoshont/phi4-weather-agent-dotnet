using System.Text;
using System.Text.Json;

namespace Phi4WeatherAgent.Agent.Parsing;

/// <summary>
/// Parses functools blocks from Phi-4-mini model responses using a buffered state machine.
/// Handles streaming scenarios where chunks arrive incrementally.
/// Performance: O(n) time, O(k) space with 16KB buffer.
/// </summary>
public sealed class FunctoolsParser : IFunctoolsParser
{
    private readonly StringBuilder _buffer = new(16384); // 16KB buffer
    private ParserState _state = ParserState.Start;
    private int _blockStart = -1;
    private int _bracketDepth = 0;

    /// <summary>
    /// Parser state machine states.
    /// </summary>
    private enum ParserState
    {
        Start,        // Initial state
        F,            // 'f' seen
        U,            // 'fu' seen
        N1,           // 'fun' seen
        C,            // 'func' seen
        T,            // 'funct' seen
        O,            // 'functo' seen
        O2,           // 'functoo' seen
        L,            // 'functool' seen
        S,            // 'functools' seen
        BlockStart,   // 'functools[' seen, now parsing JSON
        BlockParsing  // Inside JSON block, tracking bracket depth
    }

    /// <inheritdoc />
    public IEnumerable<FunctionCall> Parse(ReadOnlySpan<char> chunk)
    {
        _buffer.Append(chunk);
        var text = _buffer.ToString();
        var result = new List<FunctionCall>();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            switch (_state)
            {
                case ParserState.Start:
                    if (c == 'f') _state = ParserState.F;
                    break;

                case ParserState.F:
                    if (c == 'u') _state = ParserState.U;
                    else if (c == 'f') _state = ParserState.F; // Stay in F if another 'f'
                    else _state = ParserState.Start;
                    break;

                case ParserState.U:
                    if (c == 'n') _state = ParserState.N1;
                    else if (c == 'f') _state = ParserState.F;
                    else _state = ParserState.Start;
                    break;

                case ParserState.N1:
                    if (c == 'c') _state = ParserState.C;
                    else if (c == 'f') _state = ParserState.F;
                    else _state = ParserState.Start;
                    break;

                case ParserState.C:
                    if (c == 't') _state = ParserState.T;
                    else if (c == 'f') _state = ParserState.F;
                    else _state = ParserState.Start;
                    break;

                case ParserState.T:
                    if (c == 'o') _state = ParserState.O;
                    else if (c == 'f') _state = ParserState.F;
                    else _state = ParserState.Start;
                    break;

                case ParserState.O:
                    if (c == 'o') _state = ParserState.O2;
                    else if (c == 'f') _state = ParserState.F;
                    else _state = ParserState.Start;
                    break;

                case ParserState.O2:
                    if (c == 'l') _state = ParserState.L;
                    else if (c == 'f') _state = ParserState.F;
                    else _state = ParserState.Start;
                    break;

                case ParserState.L:
                    if (c == 's') _state = ParserState.S;
                    else if (c == 'f') _state = ParserState.F;
                    else _state = ParserState.Start;
                    break;

                case ParserState.S:
                    if (c == '[')
                    {
                        _state = ParserState.BlockStart;
                        _blockStart = i + 1; // Start after '['
                        _bracketDepth = 1;
                    }
                    else if (c == 'f')
                    {
                        _state = ParserState.F;
                    }
                    else
                    {
                        _state = ParserState.Start;
                    }
                    break;

                case ParserState.BlockStart:
                case ParserState.BlockParsing:
                    // Track bracket depth to find matching ']'
                    if (c == '[')
                    {
                        _bracketDepth++;
                        _state = ParserState.BlockParsing;
                    }
                    else if (c == ']')
                    {
                        _bracketDepth--;
                        if (_bracketDepth == 0)
                        {
                            // Found matching closing bracket
                            var blockLength = i - _blockStart;
                            var blockText = text.AsSpan(_blockStart, blockLength);

                            try
                            {
                                var calls = ParseBlock(blockText);
                                result.AddRange(calls);

                                // Clear buffer up to and including ']'
                                _buffer.Remove(0, i + 1);
                                i = -1; // Reset loop counter (will increment to 0)
                                text = _buffer.ToString();
                                _state = ParserState.Start;
                                _blockStart = -1;
                            }
                            catch (JsonException ex)
                            {
                                throw new ParserException(
                                    ParserException.ErrorCodes.MalformedBlock,
                                    $"Invalid JSON in functools block: {ex.Message}",
                                    ex);
                            }
                        }
                        else
                        {
                            _state = ParserState.BlockParsing;
                        }
                    }
                    else
                    {
                        _state = ParserState.BlockParsing;
                    }
                    break;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _buffer.Clear();
        _state = ParserState.Start;
        _blockStart = -1;
        _bracketDepth = 0;
    }

    /// <summary>
    /// Sanitizes common malformed JSON patterns from model output.
    /// Handles cases like extra braces: {"arguments":{"location":"X"}}} -> {"arguments":{"location":"X"}}
    /// </summary>
    private string SanitizeFunctoolsJson(string json)
    {
        // Pattern: Model often adds extra closing braces before final ]
        // Example: {"name":"X","arguments":{"location":"Y"}}}] should be {"name":"X","arguments":{"location":"Y"}}]
        
        // Count opening and closing braces to detect imbalance
        int openBraces = 0;
        int closeBraces = 0;
        
        foreach (char c in json)
        {
            if (c == '{') openBraces++;
            else if (c == '}') closeBraces++;
        }
        
        // If more closing than opening braces, try to fix by removing extras from the end
        if (closeBraces > openBraces)
        {
            int extraBraces = closeBraces - openBraces;
            StringBuilder sb = new StringBuilder(json);
            
            // Remove extra } from the end (before any trailing whitespace)
            for (int removed = 0; removed < extraBraces; removed++)
            {
                for (int i = sb.Length - 1; i >= 0; i--)
                {
                    if (sb[i] == '}')
                    {
                        sb.Remove(i, 1);
                        break;
                    }
                }
            }
            
            return sb.ToString();
        }
        
        return json;
    }

    /// <summary>
    /// Parses a functools block (JSON array of function calls).
    /// </summary>
    private IEnumerable<FunctionCall> ParseBlock(ReadOnlySpan<char> blockText)
    {
        var result = new List<FunctionCall>();

        // Sanitize common malformed patterns from model output
        var sanitized = SanitizeFunctoolsJson(blockText.ToString());

        // Parse as JSON array
        using var doc = JsonDocument.Parse(sanitized);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            // Multiple function calls: functools[{...}, {...}]
            foreach (var element in root.EnumerateArray())
            {
                result.Add(ParseFunctionCall(element));
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            // Single function call: functools[{...}]
            result.Add(ParseFunctionCall(root));
        }
        else
        {
            throw new ParserException(
                ParserException.ErrorCodes.MalformedBlock,
                $"Functools block must be JSON object or array, got {root.ValueKind}");
        }

        return result;
    }

    /// <summary>
    /// Parses a single function call JSON object.
    /// </summary>
    private FunctionCall ParseFunctionCall(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ParserException(
                ParserException.ErrorCodes.MalformedBlock,
                $"Function call must be JSON object, got {element.ValueKind}");
        }

        if (!element.TryGetProperty("name", out var nameProperty))
        {
            throw new ParserException(
                ParserException.ErrorCodes.MalformedBlock,
                "Function call missing required 'name' property");
        }

        if (nameProperty.ValueKind != JsonValueKind.String)
        {
            throw new ParserException(
                ParserException.ErrorCodes.MalformedBlock,
                $"Function call 'name' must be string, got {nameProperty.ValueKind}");
        }

        var name = nameProperty.GetString() ?? throw new ParserException(
            ParserException.ErrorCodes.MalformedBlock,
            "Function call 'name' cannot be null");

        // Arguments are optional, default to empty object
        var arguments = element.TryGetProperty("arguments", out var argsProperty)
            ? argsProperty
            : JsonDocument.Parse("{}").RootElement;

        var call = new FunctionCall
        {
            Name = name,
            Arguments = arguments.Clone() // Clone to avoid dispose issues
        };

        // Validate the function call
        call.Validate();

        return call;
    }
}
