using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Phi4WeatherAgent.Agent.Dispatching;
using Phi4WeatherAgent.Agent.Registry;
using Json.Schema;

namespace Phi4WeatherAgent.Agent.Tests.Dispatching;

/// <summary>
/// Unit tests for ToolInvoker (US1, US3, US4, US5).
/// Tests argument validation, timeout enforcement, exception handling, and rate limiting.
/// </summary>
public class ToolInvokerTests
{
    private readonly Mock<IToolRegistry> _mockRegistry = new();
    private readonly ToolInvoker _invoker;

    public ToolInvokerTests()
    {
        _invoker = new ToolInvoker(_mockRegistry.Object, NullLogger<ToolInvoker>.Instance);
    }

    [Fact]
    public async Task InvokeAsync_ValidToolAndArguments_ReturnsSuccess()
    {
        // Arrange
        var descriptor = CreateTestDescriptor("GetWeather", args =>
        {
            var location = args.GetProperty("location").GetString();
            return Task.FromResult(new ToolResult
            {
                Name = "GetWeather",
                Content = $"Weather in {location}: Sunny, 22°C",
                Duration = TimeSpan.FromMilliseconds(10)
            });
        });

        _mockRegistry.Setup(r => r.TryGet("GetWeather", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        var args = JsonDocument.Parse("{\"location\":\"Seattle\"}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("GetWeather", args, CancellationToken.None);

        // Assert
        result.Name.Should().Be("GetWeather");
        result.Content.Should().Contain("Seattle");
        result.Error.Should().BeNull();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task InvokeAsync_UnknownTool_ReturnsUnknownToolError()
    {
        // Arrange - US5 Scenario 1: Unknown tool rejection
        _mockRegistry.Setup(r => r.TryGet("UnknownTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = null;
                return false;
            });

        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("UnknownTool", args, CancellationToken.None);

        // Assert
        result.Name.Should().Be("UnknownTool");
        result.Error.Should().Be("UNKNOWN_TOOL");
        result.Content.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_ToolThrowsException_ReturnsSanitizedError()
    {
        // Arrange - US4: Exception handling
        var descriptor = CreateTestDescriptor("FailingTool", args =>
        {
            throw new InvalidOperationException("Internal database connection failed at C:\\secrets\\db.config");
        });

        _mockRegistry.Setup(r => r.TryGet("FailingTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("FailingTool", args, CancellationToken.None);

        // Assert
        result.Error.Should().Be("INVOCATION_FAILED");
        result.Content.Should().BeNull();
        // Sanitization: Should NOT leak file paths or stack traces
        result.Error.Should().NotContain("C:\\secrets");
        result.Error.Should().NotContain("at System.");
    }

    [Fact]
    public async Task InvokeAsync_ToolExceedsTimeout_ReturnsTimeoutError()
    {
        // Arrange - US1: Timeout enforcement
        var descriptor = CreateTestDescriptor("SlowTool", async args =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10)); // Exceeds 2s timeout
            return new ToolResult { Name = "SlowTool", Content = "Done" };
        }, timeout: TimeSpan.FromSeconds(2));

        _mockRegistry.Setup(r => r.TryGet("SlowTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("SlowTool", args, CancellationToken.None);

        // Assert
        result.Error.Should().Be("TIMEOUT");
        result.Content.Should().BeNull();
        result.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(2), precision: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task InvokeAsync_CancellationRequested_ReturnsCancelledError()
    {
        // Arrange
        var descriptor = CreateTestDescriptor("LongTool", async args =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return new ToolResult { Name = "LongTool", Content = "Done" };
        });

        _mockRegistry.Setup(r => r.TryGet("LongTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("LongTool", args, cts.Token);

        // Assert
        result.Error.Should().Be("CANCELLED");
        result.Content.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_TracksDuration_ReturnsAccurateTiming()
    {
        // Arrange - US1: Duration tracking
        var descriptor = CreateTestDescriptor("TimedTool", async args =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            return new ToolResult { Name = "TimedTool", Content = "Done" };
        });

        _mockRegistry.Setup(r => r.TryGet("TimedTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        var args = JsonDocument.Parse("{}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("TimedTool", args, CancellationToken.None);

        // Assert
        result.Duration.Should().NotBeNull();
        result.Duration!.Value.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(50);
        result.Duration!.Value.TotalMilliseconds.Should().BeLessThan(200); // Reasonable upper bound
    }

    [Fact]
    public async Task InvokeAsync_ValidatesJsonSchema_RejectsInvalidArgs()
    {
        // Arrange - US3 Scenario 1: Schema validation
        var schema = JsonSchema.FromText(@"{
            ""type"": ""object"",
            ""properties"": {
                ""location"": { ""type"": ""string"", ""maxLength"": 10 }
            },
            ""required"": [""location""]
        }");

        var descriptor = CreateTestDescriptor("ValidatedTool", args =>
        {
            return Task.FromResult(new ToolResult { Name = "ValidatedTool", Content = "OK" });
        }, schema: schema);

        _mockRegistry.Setup(r => r.TryGet("ValidatedTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        // Invalid: location exceeds maxLength
        var args = JsonDocument.Parse("{\"location\":\"ThisIsAVeryLongLocationName\"}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("ValidatedTool", args, CancellationToken.None);

        // Assert
        result.Error.Should().Be("ARG_VALIDATION_FAILED");
        result.Content.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_NoSchema_SkipsValidation()
    {
        // Arrange - US3 Scenario 3: Opt-in validation
        var descriptor = CreateTestDescriptor("UnvalidatedTool", args =>
        {
            return Task.FromResult(new ToolResult { Name = "UnvalidatedTool", Content = "OK" });
        }, schema: null);

        _mockRegistry.Setup(r => r.TryGet("UnvalidatedTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        // Any arguments (no schema to validate against)
        var args = JsonDocument.Parse("{\"anything\":\"goes\"}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("UnvalidatedTool", args, CancellationToken.None);

        // Assert
        result.Error.Should().BeNull();
        result.Content.Should().Be("OK");
    }

    [Fact]
    public async Task InvokeAsync_PerformanceBenchmark_MeetsNFR()
    {
        // Arrange - NFR-002: Dispatcher validation <5ms
        var descriptor = CreateTestDescriptor("FastTool", args =>
        {
            return Task.FromResult(new ToolResult { Name = "FastTool", Content = "OK" });
        });

        _mockRegistry.Setup(r => r.TryGet("FastTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        var args = JsonDocument.Parse("{\"test\":\"value\"}").RootElement;

        // Act - Warmup
        await _invoker.InvokeAsync("FastTool", args, CancellationToken.None);

        // Act - Benchmark
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _invoker.InvokeAsync("FastTool", args, CancellationToken.None);
        sw.Stop();

        // Assert - Total invocation overhead <5ms (excluding actual tool execution)
        sw.ElapsedMilliseconds.Should().BeLessThan(5, "NFR-002: Dispatcher overhead <5ms");
    }

    [Fact]
    public async Task InvokeAsync_LargeArgumentPayload_RejectsOversizedArgs()
    {
        // Arrange - US3: Reject >10MB payloads (security constraint)
        var descriptor = CreateTestDescriptor("SizeLimitedTool", args =>
        {
            return Task.FromResult(new ToolResult { Name = "SizeLimitedTool", Content = "OK" });
        });

        _mockRegistry.Setup(r => r.TryGet("SizeLimitedTool", out It.Ref<ToolDescriptor?>.IsAny))
            .Returns((string name, out ToolDescriptor? desc) =>
            {
                desc = descriptor;
                return true;
            });

        // Create >10MB JSON payload
        var largeString = new string('x', 11 * 1024 * 1024); // 11MB
        var args = JsonDocument.Parse($"{{\"data\":\"{largeString}\"}}").RootElement;

        // Act
        var result = await _invoker.InvokeAsync("SizeLimitedTool", args, CancellationToken.None);

        // Assert
        result.Error.Should().Be("ARG_VALIDATION_FAILED");
        result.Content.Should().BeNull();
    }

    private static ToolDescriptor CreateTestDescriptor(
        string name,
        Func<JsonElement, ValueTask<ToolResult>> invoker,
        JsonSchema? schema = null,
        TimeSpan? timeout = null)
    {
        return new ToolDescriptor
        {
            Name = name,
            Source = $"Test:{name}",
            ArgsSchema = schema,
            Invoker = invoker,
            SecurityClass = SecurityClass.Public,
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
    }
}
