using System.Text.Json;
using FluentAssertions;
using Phi4WeatherAgent.Agent.Parsing;

namespace Phi4WeatherAgent.Agent.Tests.Parsing;

/// <summary>
/// Unit tests for FunctoolsParser (US1, US4).
/// Tests streaming state machine, JSON parsing, and error handling.
/// </summary>
public class FunctoolsParserTests
{
    private readonly FunctoolsParser _parser = new();

    [Fact]
    public void Parse_ValidSingleFunctionCall_ReturnsParsedCall()
    {
        // Arrange
        var input = "Here is the weather: functools[{\"name\":\"GetWeather\",\"arguments\":{\"location\":\"Seattle\"}}]";

        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result[0].Name.Should().Be("GetWeather");
        result[0].Arguments.GetProperty("location").GetString().Should().Be("Seattle");
    }

    [Fact]
    public void Parse_ValidMultipleFunctionCalls_ReturnsAllCalls()
    {
        // Arrange
        var input = "functools[{\"name\":\"GetWeather\",\"arguments\":{\"location\":\"Seattle\"}},{\"name\":\"GetForecast\",\"arguments\":{\"location\":\"Portland\",\"days\":7}}]";

        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("GetWeather");
        result[1].Name.Should().Be("GetForecast");
        result[1].Arguments.GetProperty("days").GetInt32().Should().Be(7);
    }

    [Fact]
    public void Parse_EmptyArgumentsObject_ParsesSuccessfully()
    {
        // Arrange
        var input = "functools[{\"name\":\"GetCurrentTime\",\"arguments\":{}}]";

        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("GetCurrentTime");
        result[0].Arguments.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void Parse_NoFunctoolsBlock_ReturnsEmptyArray()
    {
        // Arrange
        var input = "This is a normal response without any tool calls.";

        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsParserException()
    {
        // Arrange - Missing closing brace
        var input = "functools[{\"name\":\"GetWeather\",\"arguments\":{\"location\":\"Seattle\"";

        // Act & Assert
        var exception = Assert.Throws<ParserException>(() => _parser.Parse(input));
        exception.ErrorCode.Should().Be("MALFORMED_BLOCK");
        exception.Message.Should().Contain("JSON");
    }

    [Fact]
    public void Parse_MissingNameField_ThrowsParserException()
    {
        // Arrange - Missing required 'name' field
        var input = "functools[{\"arguments\":{\"location\":\"Seattle\"}}]";

        // Act & Assert
        var exception = Assert.Throws<ParserException>(() => _parser.Parse(input));
        exception.ErrorCode.Should().Be("MALFORMED_BLOCK");
        exception.Message.Should().Contain("name");
    }

    [Fact]
    public void Parse_MissingArgumentsField_ThrowsParserException()
    {
        // Arrange - Missing required 'arguments' field
        var input = "functools[{\"name\":\"GetWeather\"}]";

        // Act & Assert
        var exception = Assert.Throws<ParserException>(() => _parser.Parse(input));
        exception.ErrorCode.Should().Be("MALFORMED_BLOCK");
        exception.Message.Should().Contain("arguments");
    }

    [Fact]
    public void Parse_NestedFunctoolsBlocks_ParsesOuterBlockOnly()
    {
        // Arrange - Nested functools (edge case)
        var input = "functools[{\"name\":\"GetWeather\",\"arguments\":{\"location\":\"Seattle\",\"note\":\"functools[inner]\"}}]";

        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("GetWeather");
        result[0].Arguments.GetProperty("note").GetString().Should().Be("functools[inner]");
    }

    [Fact]
    public void Parse_LargeFunctoolsBlock_HandlesEfficiently()
    {
        // Arrange - 1MB functools block (NFR-001: parser must handle <50ms)
        var largeArgs = string.Join(",", Enumerable.Range(0, 10000).Select(i => $"\"key{i}\":\"value{i}\""));
        var input = $"functools[{{\"name\":\"LargeTool\",\"arguments\":{{{largeArgs}}}}}]";

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = _parser.Parse(input);
        sw.Stop();

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("LargeTool");
        sw.ElapsedMilliseconds.Should().BeLessThan(50, "NFR-001: parser must process <50ms");
    }

    [Fact]
    public void Parse_SpecialCharactersInArguments_ParsesCorrectly()
    {
        // Arrange - Special characters, Unicode, escape sequences
        var input = "functools[{\"name\":\"SendMessage\",\"arguments\":{\"text\":\"Hello\\nWorld\\t🌍\"}}]";

        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().ContainSingle();
        result[0].Arguments.GetProperty("text").GetString().Should().Be("Hello\nWorld\t🌍");
    }
}
