using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace GorSharp.VisualStudio;

internal sealed class GorSharpClassifier : IClassifier
{
    private static readonly Regex StringRegex = new("\"([^\\\"]|\\.)*\"", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new("\\b\\d+(?:\\.\\d+)?\\b", RegexOptions.Compiled);
    private static readonly Regex CommentRegex = new("//.*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex KeywordRegex = new(
        "\\b(eğer|değilse|yoksa\\s+eğer|döngü|tekrarla|her|fonksiyon|döndür|kır|devam|olsun|yazdır|yeniSatıraYazdır|oku|okuSatır|dene|hata_varsa|sonunda|sınıf|kurucu|bu|miras|ve|veya|değil|eşittir|eşitDeğildir|büyüktür|küçüktür|büyükEşittir|küçükEşittir)\\b",
        RegexOptions.Compiled);

    private readonly IClassificationType _keyword;
    private readonly IClassificationType _string;
    private readonly IClassificationType _number;
    private readonly IClassificationType _comment;

    public GorSharpClassifier(IClassificationTypeRegistryService registry)
    {
        _keyword = registry.GetClassificationType("keyword");
        _string = registry.GetClassificationType("string");
        _number = registry.GetClassificationType("number");
        _comment = registry.GetClassificationType("comment");
    }

    public event EventHandler<ClassificationChangedEventArgs>? ClassificationChanged;

    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
    {
        var spans = new List<ClassificationSpan>();
        var text = span.GetText();

        AddMatches(spans, span, text, CommentRegex, _comment);
        AddMatches(spans, span, text, StringRegex, _string);
        AddMatches(spans, span, text, NumberRegex, _number);
        AddMatches(spans, span, text, KeywordRegex, _keyword);

        return spans;
    }

    private static void AddMatches(
        List<ClassificationSpan> spans,
        SnapshotSpan baseSpan,
        string text,
        Regex regex,
        IClassificationType classification)
    {
        foreach (Match match in regex.Matches(text))
        {
            if (!match.Success || match.Length == 0)
            {
                continue;
            }

            var start = baseSpan.Start + match.Index;
            var tokenSpan = new SnapshotSpan(start, match.Length);
            spans.Add(new ClassificationSpan(tokenSpan, classification));
        }
    }
}
