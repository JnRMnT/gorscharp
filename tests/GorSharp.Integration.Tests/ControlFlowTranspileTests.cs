using GorSharp.Core.Sozluk;
using GorSharp.Parser;
using GorSharp.Transpiler;
using Xunit;

namespace GorSharp.Tests.Integration;

public class ControlFlowTranspileTests
{
    private static readonly SozlukData? _sozluk = LoadSozluk();

    private static SozlukData? LoadSozluk()
    {
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

    // ── If Statement ────────────────────────────────────────────

    [Fact]
    public void IfStatement_TranspilesCorrectly()
    {
        var result = Transpile("eğer x eşittir 5 { \"beş\" yazdır; }");
        Assert.Contains("if (x == 5)", result);
        Assert.Contains("Console.Write(\"beş\")", result);
    }

    [Fact]
    public void IfElse_TranspilesCorrectly()
    {
        var result = Transpile("eğer x büyüktür 5 { } değilse { }");
        Assert.Contains("if (x > 5)", result);
        Assert.Contains("else", result);
    }

    [Fact]
    public void IfElseIfElse_TranspilesCorrectly()
    {
        var result = Transpile("eğer x büyüktür 5 { } yoksa eğer x eşittir 5 { } değilse { }");
        Assert.Contains("if (x > 5)", result);
        Assert.Contains("else if (x == 5)", result);
        Assert.Contains("else {", result);
    }

    // ── While Loop ──────────────────────────────────────────────

    [Fact]
    public void WhileLoop_TranspilesCorrectly()
    {
        var result = Transpile("döngü x büyüktür 0 { }");
        Assert.Contains("while (x > 0)", result);
    }

    // ── For Loop ────────────────────────────────────────────────

    [Fact]
    public void ForLoop_TranspilesCorrectly()
    {
        var result = Transpile("tekrarla (i: sayı 0 olsun; i küçüktür 10; i = i + 1) { }");
        Assert.Contains("for (int i = 0; i < 10; i = i + 1)", result);
    }

    // ── Function Definition ─────────────────────────────────────

    [Fact]
    public void FunctionDefinition_TranspilesCorrectly()
    {
        var result = Transpile("fonksiyon topla(a: sayı, b: sayı): sayı { döndür a + b; }");
        Assert.Contains("static int topla(int a, int b)", result);
        Assert.Contains("return a + b;", result);
    }

    [Fact]
    public void VoidFunction_TranspilesCorrectly()
    {
        var result = Transpile("fonksiyon selamla() { \"merhaba\" yazdır; }");
        Assert.Contains("static void selamla()", result);
    }

    // ── Return / Break / Continue ───────────────────────────────

    [Fact]
    public void Return_TranspilesCorrectly()
    {
        var result = Transpile("döndür 42;");
        Assert.Contains("return 42;", result);
    }

    [Fact]
    public void ReturnVoid_TranspilesCorrectly()
    {
        var result = Transpile("döndür;");
        Assert.Contains("return;", result);
    }

    [Fact]
    public void Break_TranspilesCorrectly()
    {
        var result = Transpile("kır;");
        Assert.Contains("break;", result);
    }

    [Fact]
    public void Continue_TranspilesCorrectly()
    {
        var result = Transpile("devam;");
        Assert.Contains("continue;", result);
    }

    // ── Function Call ───────────────────────────────────────────

    [Fact]
    public void FunctionCall_InPrint_TranspilesCorrectly()
    {
        var result = Transpile("topla(3, 5) yazdır;");
        Assert.Contains("Console.Write(topla(3, 5))", result);
    }

    // ── Turkish Operators ───────────────────────────────────────

    [Theory]
    [InlineData("eğer x büyükEşittir 5 { }", ">=")]
    [InlineData("eğer x küçükEşittir 5 { }", "<=")]
    [InlineData("eğer x eşitDeğildir 5 { }", "!=")]
    public void TurkishComparisonOperators_TranspileCorrectly(string source, string expectedOp)
    {
        var result = Transpile(source);
        Assert.Contains(expectedOp, result);
    }
}
