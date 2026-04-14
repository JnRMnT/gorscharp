using GorSharp.Core.Diagnostics;
using GorSharp.LanguageServer.Services;

namespace GorSharp.Tests.Integration;

public class GeneratedCSharpDiagnosticsTests
{
    private static string ResolveSozlukPath()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var path = Path.Combine(dir, "dictionaries", "sozluk.json");
            if (File.Exists(path))
                return path;

            dir = Path.GetDirectoryName(dir)!;
        }

        throw new FileNotFoundException("dictionaries/sozluk.json bulunamadı.");
    }

    private static TranspilationResult Transpile(string source)
    {
        var service = new TranspilationService(new ParsingModeService());
        service.LoadSozluk(ResolveSozlukPath());
        return service.Transpile(source, "test.gör");
    }

    [Fact]
    public void GeneratedCSharpCompilationError_MapsToOriginalLine()
    {
        var result = Transpile("int 5 olsun;");

        var diagnostics = result.Diagnostics
            .Where(d => d.Code == DiagnosticCodes.GeneratedCSharpCompilationError)
            .ToList();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(1, diagnostic.Line);
            Assert.Contains("üretilen C#", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("var int = 5;", diagnostic.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void GeneratedCSharpCompilationError_InMultilineStatement_MapsUsingSourceComments()
    {
        var result = Transpile("x 1 olsun;\nint 1 olsun;");

        var diagnostics = result.Diagnostics
            .Where(d => d.Code == DiagnosticCodes.GeneratedCSharpCompilationError)
            .ToList();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.True(diagnostic.Line >= 1);
            Assert.Contains("var int = 1;", diagnostic.Message, StringComparison.Ordinal);
        });
    }
}