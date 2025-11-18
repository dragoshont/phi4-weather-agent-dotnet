using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Phi4WeatherAgent.Agent.Parsing;
using Phi4WeatherAgent.Agent.Dispatching;

namespace Phi4WeatherAgent.Agent.Integration;

/// <summary>
/// IChatClient decorator that intercepts model responses to parse functools blocks,
/// execute tools, and re-prompt model with results.
/// 
/// Execution flow:
/// 1. Call inner chat client (get model response)
/// 2. Scan response for functools blocks using IFunctoolsParser
/// 3. Extract FunctionCall array
/// 4. Invoke each tool via IToolInvoker
/// 5. Convert ToolResult array to ChatMessage array (role=tool)
/// 6. Append tool messages to conversation history
/// 7. Re-prompt model with updated history (includes tool results)
/// 8. Return final assistant response (after tool execution)
/// </summary>
public sealed class FunctoolsChatClient : IChatClient
{
    private readonly IChatClient _innerClient;
    private readonly IFunctoolsParser _parser;
    private readonly IToolInvoker _invoker;
    private readonly ILogger<FunctoolsChatClient> _logger;

    public FunctoolsChatClient(
        IChatClient innerClient,
        IFunctoolsParser parser,
        IToolInvoker invoker,
        ILogger<FunctoolsChatClient> logger)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose() => _innerClient.Dispose();

    public object? GetService(Type serviceType, object? serviceKey = null)
        => _innerClient.GetService(serviceType, serviceKey);

    public TService? GetService<TService>(object? serviceKey = null) where TService : class
        => this as TService ?? _innerClient.GetService<TService>(serviceKey);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Reset parser state for new turn
        _parser.Reset();

        var messagesList = chatMessages.ToList();

        // Call inner client to get initial model response
        var response = await _innerClient.GetResponseAsync(messagesList, options, cancellationToken);

        // Extract response text from all messages
        var responseText = string.Join("", response.Messages.Select(m => m.Text ?? ""));
        if (string.IsNullOrWhiteSpace(responseText))
        {
            _logger.LogDebug("Empty response from model, returning as-is");
            return response;
        }

        _logger.LogDebug("Model response: {Response}", responseText);

