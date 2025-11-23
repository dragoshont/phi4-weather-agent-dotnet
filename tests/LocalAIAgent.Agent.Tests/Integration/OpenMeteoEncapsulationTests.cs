using System.Reflection;
using Xunit;

namespace LocalAIAgent.Agent.Tests.Integration;

/// <summary>
/// T093: Verifies that LocalAIAgent.OpenMeteo assembly does not expose
/// any openmeteo_sdk types in its public API surface.
/// Success Criteria (SC-011): Only tool method signatures visible, no SDK types leaked.
/// </summary>
public class OpenMeteoEncapsulationTests
{
    [Fact]
    public void OpenMeteoAssembly_ShouldNotExposeSDKTypes_InPublicAPI()
    {
        // Arrange: Load LocalAIAgent.OpenMeteo assembly
        var assemblyPath = GetOpenMeteoAssemblyPath();

        if (!File.Exists(assemblyPath))
        {
            // Skip test if assembly not built yet
            throw new SkipException($"OpenMeteo assembly not found at {assemblyPath}. Build solution first.");
        }

        var assembly = Assembly.LoadFrom(assemblyPath);

        // Act: Get all public types from the assembly
        var publicTypes = assembly.GetExportedTypes();

        // Assert: Verify no openmeteo_sdk types in public API
        var violations = new List<string>();

        foreach (var type in publicTypes)
        {
            // Check class itself
            if (IsSDKType(type))
            {
                violations.Add($"Public type exposes SDK: {type.FullName}");
            }

            // Check public methods
            var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var method in publicMethods)
            {
                // Check return type
                if (IsSDKType(method.ReturnType))
                {
                    violations.Add($"Method {type.Name}.{method.Name}() returns SDK type: {method.ReturnType.FullName}");
                }

                // Check parameters
                foreach (var param in method.GetParameters())
                {
                    if (IsSDKType(param.ParameterType))
                    {
                        violations.Add($"Method {type.Name}.{method.Name}() parameter '{param.Name}' uses SDK type: {param.ParameterType.FullName}");
                    }
                }
            }

            // Check public properties
            var publicProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var property in publicProperties)
            {
                if (IsSDKType(property.PropertyType))
                {
                    violations.Add($"Property {type.Name}.{property.Name} exposes SDK type: {property.PropertyType.FullName}");
                }
            }

            // Check public fields
            var publicFields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var field in publicFields)
            {
                if (IsSDKType(field.FieldType))
                {
                    violations.Add($"Field {type.Name}.{field.Name} exposes SDK type: {field.FieldType.FullName}");
                }
            }
        }

        // Report all violations
        if (violations.Any())
        {
            var violationReport = string.Join(Environment.NewLine, violations);
            Assert.Fail($"OpenMeteo assembly exposes {violations.Count} SDK type(s) in public API:" +
                       $"{Environment.NewLine}{violationReport}{Environment.NewLine}{Environment.NewLine}" +
                       "SDK types MUST remain internal. Public API should only expose primitives (string, int, double) or custom DTOs.");
        }
    }

    [Fact]
    public void OpenMeteoTools_ShouldOnlyReturnPrimitivesOrDTOs()
    {
        // Arrange: Load assembly and find tool classes
        var assemblyPath = GetOpenMeteoAssemblyPath();

        if (!File.Exists(assemblyPath))
        {
            throw new SkipException($"OpenMeteo assembly not found at {assemblyPath}");
        }

        var assembly = Assembly.LoadFrom(assemblyPath);
        var toolClasses = assembly.GetExportedTypes()
            .Where(t => t.Name.EndsWith("Tools", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(toolClasses); // Should have GeocodingTools, WeatherTools, AirQualityTools

        // Act & Assert: Verify tool methods return only approved types
        var approvedReturnTypes = new[]
        {
            typeof(string),
            typeof(int),
            typeof(double),
            typeof(float),
            typeof(bool),
            typeof(DateTime),
            typeof(Task<>), // Async primitives
            typeof(ValueTask<>),
            typeof(void)
        };

        foreach (var toolClass in toolClasses)
        {
            var publicMethods = toolClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.DeclaringType == toolClass); // Only methods declared in this class

            foreach (var method in publicMethods)
            {
                var returnType = method.ReturnType;

                // Unwrap Task<T> to check T
                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    returnType = returnType.GetGenericArguments()[0];
                }
                else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    returnType = returnType.GetGenericArguments()[0];
                }

                // Check if return type is approved
                var isApproved = returnType.IsPrimitive ||
                                returnType == typeof(string) ||
                                returnType == typeof(void) ||
                                returnType.Namespace?.StartsWith("LocalAIAgent.OpenMeteo") == true; // Allow custom DTOs

                if (!isApproved && IsSDKType(returnType))
                {
                    Assert.Fail($"Tool method {toolClass.Name}.{method.Name}() returns SDK type: {returnType.FullName}. " +
                               "Tool methods MUST return only primitives (string, int, double) or custom DTOs.");
                }
            }
        }
    }

    [Fact]
    public void OpenMeteoAssembly_ShouldHaveExpectedToolClasses()
    {
        // Arrange
        var assemblyPath = GetOpenMeteoAssemblyPath();

        if (!File.Exists(assemblyPath))
        {
            throw new SkipException($"OpenMeteo assembly not found at {assemblyPath}");
        }

        var assembly = Assembly.LoadFrom(assemblyPath);

        // Act: Find tool classes
        var exportedTypes = assembly.GetExportedTypes().Select(t => t.Name).ToList();

        // Assert: Verify expected tool classes exist
        Assert.Contains("GeocodingTools", exportedTypes);
        Assert.Contains("WeatherTools", exportedTypes);
        Assert.Contains("AirQualityTools", exportedTypes);
    }

    // Helper methods
    private static bool IsSDKType(Type type)
    {
        if (type == null) return false;

        // Check namespace for openmeteo_sdk patterns
        var ns = type.Namespace ?? "";

        // OpenMeteo SDK typically uses namespaces like:
        // - OpenMeteo
        // - Meteostat
        // - Or has "OpenMeteo" in the assembly name

        var isSDKNamespace = ns.Contains("OpenMeteo", StringComparison.OrdinalIgnoreCase) &&
                            !ns.StartsWith("LocalAIAgent", StringComparison.OrdinalIgnoreCase);

        var isSDKAssembly = type.Assembly.GetName().Name?.Contains("openmeteo", StringComparison.OrdinalIgnoreCase) == true;

        return isSDKNamespace || isSDKAssembly;
    }

    private static string GetOpenMeteoAssemblyPath()
    {
        // Determine path based on current test assembly location
        var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var testDir = Path.GetDirectoryName(testAssemblyPath)!;

        // Navigate from tests/LocalAIAgent.Agent.Tests/bin/{config}/{tfm}/
        // to same bin folder where OpenMeteo assembly should be
        return Path.Combine(testDir, "LocalAIAgent.OpenMeteo.dll");
    }

    // Custom exception for skipping tests
    private class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}
