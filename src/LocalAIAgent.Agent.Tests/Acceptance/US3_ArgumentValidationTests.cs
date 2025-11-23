using System.Text.Json;
using FluentAssertions;
using LocalAIAgent.Agent.Parsing;
using LocalAIAgent.Agent.Registry;
using LocalAIAgent.Agent.Dispatching;
using Json.Schema;

namespace LocalAIAgent.Agent.Tests.Acceptance;

/// <summary>
/// Acceptance tests for User Story 3: Validate tool arguments with JSON Schema.
/// Tests schema validation enforcement before tool invocation.
/// </summary>
public class US3_ArgumentValidationTests
{
    [Fact]
    public async Task Scenario1_MaxLengthViolation_RejectedWithValidationError()
    {
        // US3 Scenario 1: Tool declares maxLength:100
        // → functools provides 10,000 char string
        // → dispatcher rejects with ARG_VALIDATION_FAILED error

        // Arrange
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry, Microsoft.Extensions.Logging.Abstractions.NullLogger<ToolInvoker>.Instance);

        var schema = JsonSchema.FromText(@"{
            ""type"": ""object"",
            ""properties"": {
                ""location"": { ""type"": ""string"", ""maxLength"": 100 }
            },
            ""required"": [""location""]
        }");

        var tool = new ToolDescriptor
        {
            Name = "GetWeather",
            Source = "Local:WeatherTools",
            ArgsSchema = schema,
            Invoker = async (args) => new ToolResult { Name = "GetWeather", Content = "Should not execute" },
            SecurityClass = LocalAIAgent.Tools.SecurityClass.Public,
            Timeout = TimeSpan.FromSeconds(10)
        };
        registry.Register(tool);

        // 10,000 character location (violates maxLength)
        var longLocation = new string('x', 10000);
        var args = JsonDocument.Parse($"{{\"location\":\"{longLocation}\"}}").RootElement;

        // Act
        var result = await invoker.InvokeAsync("GetWeather", args, CancellationToken.None);

        // Assert
        result.Error.Should().StartWith("ARG_VALIDATION_FAILED:");
        result.Content.Should().BeNull();
    }

    [Fact]
    public async Task Scenario2_TypeMismatch_RejectedWithValidationError()
    {
        // US3 Scenario 2: Tool requires number type
        // → functools provides string
        // → dispatcher rejects with type mismatch error

        // Arrange
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry, Microsoft.Extensions.Logging.Abstractions.NullLogger<ToolInvoker>.Instance);

        var schema = JsonSchema.FromText(@"{
            ""type"": ""object"",
            ""properties"": {
                ""temperature"": { ""type"": ""number"" }
            },
            ""required"": [""temperature""]
        }");

        var tool = new ToolDescriptor
        {
            Name = "SetTemperature",
            Source = "Local:ControlTools",
            ArgsSchema = schema,
            Invoker = async (args) => new ToolResult { Name = "SetTemperature", Content = "Should not execute" },
            SecurityClass = LocalAIAgent.Tools.SecurityClass.Public,
            Timeout = TimeSpan.FromSeconds(10)
        };
        registry.Register(tool);

        // String instead of number
        var args = JsonDocument.Parse("{\"temperature\":\"twenty-two\"}").RootElement;

        // Act
        var result = await invoker.InvokeAsync("SetTemperature", args, CancellationToken.None);

        // Assert
        result.Error.Should().StartWith("ARG_VALIDATION_FAILED:");
        result.Content.Should().BeNull();
    }

    [Fact]
    public async Task Scenario3_NoSchema_SkipsValidation()
    {
        // US3 Scenario 3: Tool has no schema
        // → functools provides any arguments
        // → dispatcher skips validation (opt-in validation)

        // Arrange
        var registry = new ToolRegistry();
        var invoker = new ToolInvoker(registry, Microsoft.Extensions.Logging.Abstractions.NullLogger<ToolInvoker>.Instance);

        var tool = new ToolDescriptor
        {
            Name = "FlexibleTool",
            Source = "Local:UtilityTools",
            ArgsSchema = null, // No schema = no validation
            Invoker = async (args) => new ToolResult
            {
                Name = "FlexibleTool",
                Content = "Executed with any arguments",
                Duration = TimeSpan.FromMilliseconds(5)
            },
            SecurityClass = LocalAIAgent.Tools.SecurityClass.Public,
            Timeout = TimeSpan.FromSeconds(10)
        };
        registry.Register(tool);

        // Any arguments (even unusual ones)
        var args = JsonDocument.Parse("{\"weird\":true,\"nested\":{\"data\":[1,2,3]}}").RootElement;

        // Act
        var result = await invoker.InvokeAsync("FlexibleTool", args, CancellationToken.None);

        // Assert
        result.Error.Should().BeNull();
        result.Content.Should().Be("Executed with any arguments");
    }
}
