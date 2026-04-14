using System.Text.RegularExpressions;

namespace GorSharp.LanguageServer.Services;

public sealed class GeneratedCSharpSourceMap
{
    private static readonly Regex MarkerRegex = new(
        @"/\*\s*gör:(?<line>\d+)\s*\*/",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<Entry> _entries;
    private readonly string[] _lines;

    private GeneratedCSharpSourceMap(IReadOnlyList<Entry> entries, string[] lines)
    {
        _entries = entries;
        _lines = lines;
    }

    public static GeneratedCSharpSourceMap Create(string csharpCode)
    {
        var normalized = csharpCode.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var entries = new List<Entry>();
        var previousMarkerLine = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            var match = MarkerRegex.Match(lines[index]);
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups["line"].Value, out var gorLine))
                continue;

            var generatedLine = index + 1;
            entries.Add(new Entry(previousMarkerLine + 1, generatedLine, gorLine));
            previousMarkerLine = generatedLine;
        }

        return new GeneratedCSharpSourceMap(entries, lines);
    }

    public bool TryMap(int generatedLine, out int gorLine)
    {
        foreach (var entry in _entries)
        {
            if (generatedLine < entry.StartLine || generatedLine > entry.EndLine)
                continue;

            gorLine = entry.GorLine;
            return true;
        }

        gorLine = 1;
        return false;
    }

    public string? GetGeneratedLineSnippet(int generatedLine)
    {
        if (generatedLine < 1 || generatedLine > _lines.Length)
            return null;

        var line = MarkerRegex.Replace(_lines[generatedLine - 1], string.Empty).Trim();
        return string.IsNullOrWhiteSpace(line) ? null : line;
    }

    private sealed record Entry(int StartLine, int EndLine, int GorLine);
}