        // Parse functools blocks from response
        IEnumerable<FunctionCall> functionCalls;
        try
        {
            functionCalls = _parser.Parse(responseText.AsSpan());
        }
        catch (ParserException ex)
        {
            _logger.LogError(ex, "Failed to parse functools block: {Error}", ex.Message);
            // Return error as assistant message (don't crash conversation)
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, 
                $"I encountered an error processing tool calls: {ex.Message}"));
        }

        var calls = functionCalls.ToList();
        if (calls.Count == 0)
        {
            _logger.LogDebug("No functools blocks detected, returning original response");
            return response;
        }

        _logger.LogInformation("Detected {Count} tool calls: {Tools}", 
            calls.Count, string.Join(", ", calls.Select(c => c.Name)));

        // Invoke each tool
        var toolResults = new List<ToolResult>();
        foreach (var call in calls)
        {
            try
            {
                call.Validate();
                _logger.LogDebug("Invoking tool: {ToolName} with arguments: {Args}", 
                    call.Name, call.Arguments);

                var result = await _invoker.InvokeAsync(call.Name, call.Arguments, cancellationToken);
                toolResults.Add(result);

                if (result.Error != null)
                {
                    _logger.LogWarning("Tool {ToolName} failed: {Error}", call.Name, result.Error);
                }
                else
                {
                    _logger.LogInformation("Tool {ToolName} succeeded in {Duration}ms", 
                        call.Name, result.Duration?.TotalMilliseconds ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error invoking tool {ToolName}", call.Name);
                toolResults.Add(new ToolResult
                {
                    Name = call.Name,
                    Error = $"INVOCATION_FAILED: {ex.Message}"
                });
            }
        }

        // Convert tool results to ChatMessages with role=tool or system
        var toolMessages = ConvertToolResultsToMessages(toolResults);

        // Append original assistant response (with functools block)
        var updatedMessages = messagesList.ToList();
        updatedMessages.Add(new ChatMessage(ChatRole.Assistant, responseText));

        // Append tool result messages
        updatedMessages.AddRange(toolMessages);

        _logger.LogDebug("Re-prompting model with {Count} tool results", toolResults.Count);

        // Re-prompt model with updated history (includes tool results)
        var finalResponse = await _innerClient.GetResponseAsync(updatedMessages, options, cancellationToken);

        _logger.LogDebug("Final model response after tool execution: {Response}", 
            string.Join("", finalResponse.Messages.Select(m => m.Text ?? "")));

        return finalResponse;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Buffer the streaming response to detect functools
        // IMPORTANT: We must buffer BEFORE yielding to avoid showing functools to user
        _parser.Reset();
        var messagesList = chatMessages.ToList();
        var fullResponseText = new StringBuilder();

        _logger.LogDebug("Streaming mode - buffering response to detect functools");

        // Collect all streaming updates WITHOUT yielding them yet
        await foreach (var update in _innerClient.GetStreamingResponseAsync(messagesList, options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullResponseText.Append(update.Text);
            }
        }

        var responseText = fullResponseText.ToString();
        
        _logger.LogWarning("BUFFERED RESPONSE (length={Length}): '{Text}'", responseText.Length, responseText);
        
        // Try to parse functools
        IEnumerable<FunctionCall> functionCalls;
        try
        {
            functionCalls = _parser.Parse(responseText.AsSpan());
        }
        catch (ParserException ex)
        {
            _logger.LogError(ex, "Failed to parse functools in streaming mode: {Error}", ex.Message);
            functionCalls = new List<FunctionCall>();
        }

        var calls = functionCalls.ToList();
        
        // If no functools detected, yield the buffered response as a single update
        if (calls.Count == 0)
        {
            _logger.LogInformation("No functools detected in stream (length={Length}), returning buffered text", responseText.Length);
            // Yield the entire buffered response as one update
            yield return new ChatResponseUpdate 
            { 
                Contents = [new TextContent(responseText)]
            };
            yield break;
        }

        _logger.LogWarning("FUNCTOOLS DETECTED! Count={Count}, Tools={Tools}", 
            calls.Count, string.Join(", ", calls.Select(c => c.Name)));

        // Execute tools
        var toolResults = new List<ToolResult>();
        foreach (var call in calls)
        {
            _logger.LogDebug("Invoking tool: {ToolName} with args: {Args}", call.Name, call.Arguments);
            var result = await _invoker.InvokeAsync(call.Name, call.Arguments, cancellationToken);
            toolResults.Add(result);
            
            if (result.Error != null)
            {
                _logger.LogWarning("Tool {ToolName} failed: {Error}", call.Name, result.Error);
            }
            else
            {
                _logger.LogInformation("Tool {ToolName} succeeded in {Duration}ms", 
                    call.Name, result.Duration?.TotalMilliseconds);
            }
        }

        // Convert results and re-prompt
        var toolMessages = ConvertToolResultsToMessages(toolResults);
        var updatedMessages = messagesList.ToList();
        updatedMessages.Add(new ChatMessage(ChatRole.Assistant, responseText));
        updatedMessages.AddRange(toolMessages);

        _logger.LogDebug("Re-prompting model with {Count} tool results", toolResults.Count);

        // Stream the final response (this will NOT contain functools)
        await foreach (var update in _innerClient.GetStreamingResponseAsync(updatedMessages, options, cancellationToken))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Converts ToolResult array to ChatMessage array with role=tool.
    /// Format: JSON with tool name, content/error, duration.
    /// </summary>
    private List<ChatMessage> ConvertToolResultsToMessages(List<ToolResult> results)
    {
        var messages = new List<ChatMessage>();

        foreach (var result in results)
        {
            // Format tool result as structured message
            var resultJson = new
            {
                tool = result.Name,
                success = result.Error == null,
                content = result.Content,
                error = result.Error,
                duration_ms = result.Duration?.TotalMilliseconds
            };

            var messageText = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions 
            { 
                WriteIndented = false 
            });

            // Use System role for tool results (Microsoft.Extensions.AI may not support Tool role yet)
            messages.Add(new ChatMessage(ChatRole.System, messageText));

            _logger.LogDebug("Converted tool result to message: {Tool} - {Success}", 
                result.Name, result.Error == null);
        }

        return messages;
    }
}
