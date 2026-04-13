using GorSharp.Core.Sozluk;
using GorSharp.Parser;
using GorSharp.Transpiler;
using Xunit;

namespace GorSharp.Tests.Integration;

public class TranspileTests
{
    private static readonly SozlukData? _sozluk = LoadSozluk();

    private static SozlukData? LoadSozluk()
    {
        // Walk up from bin output to find dictionaries/sozluk.json
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var path = Path.Combine(dir, "dictionaries", "sozluk.json");
            if (File.Exists(path))
            {
                var service = new SozlukService();
                service.Load(path);
                return service.Data;
            }
            dir = Path.GetDirectoryName(dir)!;
        }
        return null;
    }

    private static string Transpile(string source)
    {
        var parser = new GorSharpParserService();
        var (ast, diagnostics) = parser.Parse(source);

        Assert.Empty(diagnostics);

        var emitter = new CSharpEmitter(_sozluk);
        return emitter.Emit(ast).Trim();
    }

    [Fact]
    public void InferredAssignment_ProducesVar()
    {
        var result = Transpile("x 5 olsun;");
        Assert.Contains("var x = 5;", result);
    }

    [Fact]
    public void TypedAssignment_ProducesExplicitType()
    {
        var result = Transpile("y: sayı 10 olsun;");
        Assert.Contains("int y = 10;", result);
    }

    [Fact]
    public void EqualsAssignment_ProducesReassignment()
    {
        var result = Transpile("x = 42;");
        Assert.Contains("x = 42;", result);
    }

    [Fact]
    public void PrintSOV_ProducesConsoleWrite()
    {
        var result = Transpile("\"Merhaba\" yazdır;");
        Assert.Contains("Console.Write(\"Merhaba\");", result);
    }

    [Fact]
    public void PrintLineSOV_ProducesConsoleWriteLine()
    {
        var result = Transpile("\"Merhaba\" yeniSatıraYazdır;");
        Assert.Contains("Console.WriteLine(\"Merhaba\");", result);
    }

    [Fact]
    public void ArithmeticExpression_Transpiles()
    {
        var result = Transpile("x 3 + 5 olsun;");
        Assert.Contains("var x = 3 + 5;", result);
    }

    [Fact]
    public void BooleanLiterals_Transpile()
    {
        var result = Transpile("x doğru olsun;");
        Assert.Contains("var x = true;", result);
    }

    [Fact]
    public void SourceLocationComment_IsPresent()
    {
        var result = Transpile("x 5 olsun;");
        Assert.Contains("/* gör:", result);
    }

    [Theory]
    [InlineData("x 5 olsun;", "var x = 5;")]
    [InlineData("y: metin \"Ali\" olsun;", "string y = \"Ali\";")]
    [InlineData("z: mantık doğru olsun;", "bool z = true;")]
    [InlineData("n: ondalık 3.14 olsun;", "double n = 3.14;")]
    public void TypedDeclarations_MapCorrectly(string source, string expected)
    {
        var result = Transpile(source);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("x değişkeni 5 olsun;", "var x = 5;")]
    [InlineData("x olarak 5 olsun;", "var x = 5;")]
    [InlineData("x: sayı değişkeni 5 olsun;", "int x = 5;")]
    public void NaturalDeclarationParticles_ProduceEquivalentCSharp(string source, string expected)
    {
        var result = Transpile(source);
        Assert.Contains(expected, result);
    }

    [Fact]
    public void SuffixMethodCall_UsesSozlukMappingInCSharp()
    {
        var result = Transpile("liste'ye 10 ekle;");
        Assert.Contains("liste.Add(10);", result);
    }

    [Fact]
    public void SuffixMethodCall_Chained_UsesSozlukMappingInCSharp()
    {
        var result = Transpile("liste'ye 10 ekle sonra 20 ekle;");
        Assert.Contains("liste.Add(10);", result);
        Assert.Contains("liste.Add(20);", result);
    }

    [Fact]
    public void SuffixMethodCall_ChainedPropertyPrint_UsesSozlukMappingsInCSharp()
    {
        var result = Transpile("liste'ye 10 ekle sonra uzunluğu yazdır;");
        Assert.Contains("liste.Add(10);", result);
        Assert.Contains("Console.Write(liste.Count);", result);
    }

    [Fact]
    public void SuffixPropertyAccess_UsesSozlukMappingInCSharp()
    {
        var result = Transpile("liste'nin uzunluğu yazdır;");
        Assert.Contains("Console.Write(liste.Count);", result);
    }
}
