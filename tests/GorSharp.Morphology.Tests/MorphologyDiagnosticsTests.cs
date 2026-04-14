using GorSharp.Core.Ast;
using GorSharp.Core.Diagnostics;
using GorSharp.Core.Sozluk;
using GorSharp.Morphology;
using GorSharp.Morphology.Normalization;

namespace GorSharp.Tests.Morphology;

public class MorphologyDiagnosticsTests
{
    [Fact]
    public void AnalyzeCase_WhenMorphologyFails_ReportsFailureAndFallback()
    {
        var sozluk = new SozlukData
        {
            Suffixes = new Dictionary<string, SuffixEntry>
            {
                ["ablative"] = new SuffixEntry { Markers = ["'den", "'dan", "'ten", "'tan"] }
            }
        };

        var resolver = new SuffixResolver(
            sozluk,
            () => throw new InvalidOperationException("simüle zemberek hatası"));

        var result = resolver.AnalyzeCase("liste'den");

        Assert.Equal("ablative", result.SuffixCase);
        Assert.False(result.UsedMorphology);
        Assert.True(result.UsedFallbackMarkers);
        Assert.Contains("simüle zemberek hatası", result.MorphologyFailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_WhenMethodMappingMissing_IncludesKnownVerbSuggestions()
    {
        var sozluk = new SozlukData
        {
            Suffixes = new Dictionary<string, SuffixEntry>
            {
                ["dative"] = new SuffixEntry
                {
                    Markers = ["'ye", "'ya"],
                    VerbMappings = new Dictionary<string, SuffixMappingEntry>
                    {
                        ["ekle"] = new SuffixMappingEntry { CSharp = ".Add({arg})" }
                    }
                }
            }
        };

        var resolver = new SuffixResolver(sozluk);
        var pass = new ZemberekMorphologyNormalizationPass(resolver);
        var ast = new ProgramNode(
            [
                new SuffixMethodCallNode(
                    "liste'ye",
                    "liste",
                    "yaz",
                    "dative",
                    null,
                    [new LiteralNode(1, LiteralType.Integer, new SourceLocation(1, 0))],
                    new SourceLocation(1, 0))
            ],
            new SourceLocation(1, 0));

        var result = pass.Normalize(ast, string.Empty, "test.gör");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == DiagnosticCodes.MorphologyMappingMissing);
        Assert.Contains("Bilinen fiiller: ekle", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("yönelme", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_WhenPropertyMappingMissing_IncludesKnownPropertySuggestions()
    {
        var sozluk = new SozlukData
        {
            Suffixes = new Dictionary<string, SuffixEntry>
            {
                ["genitive"] = new SuffixEntry
                {
                    Markers = ["'nin", "'nın"],
                    PropertyMappings = new Dictionary<string, SuffixMappingEntry>
                    {
                        ["uzunluğu"] = new SuffixMappingEntry { CSharp = ".Count" }
                    }
                }
            }
        };

        var resolver = new SuffixResolver(sozluk);
        var pass = new ZemberekMorphologyNormalizationPass(resolver);
        var ast = new ProgramNode(
            [
                new SuffixPropertyAccessNode(
                    "liste'nin",
                    "liste",
                    "toplamı",
                    "genitive",
                    null,
                    new SourceLocation(1, 0))
            ],
            new SourceLocation(1, 0));

        var result = pass.Normalize(ast, string.Empty, "test.gör");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == DiagnosticCodes.MorphologyMappingMissing);
        Assert.Contains("Bilinen özellikler: uzunluğu", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("ilgi", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_WhenMorphologyRuntimeFails_EmitsRuntimeDiagnostic()
    {
        var sozluk = new SozlukData
        {
            Suffixes = new Dictionary<string, SuffixEntry>
            {
                ["dative"] = new SuffixEntry
                {
                    Markers = ["'ye", "'ya"],
                    VerbMappings = new Dictionary<string, SuffixMappingEntry>
                    {
                        ["ekle"] = new SuffixMappingEntry { CSharp = ".Add({arg})" }
                    }
                }
            }
        };

        var resolver = new SuffixResolver(
            sozluk,
            () => throw new InvalidOperationException("simüle zemberek hatası"));
        var pass = new ZemberekMorphologyNormalizationPass(resolver);
        var ast = new ProgramNode([], SourceLocation.Empty);

        var result = pass.Normalize(ast, "liste'ye 10 ekle;", "test.gör");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == DiagnosticCodes.MorphologyRuntimeUnavailable);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("ek belirteçleriyle devam edildi", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_WhenSuffixMethodCaseMissing_InfersUniqueCaseAndResolvesMethod()
    {
        var sozluk = new SozlukData
        {
            Suffixes = new Dictionary<string, SuffixEntry>
            {
                ["dative"] = new SuffixEntry
                {
                    Markers = ["'ye", "'ya"],
                    VerbMappings = new Dictionary<string, SuffixMappingEntry>
                    {
                        ["ekle"] = new SuffixMappingEntry { CSharp = ".Add({arg})" }
                    }
                },
                ["ablative"] = new SuffixEntry
                {
                    Markers = ["'den", "'dan"],
                    VerbMappings = new Dictionary<string, SuffixMappingEntry>
                    {
                        ["çıkar"] = new SuffixMappingEntry { CSharp = ".Remove({arg})" }
                    }
                }
            }
        };

        var resolver = new SuffixResolver(sozluk);
        var pass = new ZemberekMorphologyNormalizationPass(resolver);
        var ast = new ProgramNode(
            [
                new SuffixMethodCallNode(
                    "liste",
                    "liste",
                    "ekle",
                    null,
                    null,
                    [new LiteralNode(1, LiteralType.Integer, new SourceLocation(1, 0))],
                    new SourceLocation(1, 0))
            ],
            new SourceLocation(1, 0));

        var result = pass.Normalize(ast, "liste 1 ekle;", "test.gör");

        var normalized = Assert.IsType<SuffixMethodCallNode>(Assert.Single(result.Ast.Statements));
        Assert.Equal("dative", normalized.SuffixCase);
        Assert.Equal("Add", normalized.ResolvedMethod);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.MorphologyCandidateDetected);
    }

    [Fact]
    public void Normalize_WhenSuffixPropertyCaseMissing_InfersUniqueCaseAndResolvesMember()
    {
        var sozluk = new SozlukData
        {
            Suffixes = new Dictionary<string, SuffixEntry>
            {
                ["genitive"] = new SuffixEntry
                {
                    Markers = ["'nin", "'nın"],
                    PropertyMappings = new Dictionary<string, SuffixMappingEntry>
                    {
                        ["uzunluğu"] = new SuffixMappingEntry { CSharp = ".Count" }
                    }
                },
                ["locative"] = new SuffixEntry
                {
                    Markers = ["'de", "'da"],
                    PropertyMappings = new Dictionary<string, SuffixMappingEntry>
                    {
                        ["içinde"] = new SuffixMappingEntry { CSharp = ".Contains" }
                    }
                }
            }
        };

        var resolver = new SuffixResolver(sozluk);
        var pass = new ZemberekMorphologyNormalizationPass(resolver);
        var ast = new ProgramNode(
            [
                new PrintNode(
                    new SuffixPropertyAccessNode(
                        "liste",
                        "liste",
                        "uzunluğu",
                        null,
                        null,
                        new SourceLocation(1, 0)),
                    isWriteLine: true,
                    new SourceLocation(1, 0))
            ],
            new SourceLocation(1, 0));

        var result = pass.Normalize(ast, "liste uzunluğu yeniSatıraYazdır;", "test.gör");

        var printNode = Assert.IsType<PrintNode>(Assert.Single(result.Ast.Statements));
        var normalized = Assert.IsType<SuffixPropertyAccessNode>(printNode.Expression);
        Assert.Equal("genitive", normalized.SuffixCase);
        Assert.Equal("Count", normalized.ResolvedMember);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.MorphologyCandidateDetected);
    }
}