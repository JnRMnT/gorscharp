using System.Text;
using GorSharp.Core.Sozluk;
using GorSharp.Morphology;

namespace GorSharp.LanguageServer.Services;

public enum SuffixSuggestionKind
{
    Verb,
    Property,
}

public sealed record SuffixSuggestion(
    string Word,
    string CSharp,
    string? Description,
    SuffixSuggestionKind Kind);

public sealed record SuffixExplanation(
    string RawToken,
    string Stem,
    string SuffixCase,
    string CaseDisplayName,
    string Description,
    bool UsedMorphology,
    bool UsedFallbackMarkers,
    string? MorphologyFailureMessage,
    IReadOnlyList<SuffixSuggestion> Suggestions);

public sealed record SuffixExplanationMatch(
    SuffixExplanation Explanation,
    int Line,
    int StartColumn,
    int EndColumn);

public sealed record SuffixCompletionContext(
    SuffixExplanation Explanation,
    string PartialWord);

public sealed class SuffixExplanationService
{
    private readonly TranspilationService _transpiler;

    public SuffixExplanationService(TranspilationService transpiler)
    {
        _transpiler = transpiler;
    }

    public SuffixExplanation? ExplainToken(string token)
    {
        var sozluk = _transpiler.Sozluk;
        if (sozluk is null || string.IsNullOrWhiteSpace(token))
            return null;

        var resolver = new SuffixResolver(sozluk);
        var analysis = resolver.AnalyzeCase(token);
        if (string.IsNullOrWhiteSpace(analysis.SuffixCase))
            return null;

        if (!sozluk.Suffixes.TryGetValue(analysis.SuffixCase, out var suffixEntry))
            return null;

        var suggestions = new List<SuffixSuggestion>();

        foreach (var (word, mapping) in suffixEntry.VerbMappings.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            suggestions.Add(new SuffixSuggestion(word, mapping.CSharp, mapping.Description, SuffixSuggestionKind.Verb));
        }

        foreach (var (word, mapping) in suffixEntry.PropertyMappings.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            suggestions.Add(new SuffixSuggestion(word, mapping.CSharp, mapping.Description, SuffixSuggestionKind.Property));
        }

        return new SuffixExplanation(
            RawToken: token,
            Stem: ExtractStem(token, suffixEntry),
            SuffixCase: analysis.SuffixCase,
            CaseDisplayName: GetCaseDisplayName(analysis.SuffixCase),
            Description: suffixEntry.Description,
            UsedMorphology: analysis.UsedMorphology,
            UsedFallbackMarkers: analysis.UsedFallbackMarkers,
            MorphologyFailureMessage: analysis.MorphologyFailureMessage,
            Suggestions: suggestions);
    }

    public SuffixExplanationMatch? FindExplanationAtPosition(string text, int line, int character)
    {
        var lineText = GetLineText(text, line);
        if (lineText is null)
            return null;

        if (!TryFindTokenSpan(lineText, character, out var start, out var end))
            return null;

        var token = lineText[start..end];
        var explanation = ExplainToken(token);
        if (explanation is null)
            return null;

        return new SuffixExplanationMatch(explanation, line, start, end);
    }

    public SuffixCompletionContext? FindCompletionContext(string text, int line, int character)
    {
        var lineText = GetLineText(text, line);
        if (lineText is null)
            return null;

        var cursor = Math.Clamp(character, 0, lineText.Length);

        var partialStart = cursor;
        while (partialStart > 0 && IsWordChar(lineText[partialStart - 1]))
            partialStart--;

        var partialWord = lineText[partialStart..cursor];

        var separator = partialStart;
        while (separator > 0 && char.IsWhiteSpace(lineText[separator - 1]))
            separator--;

        if (separator == partialStart)
            return null;

        var targetEnd = separator;
        var targetStart = targetEnd;
        while (targetStart > 0 && IsSuffixTokenChar(lineText[targetStart - 1]))
            targetStart--;

        if (targetStart == targetEnd)
            return null;

        var token = lineText[targetStart..targetEnd];
        var explanation = ExplainToken(token);
        if (explanation is null || explanation.Suggestions.Count == 0)
            return null;

        return new SuffixCompletionContext(explanation, partialWord);
    }

