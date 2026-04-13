using GorSharp.Core.Sozluk;
using System.Text.RegularExpressions;
using ZemberekDotNet.Morphology.Morphotactics;

namespace GorSharp.Morphology;

/// <summary>
/// Result of resolving a Turkish suffix on an identifier.
/// </summary>
public record SuffixResolution(
    string Stem,
    string SuffixCase,
    string? CSharpMethod);

/// <summary>
/// Result of resolving a suffix-based property access token pair.
/// Example: "liste'nin uzunluğu" -> stem "liste", case "genitive", member "Count"
/// </summary>
public record SuffixPropertyResolution(
    string Stem,
    string SuffixCase,
    string? CSharpMember);

/// <summary>
/// Result of resolving an ablative numeric comparison token.
/// Example: "90'dan" -> value 90, case "ablative"
/// </summary>
public record AblativeNumberResolution(
    int Value,
    string SuffixCase);

/// <summary>
/// Resolves Turkish suffixes on identifiers using ZemberekDotNet morphology
/// and sozluk.json suffix mappings.
///
/// Example: "liste'ye" + verb "ekle" → stem "liste", case "dative", C# method "Add"
/// </summary>
public class SuffixResolver
{
    private static readonly Regex AblativeNumberRegex = new(
        @"^(?<value>\d+)'(?<suffix>d[ae]n|t[ae]n)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly SozlukData _sozluk;
    private readonly Lazy<ZemberekDotNet.Morphology.TurkishMorphology> _morphology;
    private readonly Dictionary<string, List<string>> _fallbackMarkers;

    public SuffixResolver(SozlukData sozluk)
    {
        _sozluk = sozluk;
        _morphology = new Lazy<ZemberekDotNet.Morphology.TurkishMorphology>(
            () => ZemberekDotNet.Morphology.TurkishMorphology.CreateWithDefaults());
        _fallbackMarkers = BuildFallbackMarkers(_sozluk);
    }

    public bool HasConfiguredSuffixMappings => _sozluk.Suffixes.Count > 0;

    /// <summary>
    /// Analyzes a suffixed word (e.g., "listeye" or "liste'ye") and returns the detected case.
    /// </summary>
    public string? DetectCase(string word)
    {
        // Strip apostrophe for analysis
        var normalized = word.Replace("'", "").Replace("\u2019", "");
        var morphCase = DetectCaseWithMorphology(normalized);
        if (!string.IsNullOrWhiteSpace(morphCase))
            return morphCase;

        return DetectCaseFromMarkers(word);
    }

    /// <summary>
    /// Resolves a suffixed word + verb into a C# method call.
    /// Example: ("liste'ye", "ekle") → SuffixResolution("liste", "dative", "Add")
    /// </summary>
    public SuffixResolution? Resolve(string suffixedWord, string verb)
    {
        var suffixCase = DetectCase(suffixedWord);
        if (suffixCase is null)
            return null;

        // Extract stem (text before the suffix)
        var stem = ExtractStem(suffixedWord);

        // Look up verb → C# method in sozluk suffix mappings
        if (_sozluk.Suffixes.TryGetValue(suffixCase, out var suffixEntry)
            && suffixEntry.TryResolveVerbMethodName(verb, out var csharpMethod)
            && !string.IsNullOrWhiteSpace(csharpMethod))
        {
            return new SuffixResolution(stem, suffixCase, csharpMethod);
        }

        return new SuffixResolution(stem, suffixCase, null);
    }

    /// <summary>
    /// Resolves a verb mapping when suffix case is already known.
    /// </summary>
    public string? ResolveVerbMethodFromCase(string suffixCase, string verb)
    {
        if (_sozluk.Suffixes.TryGetValue(suffixCase, out var suffixEntry)
            && suffixEntry.TryResolveVerbMethodName(verb, out var csharpMethod)
            && !string.IsNullOrWhiteSpace(csharpMethod))
        {
            return csharpMethod;
        }

        return null;
    }

    /// <summary>
    /// Resolves suffixed word + property token into a C# member access.
    /// Example: ("liste'nin", "uzunluğu") -> SuffixPropertyResolution("liste", "genitive", "Count")
    /// </summary>
    public SuffixPropertyResolution? ResolveProperty(string suffixedWord, string propertyWord)
    {
        var suffixCase = DetectCase(suffixedWord);
        if (suffixCase is null)
            return null;

        var stem = ExtractStem(suffixedWord);

        if (_sozluk.Suffixes.TryGetValue(suffixCase, out var suffixEntry)
            && suffixEntry.TryResolvePropertyMemberName(propertyWord, out var csharpMember)
            && !string.IsNullOrWhiteSpace(csharpMember))
        {
            return new SuffixPropertyResolution(stem, suffixCase, csharpMember);
        }

        return new SuffixPropertyResolution(stem, suffixCase, null);
    }

    /// <summary>
    /// Resolves a property member when suffix case is already known.
    /// </summary>
    public string? ResolvePropertyMemberFromCase(string suffixCase, string propertyWord)
    {
        if (_sozluk.Suffixes.TryGetValue(suffixCase, out var suffixEntry)
            && suffixEntry.TryResolvePropertyMemberName(propertyWord, out var csharpMember)
            && !string.IsNullOrWhiteSpace(csharpMember))
        {
            return csharpMember;
        }

        return null;
    }

    /// <summary>
    /// Resolves a property member by scanning all configured suffix mappings.
    /// Useful for chain tails like "... sonra uzunluğu yazdır" where an explicit case marker is omitted.
    /// </summary>
    public string? ResolvePropertyMember(string propertyWord)
    {
        foreach (var suffixEntry in _sozluk.Suffixes.Values)
        {
            if (suffixEntry.TryResolvePropertyMemberName(propertyWord, out var csharpMember)
                && !string.IsNullOrWhiteSpace(csharpMember))
            {
                return csharpMember;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves tokens like "90'dan" for natural-language comparisons.
    /// This is implemented in morphology to keep case logic centralized.
    /// </summary>
    public AblativeNumberResolution? DetectAblativeNumber(string token)
    {
        var normalized = token.Replace("\u2019", "'");
        var match = AblativeNumberRegex.Match(normalized);
        if (!match.Success)
            return null;

        var suffix = match.Groups["suffix"].Value.ToLowerInvariant();
        if (suffix is not ("dan" or "den" or "tan" or "ten"))
            return null;

        if (!int.TryParse(match.Groups["value"].Value, out var value))
            return null;

        // Enforce morphology runtime: validate that the suffix is recognized as ablative.
        var probeWord = suffix switch
        {
            "dan" => "araba'dan",
            "den" => "ev'den",
            "tan" => "kitap'tan",
            "ten" => "ağaç'tan",
            _ => throw new InvalidOperationException($"Desteklenmeyen ayrılma eki: {suffix}")
        };

        string? probeCase;
        try
        {
            probeCase = DetectCase(probeWord);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Zemberek doğrulaması başarısız. Antlr4/Zemberek paket sürümlerini uyumlu hale getirip tekrar deneyin.",
                ex);
        }

        if (!string.Equals(probeCase, "ablative", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Zemberek ayrılma durumunu doğrulayamadı. Zemberek paket sürümü veya morfoloji yapılandırmasını kontrol edin.");

        return new AblativeNumberResolution(value, "ablative");
    }

    /// <summary>
    /// Extracts the stem from a suffixed word using ZemberekDotNet analysis.
    /// Falls back to apostrophe-based splitting.
    /// </summary>
    private string ExtractStem(string word)
    {
        // If apostrophe is present, split there
        var apostropheIdx = word.IndexOfAny(['\'', '\u2019']);
        if (apostropheIdx > 0)
            return word[..apostropheIdx];

        // Use ZemberekDotNet to find the stem
        var normalized = word.Replace("'", "").Replace("\u2019", "");
        var analysis = _morphology.Value.Analyze(normalized);
        if (analysis.AnalysisCount() > 0)
        {
            var first = analysis.GetAnalysisResults()[0];
            return first.GetStem();
        }

        return word;
    }

    private string? DetectCaseWithMorphology(string normalizedWord)
    {
        try
        {
            var analysis = _morphology.Value.Analyze(normalizedWord);
            if (analysis.AnalysisCount() == 0)
                return null;

            foreach (var result in analysis)
            {
                if (result.ContainsMorpheme(TurkishMorphotactics.dat))
                    return "dative";
                if (result.ContainsMorpheme(TurkishMorphotactics.abl))
                    return "ablative";
                if (result.ContainsMorpheme(TurkishMorphotactics.gen))
                    return "genitive";
                if (result.ContainsMorpheme(TurkishMorphotactics.loc))
                    return "locative";
                if (result.ContainsMorpheme(TurkishMorphotactics.acc))
                    return "accusative";
            }
        }
        catch
        {
            // Fall back to marker-based detection when morphology resources are unavailable.
        }

        return null;
    }

    private string? DetectCaseFromMarkers(string originalWord)
    {
        var normalized = originalWord.Replace("\u2019", "'").ToLowerInvariant();
        var apostropheIdx = normalized.LastIndexOf('\'');
        var suffix = apostropheIdx >= 0 ? normalized[(apostropheIdx + 1)..] : normalized;

        foreach (var (caseName, markers) in _fallbackMarkers)
        {
            foreach (var marker in markers)
            {
                if (apostropheIdx >= 0)
                {
                    if (suffix == marker)
                        return caseName;
                }
                else
                {
                    // Avoid excessive false positives for one-letter markers when apostrophe is absent.
                    if (marker.Length < 2)
                        continue;

                    if (normalized.EndsWith(marker, StringComparison.Ordinal))
                        return caseName;
                }
            }
        }

        return null;
    }

    private static Dictionary<string, List<string>> BuildFallbackMarkers(SozlukData sozluk)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (caseName, entry) in sozluk.Suffixes)
        {
            if (entry.Markers.Count == 0)
                continue;

            var normalized = entry.Markers
                .Select(m => m.Replace("\u2019", "'").Trim().TrimStart('\''))
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(m => m.Length)
                .ToList();

            if (normalized.Count > 0)
                result[caseName] = normalized;
        }

        if (result.Count > 0)
            return result;

        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dative"] = ["ye", "ya", "e", "a"],
            ["ablative"] = ["den", "dan", "ten", "tan"],
            ["genitive"] = ["nin", "nın", "nun", "nün", "in", "ın", "un", "ün"],
            ["locative"] = ["de", "da", "te", "ta"],
            ["accusative"] = ["yi", "yı", "yu", "yü", "i", "ı", "u", "ü"]
        };
    }
}
