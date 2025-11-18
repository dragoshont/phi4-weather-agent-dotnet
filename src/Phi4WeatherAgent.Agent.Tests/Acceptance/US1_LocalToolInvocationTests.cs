using System.Text.Json;
using FluentAssertions;
using Phi4WeatherAgent.Agent.Registry;
using Phi4WeatherAgent.Agent.Dispatching;

namespace Phi4WeatherAgent.Agent.Tests.Acceptance;

/// <summary>
/// Acceptance tests for User Story 1: Parse functools and invoke local C# tools.
/// Tests the complete end-to-end flow from parsing to invocation.
/// </summary>
public class US1_LocalToolInvocationTests
{
    [Fact]
    public async Task Scenario1_ParseAndInvokeLocalTool_Success()
    {
        // US1 Scenario 1: Developer adds [Tool("GetWeather")] method to WeatherTools.cs
        // → Starts app → Tool discovered and registered
        // → User asks "What's the weather in Seattle?"
        // → Phi-4-mini outputs functools[{"name":"GetWeather","arguments":{"location":"Seattle"}}]
        // → Parser extracts FunctionCall → Dispatcher invokes tool → Returns ToolResult

        // Arrange
        var parser = new Phi4WeatherAgent.Agent.Parsing.FunctoolsParser();
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry);

        // Register test tool
        var weatherTool = new ToolDescriptor
        {
            Name = "GetWeather",
            Source = "Local:WeatherTools",
            ArgsSchema = null,
            Invoker = async (args) =>
            {
                var location = args.GetProperty("location").GetString();
                return new ToolResult
                {
                    Name = "GetWeather",
                    Content = $"{{\"temperature\":22,\"conditions\":\"Sunny\",\"location\":\"{location}\"}}",
                    Duration = TimeSpan.FromMilliseconds(50)
                };
            },
            SecurityClass = Phi4WeatherAgent.Tools.SecurityClass.Public,
            Timeout = TimeSpan.FromSeconds(10)
        };
        registry.Register(weatherTool);

        var modelResponse = "The weather in Seattle is: functools[{\"name\":\"GetWeather\",\"arguments\":{\"location\":\"Seattle\"}}]";

        // Act
        var calls = parser.Parse(modelResponse);
        calls.Should().ContainSingle();

        var result = await invoker.InvokeAsync(calls[0].Name, calls[0].Arguments, CancellationToken.None);

        // Assert
        result.Error.Should().BeNull();
        result.Content.Should().Contain("Seattle");
        result.Content.Should().Contain("Sunny");
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Scenario2_MultipleToolCalls_AllExecuted()
    {
        // US1 Scenario 2: User asks "What's the weather in Seattle and Portland?"
        // → Model outputs two function calls in functools array
        // → Both tools invoked successfully

        // Arrange
        var parser = new Phi4WeatherAgent.Agent.Parsing.FunctoolsParser();
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry);

        var weatherTool = new ToolDescriptor
        {
            Name = "GetWeather",
            Source = "Local:WeatherTools",
            ArgsSchema = null,
            Invoker = async (args) =>
            {
                var location = args.GetProperty("location").GetString();
                return new ToolResult
                {
                    Name = "GetWeather",
                    Content = $"Weather in {location}: 20°C",
                    Duration = TimeSpan.FromMilliseconds(30)
                };
            },
            SecurityClass = Phi4WeatherAgent.Tools.SecurityClass.Public,
            Timeout = TimeSpan.FromSeconds(10)
        };
        registry.Register(weatherTool);

        var modelResponse = "functools[{\"name\":\"GetWeather\",\"arguments\":{\"location\":\"Seattle\"}},{\"name\":\"GetWeather\",\"arguments\":{\"location\":\"Portland\"}}]";

        // Act
        var calls = parser.Parse(modelResponse);
        var results = new List<ToolResult>();
        foreach (var call in calls)
        {
            var result = await invoker.InvokeAsync(call.Name, call.Arguments, CancellationToken.None);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(2);
        results[0].Content.Should().Contain("Seattle");
        results[1].Content.Should().Contain("Portland");
        results.All(r => r.Error == null).Should().BeTrue();
    }

    [Fact]
    public async Task Scenario3_ToolWithComplexArguments_ParsedAndInvokedCorrectly()
    {
        // US1 Scenario 3: Tool with complex nested arguments
        // → Parser extracts all argument fields correctly
        // → Tool receives complete argument structure

        // Arrange
        var parser = new Phi4WeatherAgent.Agent.Parsing.FunctoolsParser();
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry);

        var forecastTool = new ToolDescriptor
        {
            Name = "GetForecast",
            Source = "Local:WeatherTools",
            ArgsSchema = null,
            Invoker = async (args) =>
            {
                var location = args.GetProperty("location").GetString();
                var days = args.GetProperty("options").GetProperty("days").GetInt32();
                var units = args.GetProperty("options").GetProperty("units").GetString();
                return new ToolResult
                {
                    Name = "GetForecast",
                    Content = $"{days}-day forecast for {location} in {units}",
                    Duration = TimeSpan.FromMilliseconds(100)
                };
            },
            SecurityClass = Phi4WeatherAgent.Tools.SecurityClass.Public,
            Timeout = TimeSpan.FromSeconds(10)
        };
        registry.Register(forecastTool);

        var modelResponse = "functools[{\"name\":\"GetForecast\",\"arguments\":{\"location\":\"Seattle\",\"options\":{\"days\":7,\"units\":\"metric\"}}}]";

        // Act
        var calls = parser.Parse(modelResponse);
        var result = await invoker.InvokeAsync(calls[0].Name, calls[0].Arguments, CancellationToken.None);

        // Assert
        result.Error.Should().BeNull();
        result.Content.Should().Contain("7-day");
        result.Content.Should().Contain("Seattle");
        result.Content.Should().Contain("metric");
    }
}
