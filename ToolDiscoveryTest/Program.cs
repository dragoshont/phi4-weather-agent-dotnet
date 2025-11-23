using LocalAIAgent.Agent.Registry;
using LocalAIAgent.Tools;
using System.Reflection;

// Simulate tool discovery like ToolDiscoveryService does
Console.WriteLine("=== Tool Discovery Verification ===\n");

var toolsAssembly = typeof(WeatherTools).Assembly;
Console.WriteLine($"Tools Assembly: {toolsAssembly.FullName}\n");

var toolCount = 0;
var types = toolsAssembly.GetTypes();

Console.WriteLine($"Scanning {types.Length} types...\n");

foreach (var type in types)
{
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
    
    foreach (var method in methods)
    {
        var attr = method.GetCustomAttribute<ToolAttribute>();
        
        if (attr != null)
        {
            toolCount++;
            Console.WriteLine($"? Found Tool: {attr.Name}");
            Console.WriteLine($"  Type: {type.Name}");
            Console.WriteLine($"  Method: {method.Name}");
            Console.WriteLine($"  Description: {attr.Description ?? "(none)"}");
            Console.WriteLine($"  SecurityClass: {attr.SecurityClass}");
            Console.WriteLine($"  Timeout: {attr.TimeoutSeconds}s");
            Console.WriteLine();
        }
    }
}

Console.WriteLine($"\n=== Summary ===");
Console.WriteLine($"Total tools discovered: {toolCount}");

if (toolCount == 0)
{
    Console.WriteLine("\n? FAILED: No tools found!");
    Environment.Exit(1);
}
else
{
    Console.WriteLine($"\n? SUCCESS: Found {toolCount} tool(s)");
    Environment.Exit(0);
}
