using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using LocalAIAgent.Agent.Handlers;
using LocalAIAgent.Agent.Integration;
using LocalAIAgent.Agent.Parsing;
using LocalAIAgent.Agent.Dispatching;
using System.Text.Json;
using Xunit;

namespace LocalAIAgent.Agent.Tests.Handlers;

/// <summary>
/// Unit tests for FunctoolsHandler (T041).
/// Validates User Story 3: Pluggable Tool Invocation Handlers.
/// </summary>
public class FunctoolsHandlerTests
{
    private readonly TestFunctoolsParser _parser;
    private readonly TestToolInvoker _invoker;
    private readonly FunctoolsHandler _handler;

    public FunctoolsHandlerTests()
    {
        _parser = new TestFunctoolsParser();
        _invoker = new TestToolInvoker();
        _handler = new FunctoolsHandler(_parser, _invoker, NullLogger<FunctoolsChatClient>.Instance);
    }

    [Fact]
    public void CreateHandler_WithValidClient_ReturnsWrappedClient()
    {
        // Arrange
        var innerClient = new TestChatClient();

        // Act
        var wrappedClient = _handler.CreateHandler(innerClient);

        // Assert
        Assert.NotNull(wrappedClient);
        Assert.IsType<FunctoolsChatClient>(wrappedClient);
    }

