using System.Reflection;

var serverDll = @"c:\Personal\Genel\Projeler\Gör#\src\GorSharp.LanguageServer\bin\Release\net10.0\GorSharp.LanguageServer.dll";

if (!File.Exists(serverDll))
{
    Console.Error.WriteLine($"DLL not found: {serverDll}");
    Environment.Exit(1);
}

Console.WriteLine("=== Gör# Language Server Capability Analysis ===\n");
Console.WriteLine($"Loading: {serverDll}");

try
{
    var assembly = Assembly.LoadFrom(serverDll);
    var types = assembly.GetTypes();

    // Find all handler types
    var handlerTypes = types
        .Where(t => !t.IsAbstract && t.Name.EndsWith("Handler"))
        .OrderBy(t => t.Name)
        .ToList();

    Console.WriteLine($"\n✓ Found {handlerTypes.Count} handler types:\n");

    var hasInlayHints = false;
    foreach (var handler in handlerTypes)
    {
        var baseTypes = handler.BaseType?.Name ?? "Unknown";
        var isInlayHints = handler.Name == "InlayHintsHandler";
        
        if (isInlayHints)
        {
            hasInlayHints = true;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  ★ ");
        }
        else
        {
            Console.Write("    ");
        }
        
        Console.ResetColor();
        Console.WriteLine($"{handler.Name,-35} (extends {baseTypes})");
    }

    Console.WriteLine($"\n{'='*60}");
    
    if (hasInlayHints)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ InlayHintsHandler IS REGISTERED");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("✗ InlayHintsHandler NOT FOUND - not registered as handler!");
        Console.ResetColor();
    }

    // Check if InlayHintsHandler is in Program.cs registration
    var programCs = @"c:\Personal\Genel\Projeler\Gör#\src\GorSharp.LanguageServer\Program.cs";
    if (File.Exists(programCs))
    {
        var content = File.ReadAllText(programCs);
        if (content.Contains("WithHandler<InlayHintsHandler>"))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ InlayHintsHandler IS REGISTERED in Program.cs");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ InlayHintsHandler NOT REGISTERED in Program.cs");
            Console.ResetColor();
        }
    }

    Console.WriteLine();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}
