using System.Text.RegularExpressions;

namespace GorSharp.LanguageServer.Services;

/// <summary>
/// Classifies Gör# source text into semantic token spans using regex.
/// This is parser-independent so it works even on incomplete/invalid source.
/// </summary>
public static class GorSharpTokenClassifier
{
    // Ordered by priority — first match wins for each position.
    private static readonly (Regex Pattern, SemanticTokenKind Kind)[] Rules =
    [
        // Line and block comments
        (new Regex(@"//[^\r\n]*",           RegexOptions.Compiled), SemanticTokenKind.Comment),
        (new Regex(@"/\*.*?\*/",            RegexOptions.Compiled | RegexOptions.Singleline), SemanticTokenKind.Comment),

        // String literals
        (new Regex(@"""(?:[^""\\]|\\.)*""", RegexOptions.Compiled), SemanticTokenKind.String),

        // Numeric literals (float before int)
        (new Regex(@"\b\d+\.\d+\b",        RegexOptions.Compiled), SemanticTokenKind.Number),
        (new Regex(@"\b\d+\b",             RegexOptions.Compiled), SemanticTokenKind.Number),

        // Control-flow keywords
        (new Regex(
            @"\b(eğer|değilse|yoksa|döngü|tekrarla|her|döndür|kır|devam|dene|hata_varsa|sonunda|başla)\b",
            RegexOptions.Compiled),
         SemanticTokenKind.Keyword),

        // Declaration / IO keywords
        (new Regex(
            @"\b(olsun|yazdır|yeniSatıraYazdır|oku|okuSatır|fonksiyon|sınıf|kurucu|bu|miras|sonra|ile)\b",
            RegexOptions.Compiled),
         SemanticTokenKind.Keyword),

        // Type names
        (new Regex(
            @"\b(sayı|ondalık|metin|mantık|karakter|liste|sözlük|dizi|nesne|boşluk)\b",
            RegexOptions.Compiled),
         SemanticTokenKind.Type),

        // Boolean / null literals
        (new Regex(@"\b(doğru|yanlış|boş)\b", RegexOptions.Compiled), SemanticTokenKind.EnumMember),

        // Named operators (comparison / logical)
        (new Regex(
            @"\b(eşittir|eşitDeğildir|büyüktür|küçüktür|büyükEşittir|küçükEşittir|ve|veya|değil)\b",
            RegexOptions.Compiled),
         SemanticTokenKind.Operator),

        // Compound-assignment verbs
        (new Regex(@"\b(arttır|azalt|çarp|böl)\b", RegexOptions.Compiled), SemanticTokenKind.Operator),

        // Function call: identifier followed by (
        (new Regex(@"\b([\wöüşçığİÖÜŞÇĞ]+)(?=\s*\()", RegexOptions.Compiled), SemanticTokenKind.Function),

        // Parameters / identifiers  — everything else
        (new Regex(@"\b[\wöüşçığİÖÜŞÇĞ]+\b", RegexOptions.Compiled), SemanticTokenKind.Variable),
    ];

    /// <summary>Returns all classified spans in document order, non-overlapping.</summary>
    public static IReadOnlyList<TokenSpan> Classify(string text)
    {
        // Track which character positions have already been claimed.
        var covered = new HashSet<int>();
        var results = new List<TokenSpan>();

        foreach (var (pattern, kind) in Rules)
        {
            foreach (Match m in pattern.Matches(text))
            {
                // Skip if this range overlaps an already-classified region.
                if (covered.Contains(m.Index))
                    continue;

                results.Add(new TokenSpan(m.Index, m.Length, kind));
                for (int i = m.Index; i < m.Index + m.Length; i++)
                    covered.Add(i);
            }
        }

        results.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return results;
    }
}

public enum SemanticTokenKind
{
    Keyword,
    Type,
    Function,
    Variable,
    Parameter,
    String,
    Number,
    Operator,
    Comment,
    EnumMember,
}

public record TokenSpan(int StartOffset, int Length, SemanticTokenKind Kind);
