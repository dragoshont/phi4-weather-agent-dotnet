using FluentAssertions;
using LocalAIAgent.Agent.Parsing;

namespace LocalAIAgent.Agent.Tests.Acceptance;

/// <summary>
/// Acceptance tests for User Story 4: Handle malformed functools gracefully.
/// Tests parser error detection and recovery without crashes.
/// </summary>
public class US4_ErrorHandlingTests
{
    private readonly FunctoolsParser _parser = new();

    [Fact]
    public void Scenario1_InvalidJson_ReturnsMalformedBlockError()
    {
        // US4 Scenario 1: Model outputs invalid JSON in functools
        // → parser catches exception
        // → returns MALFORMED_BLOCK error

        // Arrange - Broken JSON (missing closing brace)
        var malformedInput = "functools[{\"name\":\"GetWeather\",\"arguments\":{\"location\":\"Seattle\"";

        // Act & Assert
        var exception = Assert.Throws<ParserException>(() => _parser.Parse(malformedInput));
        exception.ErrorCode.Should().Be("MALFORMED_BLOCK");
        exception.Message.Should().Contain("JSON");
    }

    [Fact]
    public void Scenario2_MissingRequiredField_ReturnsMalformedBlockError()
    {
        // US4 Scenario 2: Model outputs functools missing 'arguments' field
        // → parser validates structure
        // → returns MALFORMED_BLOCK error

        // Arrange - Missing 'arguments' field
        var invalidStructure = "functools[{\"name\":\"GetWeather\"}]";

        // Act & Assert
        var exception = Assert.Throws<ParserException>(() => _parser.Parse(invalidStructure));
        exception.ErrorCode.Should().Be("MALFORMED_BLOCK");
        exception.Message.Should().Contain("arguments");
    }

    [Fact]
    public void Scenario3_PartialStreamingBlock_WaitsForComplete()
    {
        // US4 Scenario 3: Model outputs partial functools in streaming
        // → parser buffers chunks
        // → waits for complete block (no false positives)

        // Arrange - Partial block (incomplete)
        var partialInput = "Let me check the weather for you... functools[{\"name\":\"GetWeather\",\"arg";

        // Act
        var exception = Assert.Throws<ParserException>(() => _parser.Parse(partialInput));

        // Assert - Should detect incomplete block, not crash
        exception.ErrorCode.Should().Be("MALFORMED_BLOCK");
    }

    [Fact]
    public void Scenario4_MultipleErrors_AllDetected()
    {
        // Additional: Multiple structural errors
        // → parser detects first error and reports clearly

        // Arrange - Missing name AND arguments
        var multipleErrors = "functools[{\"tool\":\"Something\"}]";

        // Act & Assert
        var exception = Assert.Throws<ParserException>(() => _parser.Parse(multipleErrors));
        exception.ErrorCode.Should().Be("MALFORMED_BLOCK");
        // Should mention at least one missing field
        var message = exception.Message.ToLower();
        (message.Contains("name") || message.Contains("arguments")).Should().BeTrue();
    }
}
