using System.Reflection;
using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phi4WeatherAgent.Tools;

namespace Phi4WeatherAgent.Agent.Registry;

/// <summary>
/// Background service that discovers and registers tools at startup.
/// Scans assemblies for methods decorated with [Tool] attribute.
/// </summary>
public sealed class ToolDiscoveryService : BackgroundService
{
    private readonly IToolRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ToolDiscoveryService> _logger;

    public ToolDiscoveryService(
        IToolRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<ToolDiscoveryService> logger)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting tool discovery...");

        try
        {
            // Explicitly load Tools assembly to ensure it's available for scanning
            // This triggers static constructors and ensures types are loaded
            _ = typeof(WeatherTools).Assembly;
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var discoveredCount = 0;

            _logger.LogInformation("Scanning {Count} assemblies for tools", assemblies.Length);

            foreach (var assembly in assemblies)
            {
                // Skip system assemblies for performance
                if (assembly.FullName?.StartsWith("System") == true ||
                    assembly.FullName?.StartsWith("Microsoft") == true && 
                    !assembly.FullName.Contains("Phi4WeatherAgent"))
                {
                    continue;
                }

                _logger.LogInformation("Scanning assembly: {AssemblyName}", assembly.FullName);

                foreach (var type in GetLoadableTypes(assembly))
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                    {
                        var attr = method.GetCustomAttribute<ToolAttribute>();
                        if (attr == null) continue;

                        try
                        {
                            ValidateMethodSignature(method);
                            var descriptor = CreateDescriptor(attr, method, type);
                            _registry.Register(descriptor);
                            discoveredCount++;

                            _logger.LogInformation(
                                "Registered tool '{ToolName}' from {TypeName}.{MethodName}",
                                attr.Name, type.Name, method.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Failed to register tool '{ToolName}' from {TypeName}.{MethodName}",
                                attr.Name, type.Name, method.Name);
                        }
                    }
                }
            }

            _logger.LogInformation("Tool discovery complete. Registered {Count} tools.", discoveredCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool discovery failed");
            throw;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets loadable types from an assembly, handling ReflectionTypeLoadException.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    /// <summary>
    /// Validates that a method signature is compatible with tool invocation.
    /// </summary>
    private void ValidateMethodSignature(MethodInfo method)
    {
        // Must return string, Task<string>, or ValueTask<string>
        var returnType = method.ReturnType;
        var isValidReturn = returnType == typeof(string) ||
                           returnType == typeof(Task<string>) ||
                           returnType == typeof(ValueTask<string>);

        if (!isValidReturn)
        {
            throw new InvalidOperationException(
                $"Method {method.Name} must return string, Task<string>, or ValueTask<string>. " +
                $"Found: {returnType.Name}");
        }

        // Parameters must be deserializable from JsonElement
        var parameters = method.GetParameters();
        foreach (var param in parameters)
        {
            // Allow CancellationToken as last parameter
            if (param.ParameterType == typeof(CancellationToken))
            {
                if (param.Position != parameters.Length - 1)
                {
                    throw new InvalidOperationException(
                        $"CancellationToken must be the last parameter in {method.Name}");
                }
                continue;
            }

            // Check if parameter type is JSON-deserializable
            if (!IsJsonDeserializable(param.ParameterType))
            {
                throw new InvalidOperationException(
                    $"Parameter '{param.Name}' of type {param.ParameterType.Name} in method {method.Name} " +
                    "must be deserializable from JSON (primitive, string, POCO, or IEnumerable)");
            }
        }
    }

    /// <summary>
    /// Checks if a type can be deserialized from JSON.
    /// </summary>
    private static bool IsJsonDeserializable(Type type)
    {
        // Primitive types
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            return true;

        // Nullable types
        if (Nullable.GetUnderlyingType(type) != null)
            return true;

        // Collections
        if (type.IsAssignableTo(typeof(System.Collections.IEnumerable)))
            return true;

        // POCOs (classes with parameterless constructor)
        if (type.IsClass && type.GetConstructor(Type.EmptyTypes) != null)
            return true;

        // Records
        if (type.IsClass && type.GetMethod("<Clone>$") != null)
            return true;

        return false;
    }

    /// <summary>
    /// Creates a ToolDescriptor from attribute metadata and method info.
    /// </summary>
    private ToolDescriptor CreateDescriptor(ToolAttribute attr, MethodInfo method, Type declaringType)
    {
        // Parse JSON Schema if provided
        JsonSchema? schema = null;
        if (!string.IsNullOrWhiteSpace(attr.InputSchemaJson))
        {
            try
            {
                schema = JsonSchema.FromText(attr.InputSchemaJson);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Invalid JSON Schema in [Tool(\"{attr.Name}\")] attribute: {ex.Message}", ex);
            }
        }

        // Create invoker delegate
        var invoker = CreateInvoker(method, declaringType);

        return new ToolDescriptor
        {
            Name = attr.Name,
            Source = $"Local:{declaringType.FullName}",
            ArgsSchema = schema,
            Invoker = invoker,
            SecurityClass = attr.SecurityClass,
            Timeout = TimeSpan.FromSeconds(attr.TimeoutSeconds)
        };
    }

    /// <summary>
    /// Creates an async invoker delegate that wraps the discovered method.
    /// </summary>
    private Func<JsonElement, ValueTask<Dispatching.ToolResult>> CreateInvoker(MethodInfo method, Type declaringType)
    {
        return async (args) =>
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Create instance (or null for static methods)
                object? instance = null;
                if (!method.IsStatic)
                {
                    instance = ActivatorUtilities.CreateInstance(_serviceProvider, declaringType);
                }

                // Convert JsonElement args to method parameters
                var parameters = method.GetParameters();
                var paramValues = new object?[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];

                    // Handle CancellationToken
                    if (param.ParameterType == typeof(CancellationToken))
                    {
                        paramValues[i] = CancellationToken.None;
                        continue;
                    }

                    // Deserialize from JsonElement
                    if (args.TryGetProperty(param.Name!, out var argValue))
                    {
                        paramValues[i] = JsonSerializer.Deserialize(argValue.GetRawText(), param.ParameterType);
                    }
                    else if (param.HasDefaultValue)
                    {
                        paramValues[i] = param.DefaultValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Missing required parameter '{param.Name}'");
                    }
                }

                // Invoke method
                var result = method.Invoke(instance, paramValues);

                // Handle async return types
                string? content = null;
                if (result is Task<string> taskResult)
                {
                    content = await taskResult;
                }
                else if (result is ValueTask<string> valueTaskResult)
                {
                    content = await valueTaskResult;
                }
                else if (result is string stringResult)
                {
                    content = stringResult;
                }

                stopwatch.Stop();

                return Dispatching.ToolResult.Success(
                    method.GetCustomAttribute<ToolAttribute>()!.Name,
                    content ?? string.Empty,
                    stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Unwrap TargetInvocationException
                var innerEx = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;

                _logger.LogError(innerEx, "Tool invocation failed for {MethodName}", method.Name);

                return Dispatching.ToolResult.Failure(
                    method.GetCustomAttribute<ToolAttribute>()!.Name,
                    $"INVOCATION_FAILED: {innerEx.Message}",
                    stopwatch.Elapsed);
            }
        };
    }
}
