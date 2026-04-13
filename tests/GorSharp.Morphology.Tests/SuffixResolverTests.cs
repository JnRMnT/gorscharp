using GorSharp.Core.Sozluk;
using GorSharp.Morphology;

namespace GorSharp.Tests.Morphology;

public class SuffixResolverTests
{
    private static SuffixResolver CreateResolver()
    {
        return new SuffixResolver(new SozlukData());
    }

    [Theory]
    [InlineData("90'dan", 90)]
    [InlineData("100'den", 100)]
    [InlineData("75'ten", 75)]
    [InlineData("0'tan", 0)]
    public void DetectAblativeNumber_ValidTokens_ReturnsValue(string token, int expected)
    {
        var resolver = CreateResolver();

        var result = resolver.DetectAblativeNumber(token);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
        Assert.Equal("ablative", result.SuffixCase);
    }

    [Theory]
    [InlineData("90'a")]
    [InlineData("90'da")]
    [InlineData("doksan'dan")]
    [InlineData("90")]
    public void DetectAblativeNumber_InvalidTokens_ReturnsNull(string token)
    {
        var resolver = CreateResolver();

        var result = resolver.DetectAblativeNumber(token);

        Assert.Null(result);
    }

    [Fact]
    public void DetectCase_FallsBackToSuffixMarkers_WhenMorphologyUnavailable()
    {
        var resolver = CreateResolver();

        var result = resolver.DetectCase("liste'den");

        Assert.Equal("ablative", result);
    }

    [Fact]
    public void Resolve_UsesVerbMappingsTemplate_AndReturnsMethodName()
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
        var result = resolver.Resolve("liste'ye", "ekle");

        Assert.NotNull(result);
        Assert.Equal("liste", result!.Stem);
        Assert.Equal("dative", result.SuffixCase);
        Assert.Equal("Add", result.CSharpMethod);
    }
}