    [Fact]
    public void CreateHandler_WithNullClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _handler.CreateHandler(null!));
    }

    [Fact]
    public async Task FunctoolsHandler_ParsesValidBlock_InvokesTool()
    {
        // Arrange
        var innerClient = new TestChatClient
        {
            ResponseText = "functools[{\"name\": \"GetWeather\", \"arguments\": {\"location\": \"Seattle\"}}]"
        };
        var wrappedClient = _handler.CreateHandler(innerClient);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in Seattle?")
        };

        // Simulate functools block parsing
        _parser.SetFunctionCalls(new[]
        {
            new FunctionCall
            {
                Name = "GetWeather",
                Arguments = JsonDocument.Parse("{\"location\": \"Seattle\"}").RootElement
            }
        });

        // Simulate tool invocation result
        _invoker.SetToolResult(new ToolResult
        {
            Name = "GetWeather",
            Content = "{\"temperature\": 55, \"condition\": \"Cloudy\"}",
            Duration = TimeSpan.FromMilliseconds(100)
        });

        // Act
        var response = await wrappedClient.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        Assert.True(_parser.ParseCalled);
        Assert.True(_invoker.InvokeCalled);
        Assert.Equal("GetWeather", _invoker.LastToolName);
    }

    [Fact]
    public async Task FunctoolsHandler_WithInvalidJson_HandlesParserException()
    {
        // Arrange
        var innerClient = new TestChatClient
        {
            ResponseText = "functools[{invalid json}]"
        };
        var wrappedClient = _handler.CreateHandler(innerClient);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test message")
        };

        // Simulate parser exception
        _parser.SetException(new ParserException("MALFORMED_BLOCK", "Invalid JSON in functools block"));

        // Act
        var response = await wrappedClient.GetResponseAsync(messages);

        // Assert - Handler should return error message instead of crashing
        Assert.NotNull(response);
        Assert.Single(response.Messages);
        Assert.Contains("error processing tool calls", response.Messages[0].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FunctoolsHandler_WithNoFunctoolsBlocks_ReturnsOriginalResponse()
    {
        // Arrange
        var innerClient = new TestChatClient
        {
            ResponseText = "The weather in Seattle is sunny."
        };
        var wrappedClient = _handler.CreateHandler(innerClient);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather?")
        };

        // Parser returns empty (no functools blocks detected)
        _parser.SetFunctionCalls(Array.Empty<FunctionCall>());

        // Act
        var response = await wrappedClient.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        Assert.False(_invoker.InvokeCalled); // Tool invoker should not be called
        Assert.Contains("sunny", response.Messages[0].Text);
    }

    [Fact]
    public async Task FunctoolsHandler_WithToolFailure_ReturnsErrorInResult()
    {
        // Arrange
        var innerClient = new TestChatClient
        {
            ResponseText = "functools[{\"name\": \"GetWeather\", \"arguments\": {\"location\": \"InvalidLocation\"}}]"
        };
        var wrappedClient = _handler.CreateHandler(innerClient);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What's the weather in InvalidLocation?")
        };

        _parser.SetFunctionCalls(new[]
        {
            new FunctionCall
            {
                Name = "GetWeather",
                Arguments = JsonDocument.Parse("{\"location\": \"InvalidLocation\"}").RootElement
            }
        });

        // Simulate tool invocation failure
        _invoker.SetToolResult(new ToolResult
        {
            Name = "GetWeather",
            Error = "INVOCATION_FAILED: Location not found",
            Duration = TimeSpan.FromMilliseconds(50)
        });

        // Act
        var response = await wrappedClient.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        Assert.True(_invoker.InvokeCalled);
        Assert.Equal("GetWeather", _invoker.LastToolName);
    }

    [Fact]
    public async Task FunctoolsHandler_WithMultipleToolCalls_InvokesAll()
    {
        // Arrange
        var innerClient = new TestChatClient
        {
            ResponseText = "functools[{\"name\": \"GetWeather\", \"arguments\": {\"location\": \"Seattle\"}}, {\"name\": \"GetAirQuality\", \"arguments\": {\"location\": \"Seattle\"}}]"
        };
        var wrappedClient = _handler.CreateHandler(innerClient);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Get weather and air quality for Seattle")
        };

        _parser.SetFunctionCalls(new[]
        {
            new FunctionCall
            {
                Name = "GetWeather",
                Arguments = JsonDocument.Parse("{\"location\": \"Seattle\"}").RootElement
            },
            new FunctionCall
            {
                Name = "GetAirQuality",
                Arguments = JsonDocument.Parse("{\"location\": \"Seattle\"}").RootElement
            }
        });

        // Track invocation count
        var invocationCount = 0;
        _invoker.OnInvoke = (name, args) =>
        {
            invocationCount++;
            return new ToolResult
            {
                Name = name,
                Content = $"{{\"data\": \"result for {name}\"}}",
                Duration = TimeSpan.FromMilliseconds(100)
            };
        };

        // Act
        var response = await wrappedClient.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, invocationCount);
    }

    #region Test Doubles

    private class TestFunctoolsParser : IFunctoolsParser
    {
        private IEnumerable<FunctionCall> _functionCalls = Array.Empty<FunctionCall>();
        private ParserException? _exception;

        public bool ParseCalled { get; private set; }

        public void SetFunctionCalls(IEnumerable<FunctionCall> calls)
        {
            _functionCalls = calls;
        }

        public void SetException(ParserException exception)
        {
            _exception = exception;
        }

        public IEnumerable<FunctionCall> Parse(ReadOnlySpan<char> chunk)
        {
            ParseCalled = true;
            if (_exception != null)
                throw _exception;
            return _functionCalls;
        }

        public void Reset()
        {
            // No-op for tests
        }
    }

    private class TestToolInvoker : IToolInvoker
    {
        private ToolResult? _toolResult;

        public bool InvokeCalled { get; private set; }
        public string? LastToolName { get; private set; }
        public JsonElement LastArguments { get; private set; }
        public Func<string, JsonElement, ToolResult>? OnInvoke { get; set; }

        public void SetToolResult(ToolResult result)
        {
            _toolResult = result;
        }

        public Task<ToolResult> InvokeAsync(string name, JsonElement args, CancellationToken ct)
        {
            InvokeCalled = true;
            LastToolName = name;
            LastArguments = args;

            if (OnInvoke != null)
                return Task.FromResult(OnInvoke(name, args));

            return Task.FromResult(_toolResult ?? new ToolResult
            {
                Name = name,
                Content = "{}",
                Duration = TimeSpan.Zero
            });
        }

        public Task<ValidationResult> ValidateAsync(string name, JsonElement args)
        {
            // No-op for tests
            return Task.FromResult(new ValidationResult { IsValid = true });
        }
    }

    private class TestChatClient : IChatClient
    {
        public string ResponseText { get; set; } = string.Empty;

        public ChatClientMetadata Metadata => new("test-provider", new Uri("http://localhost"), "test-model");

        public void Dispose() { }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public TService? GetService<TService>(object? serviceKey = null) where TService : class => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, ResponseText));
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new ChatResponseUpdate
            {
                Contents = [new TextContent(ResponseText)]
            };
        }
    }

    #endregion
}
