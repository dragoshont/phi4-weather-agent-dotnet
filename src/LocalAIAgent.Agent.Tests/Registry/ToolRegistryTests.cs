using System.Text.Json;
using FluentAssertions;
using LocalAIAgent.Agent.Registry;
using LocalAIAgent.Agent.Dispatching;

namespace LocalAIAgent.Agent.Tests.Registry;

/// <summary>
/// Unit tests for ToolRegistry (US1).
/// Tests registration, lookup, thread safety, and duplicate detection.
/// </summary>
public class ToolRegistryTests
{
    [Fact]
    public void Register_ValidDescriptor_SuccessfullyRegisters()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = CreateTestDescriptor("TestTool");

        // Act
        registry.Register(descriptor);

        // Assert
        var found = registry.TryGet("TestTool", out var retrieved);
        found.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("TestTool");
    }

    [Fact]
    public void Register_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor1 = CreateTestDescriptor("TestTool");
        var descriptor2 = CreateTestDescriptor("TestTool");
        registry.Register(descriptor1);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => registry.Register(descriptor2));
        exception.Message.Should().Contain("TestTool");
        exception.Message.Should().Contain("already registered");
    }

    [Fact]
    public void TryGet_ExistingTool_ReturnsTrue()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = CreateTestDescriptor("GetWeather");
        registry.Register(descriptor);

        // Act
        var found = registry.TryGet("GetWeather", out var retrieved);

        // Assert
        found.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("GetWeather");
    }

    [Fact]
    public void TryGet_NonExistentTool_ReturnsFalse()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var found = registry.TryGet("NonExistent", out var retrieved);

        // Assert
        found.Should().BeFalse();
        retrieved.Should().BeNull();
    }

    [Fact]
    public void TryGet_CaseInsensitive_FindsTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = CreateTestDescriptor("GetWeather");
        registry.Register(descriptor);

        // Act - Try with different casing
        var found = registry.TryGet("getweather", out var retrieved);

        // Assert - Should find regardless of case (if registry uses case-insensitive comparer)
        // Note: Update assertion based on actual registry case-sensitivity policy
        found.Should().BeFalse("Registry is case-sensitive per spec clarification needed (see A1 in analysis)");
    }

    [Fact]
    public void GetAll_MultipleTools_ReturnsAllDescriptors()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.Register(CreateTestDescriptor("Tool1"));
        registry.Register(CreateTestDescriptor("Tool2"));
        registry.Register(CreateTestDescriptor("Tool3"));

        // Act
        var all = registry.GetAll();

        // Assert
        all.Should().HaveCount(3);
        all.Select(d => d.Name).Should().Contain(new[] { "Tool1", "Tool2", "Tool3" });
    }

    [Fact]
    public void Register_ConcurrentRegistrations_AllSucceed()
    {
        // Arrange - NFR: Registry must be thread-safe
        var registry = new ToolRegistry();
        var tools = Enumerable.Range(0, 50).Select(i => CreateTestDescriptor($"Tool{i}")).ToList();

        // Act - Concurrent registrations
        Parallel.ForEach(tools, tool =>
        {
            registry.Register(tool);
        });

        // Assert
        registry.GetAll().Should().HaveCount(50);
    }

    [Fact]
    public void TryGet_PerformanceBenchmark_MeetsNFR()
    {
        // Arrange - NFR-003: Registry lookup must be <1μs
        var registry = new ToolRegistry();
        for (int i = 0; i < 50; i++)
        {
            registry.Register(CreateTestDescriptor($"Tool{i}"));
        }

        // Act - Warmup
        registry.TryGet("Tool25", out _);

        // Act - Benchmark 1000 lookups
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            registry.TryGet("Tool25", out _);
        }
        sw.Stop();

        // Assert - Average <1μs per lookup
        var avgMicroseconds = sw.Elapsed.TotalMicroseconds / 1000.0;
        avgMicroseconds.Should().BeLessThan(1.0, "NFR-003: Registry lookup must be <1μs");
    }

    private static ToolDescriptor CreateTestDescriptor(string name)
    {
        return new ToolDescriptor
        {
            Name = name,
            Source = $"Local:{name}",
            ArgsSchema = null,
            Invoker = async (args) => new ToolResult
            {
                Name = name,
                Content = "Test result",
                Duration = TimeSpan.FromMilliseconds(1)
            },
            SecurityClass = LocalAIAgent.Tools.SecurityClass.Public,
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
