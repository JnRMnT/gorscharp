using System.Text.Json;
using System.Text.Json.Serialization;

namespace GorSharp.Core.Sozluk;

/// <summary>
/// Loads and provides access to sozluk.json — the single source of truth for all keyword/type/operator mappings.
/// </summary>
public class SozlukService
{
    private SozlukData? _data;

    public SozlukData Data => _data ?? throw new InvalidOperationException("Sözlük yüklenmedi. Önce Load() çağırın.");

    public void Load(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        _data = JsonSerializer.Deserialize<SozlukData>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        }) ?? throw new InvalidOperationException($"Sözlük dosyası okunamadı: {jsonPath}");

        NormalizeSuffixMappings(_data);
    }

    public void LoadFromJson(string json)
    {
        _data = JsonSerializer.Deserialize<SozlukData>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        }) ?? throw new InvalidOperationException("Sözlük JSON verisi okunamadı.");

        NormalizeSuffixMappings(_data);
    }

    private static void NormalizeSuffixMappings(SozlukData data)
    {
        foreach (var entry in data.Suffixes.Values)
        {
            if (entry.VerbMappings.Count == 0)
                continue;

            foreach (var (verb, mapping) in entry.VerbMappings)
            {
                if (entry.Verbs.ContainsKey(verb) || string.IsNullOrWhiteSpace(mapping.CSharp))
                    continue;

                var method = ExtractMethodName(mapping.CSharp);
                if (!string.IsNullOrWhiteSpace(method))
                {
                    entry.Verbs[verb] = method;
                }
            }
        }
    }

    private static string? ExtractMethodName(string csharpTemplate)
    {
        var dotIndex = csharpTemplate.IndexOf('.');
        var parenIndex = csharpTemplate.IndexOf('(');
        if (dotIndex < 0 || parenIndex <= dotIndex + 1)
            return null;

        return csharpTemplate[(dotIndex + 1)..parenIndex];
    }
}

public class SozlukData
{
    public string Version { get; set; } = "";
    public Dictionary<string, KeywordEntry> Keywords { get; set; } = new();
    public Dictionary<string, TypeEntry> Types { get; set; } = new();
    public Dictionary<string, LiteralEntry> Literals { get; set; } = new();
    public Dictionary<string, OperatorEntry> Operators { get; set; } = new();
    public Dictionary<string, AccessModifierEntry> AccessModifiers { get; set; } = new();
    public Dictionary<string, SuffixEntry> Suffixes { get; set; } = new();
}

public class KeywordEntry
{
    public string CSharp { get; set; } = "";
    public string Category { get; set; } = "";
    public TooltipData? Tooltip { get; set; }
}

public class TypeEntry
{
    public string CSharp { get; set; } = "";
    public string? Generic { get; set; }
    public TooltipData? Tooltip { get; set; }
}

public class LiteralEntry
{
    public string CSharp { get; set; } = "";
    public string Type { get; set; } = "";
}

public class OperatorEntry
{
    public string CSharp { get; set; } = "";
    public string Category { get; set; } = "";
    public TooltipData? Tooltip { get; set; }
}

public class AccessModifierEntry
{
    public string CSharp { get; set; } = "";
}

public class SuffixEntry
{
    public string Name { get; set; } = "";
    public List<string> Markers { get; set; } = new();
    public Dictionary<string, string> Verbs { get; set; } = new();
    public string Case { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, SuffixMappingEntry> VerbMappings { get; set; } = new();
    public Dictionary<string, SuffixMappingEntry> PropertyMappings { get; set; } = new();

    public bool TryResolveVerbMethodName(string verb, out string? methodName)
    {
        methodName = null;

        if (Verbs.TryGetValue(verb, out var legacyMethod)
            && TryExtractMethodName(legacyMethod, out var parsedLegacyMethod))
        {
            methodName = parsedLegacyMethod;
            return true;
        }

        if (VerbMappings.TryGetValue(verb, out var mapping)
            && TryExtractMethodName(mapping.CSharp, out var parsedMethod))
        {
            methodName = parsedMethod;
            return true;
        }

        return false;
    }

    public bool TryResolvePropertyMemberName(string propertyWord, out string? memberName)
    {
        memberName = null;

        if (PropertyMappings.TryGetValue(propertyWord, out var mapping)
            && TryExtractPropertyName(mapping.CSharp, out var parsedMember))
        {
            memberName = parsedMember;
            return true;
        }

        return false;
    }

    private static bool TryExtractMethodName(string mapping, out string? methodName)
    {
        methodName = null;
        if (string.IsNullOrWhiteSpace(mapping))
            return false;

        // Handles template values like ".Add({arg})"
        var dotIndex = mapping.IndexOf('.');
        var parenIndex = mapping.IndexOf('(');
        if (dotIndex >= 0 && parenIndex > dotIndex + 1)
        {
            methodName = mapping[(dotIndex + 1)..parenIndex].Trim();
            return methodName.Length > 0;
        }

        // Handles legacy values like "Add"
        if (mapping.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            methodName = mapping;
            return true;
        }

        return false;
    }

    private static bool TryExtractPropertyName(string mapping, out string? memberName)
    {
        memberName = null;
        if (string.IsNullOrWhiteSpace(mapping))
            return false;

        if (!mapping.StartsWith(".", StringComparison.Ordinal))
            return false;

        // Handles values like ".Count", ".Length", ".GetType()"
        var content = mapping[1..];
        var parenIndex = content.IndexOf('(');
        if (parenIndex >= 0)
            content = content[..parenIndex];

        content = content.Trim();
        if (content.Length == 0)
            return false;

        memberName = content;
        return true;
    }
}

public class SuffixMappingEntry
{
    public string CSharp { get; set; } = "";
    public string Description { get; set; } = "";
}

public class TooltipData
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public TooltipExample? Example { get; set; }
}

public class TooltipExample
{
    [JsonPropertyName("gör")]
    public string Gor { get; set; } = "";
    public string CSharp { get; set; } = "";
}
