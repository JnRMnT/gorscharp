using System.Text;
using CliProgram = GorSharp.CLI.Program;

namespace GorSharp.Tests.Integration;

public class CSharpImportNarrativeTests
{
    [Fact]
    public void ConvertWithNarration_ReturnsTeacherStyleExplanations()
    {
        var source = """
            int sayaç = 5;
            Console.WriteLine(sayaç);
            """;

        var result = GorSharp.CLI.CSharpToGorConverter.ConvertWithNarration(source);

        Assert.Contains("sayaç: sayı 5 olsun;", result.GorSource, StringComparison.Ordinal);
        Assert.NotEmpty(result.Explanations);
        Assert.Contains(result.Explanations, explanation => explanation.Title == "Türlü değişken bildirimi");
        Assert.Contains(result.Explanations, explanation => explanation.Title == "Yazdırma");
        Assert.Contains(result.Explanations, explanation => explanation.Message.Contains("Gör#", StringComparison.Ordinal));
    }

    [Fact]
    public void FromCs_WithExplain_PrintsNarrativeSummary()
    {
        var source = """
            int sayaç = 5;
            Console.WriteLine(sayaç);
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), "gorsharp-fromcs-explain-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var inputPath = Path.Combine(tempDir, "input.cs");
        var outputPath = Path.Combine(tempDir, "output.gör");
        File.WriteAllText(inputPath, source);

        var originalOut = Console.Out;
        var capture = new StringWriter(new StringBuilder());
        Console.SetOut(capture);

        try
        {
            var exitCode = CliProgram.Main(["fromcs", inputPath, "-o", outputPath, "--explain"]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }

        var output = capture.ToString();
        Assert.Contains("Açıklamalar:", output, StringComparison.Ordinal);
        Assert.Contains("Türlü değişken bildirimi", output, StringComparison.Ordinal);
        Assert.Contains("Yazdırma", output, StringComparison.Ordinal);
    }
}