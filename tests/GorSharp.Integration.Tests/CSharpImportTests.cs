using CliProgram = GorSharp.CLI.Program;

namespace GorSharp.Tests.Integration;

public class CSharpImportTests
{
    [Fact]
    public void FromCs_ConvertsTopLevelStatements_WithRoslynImporter()
    {
        var source = """
            int sayaç = 5;
            Console.WriteLine(sayaç);
            sayaç = sayaç + 1;
            """;

        var result = RunFromCs(source);

        Assert.Contains("sayaç: sayı 5 olsun;", result, StringComparison.Ordinal);
        Assert.Contains("sayaç yeniSatıraYazdır;", result, StringComparison.Ordinal);
        Assert.Contains("sayaç = sayaç + 1;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FromCs_ConvertsMethodAndControlFlow_WithRoslynImporter()
    {
        var source = """
            static int enBuyuk(int a, int b)
            {
                if (a > b)
                {
                    return a;
                }

                return b;
            }
            """;

        var result = RunFromCs(source);

        Assert.Contains("fonksiyon enBuyuk(a: sayı, b: sayı): sayı {", result, StringComparison.Ordinal);
        Assert.Contains("eğer a büyüktür b {", result, StringComparison.Ordinal);
        Assert.Contains("döndür a;", result, StringComparison.Ordinal);
        Assert.Contains("döndür b;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FromCs_UnwrapsProgramMain_ForBeginnerConsoleTemplate()
    {
        var source = """
            using System;

            class Program
            {
                static void Main()
                {
                    Console.WriteLine("Merhaba");
                }
            }
            """;

        var result = RunFromCs(source);

        Assert.Contains("\"Merhaba\" yeniSatıraYazdır;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("DESTEKLENMEYEN SINIF", result, StringComparison.Ordinal);
    }

    private static string RunFromCs(string source)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "gorsharp-fromcs-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var inputPath = Path.Combine(tempDir, "input.cs");
        var outputPath = Path.Combine(tempDir, "output.gör");
        File.WriteAllText(inputPath, source);

        try
        {
            var exitCode = CliProgram.Main(["fromcs", inputPath, "-o", outputPath]);
            Assert.Equal(0, exitCode);
            return File.ReadAllText(outputPath).Replace("\r\n", "\n");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}