    public static string FormatHoverMarkdown(SuffixExplanation explanation)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"**{explanation.RawToken}**");
        builder.AppendLine();
        builder.AppendLine($"Durum: `{explanation.CaseDisplayName}` (`{explanation.SuffixCase}`)");
        builder.AppendLine();
        builder.AppendLine(explanation.Description);
        builder.AppendLine();
        builder.AppendLine($"Kök: `{explanation.Stem}`");

        if (explanation.UsedMorphology)
        {
            builder.AppendLine();
            builder.AppendLine("Çözümleme: Zemberek morfolojisi");
        }
        else if (explanation.UsedFallbackMarkers)
        {
            builder.AppendLine();
            builder.AppendLine("Çözümleme: ek belirteçleri");
        }

        if (!string.IsNullOrWhiteSpace(explanation.MorphologyFailureMessage))
        {
            builder.AppendLine();
            builder.AppendLine($"Not: Zemberek kullanılamadı, işaretlerle devam edildi. `{explanation.MorphologyFailureMessage}`");
        }

        var verbSuggestions = explanation.Suggestions.Where(s => s.Kind == SuffixSuggestionKind.Verb).ToList();
        if (verbSuggestions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Bilinen fiiller:");
            foreach (var suggestion in verbSuggestions)
            {
                builder.Append("- `").Append(suggestion.Word).Append("` → `").Append(suggestion.CSharp).Append('`');
                if (!string.IsNullOrWhiteSpace(suggestion.Description))
                    builder.Append(" — ").Append(suggestion.Description);
                builder.AppendLine();
            }
        }

        var propertySuggestions = explanation.Suggestions.Where(s => s.Kind == SuffixSuggestionKind.Property).ToList();
        if (propertySuggestions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Bilinen özellikler:");
            foreach (var suggestion in propertySuggestions)
            {
                builder.Append("- `").Append(suggestion.Word).Append("` → `").Append(suggestion.CSharp).Append('`');
                if (!string.IsNullOrWhiteSpace(suggestion.Description))
                    builder.Append(" — ").Append(suggestion.Description);
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string? GetLineText(string text, int line)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return line < 0 || line >= lines.Length ? null : lines[line];
    }

    private static bool TryFindTokenSpan(string lineText, int character, out int start, out int end)
    {
        start = 0;
        end = 0;

        if (lineText.Length == 0)
            return false;

        var index = GetNearestTokenCharIndex(lineText, character);
        if (index < 0)
            return false;

        start = index;
        end = index + 1;

        while (start > 0 && IsSuffixTokenChar(lineText[start - 1]))
            start--;

        while (end < lineText.Length && IsSuffixTokenChar(lineText[end]))
            end++;

        return true;
    }

    private static int GetNearestTokenCharIndex(string lineText, int character)
    {
        if (lineText.Length == 0)
            return -1;

        var candidates = new[] { character, character - 1, character + 1 };
        foreach (var candidate in candidates)
        {
            if (candidate >= 0 && candidate < lineText.Length && IsSuffixTokenChar(lineText[candidate]))
                return candidate;
        }

        return -1;
    }

    private static bool IsSuffixTokenChar(char c)
    {
        return IsWordChar(c) || c == '\'' || c == '’';
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private static string ExtractStem(string token, SuffixEntry suffixEntry)
    {
        var normalizedToken = token.Replace('’', '\'');
        var apostropheIndex = normalizedToken.IndexOf('\'');
        if (apostropheIndex > 0)
            return normalizedToken[..apostropheIndex];

        foreach (var marker in suffixEntry.Markers.OrderByDescending(m => m.Length))
        {
            var normalizedMarker = marker.Replace("'", string.Empty);
            if (normalizedToken.EndsWith(normalizedMarker, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedToken[..^normalizedMarker.Length];
            }
        }

        return normalizedToken;
    }

    private static string GetCaseDisplayName(string suffixCase) => suffixCase switch
    {
        "dative" => "yönelme",
        "ablative" => "ayrılma",
        "genitive" => "ilgi",
        "locative" => "bulunma",
        "accusative" => "belirtme",
        _ => suffixCase
    };
}