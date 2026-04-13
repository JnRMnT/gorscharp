using System.Text.Json;
using GorSharp.Parser;

namespace GorSharp.LanguageServer.Services;

public sealed class ParsingModeService
{
    private GorSharpParsingOptions _current = new();

    public GorSharpParsingOptions Current => _current;

    public void UpdateFromInitializationOptions(object? initializationOptions)
    {
        if (initializationOptions is null)
        {
            _current = new GorSharpParsingOptions();
            return;
        }

        try
        {
            var json = initializationOptions.ToString();
            if (string.IsNullOrWhiteSpace(json))
            {
                _current = new GorSharpParsingOptions();
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("gorsharp", out var gorsharpNode))
            {
                _current = new GorSharpParsingOptions();
                return;
            }

            if (!gorsharpNode.TryGetProperty("parsingMode", out var parsingModeNode))
            {
                _current = new GorSharpParsingOptions();
                return;
            }

            var parsingMode = parsingModeNode.GetString();
            _current = parsingMode?.ToLowerInvariant() switch
            {
                "strict" => new GorSharpParsingOptions { NaturalMode = false },
                _ => new GorSharpParsingOptions { NaturalMode = true }
            };
        }
        catch
        {
            _current = new GorSharpParsingOptions();
        }
    }
}
