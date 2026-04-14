using GorSharp.LanguageServer.Services;

namespace GorSharp.Tests.Integration;

public class SemanticDiagnosticsTests
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

        throw new FileNotFoundException("dictionaries/sozluk.json bulunamadi.");
    }

    private static TranspilationResult Transpile(string source)
    {
        var service = new TranspilationService(new ParsingModeService());
        service.LoadSozluk(ResolveSozlukPath());
        return service.Transpile(source, "test.gör");
    }

    [Fact]
    public void UndefinedFunction_WhenVariableWithSameNameExists_ReportsEducationalHint()
    {
        var result = Transpile("x 5 olsun; x(1) yazdır;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2001");
        Assert.Contains("değişken", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parantez", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FunctionCall_WhenArgumentTypeMismatches_ReportsGOR2007()
    {
        var result = Transpile("fonksiyon topla(a: sayı): sayı { döndür a; } topla(\"metin\") yazdır;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2007");
        Assert.Contains("argüman türü uyuşmuyor", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'a'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("sayı", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("metin", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BinaryExpression_WhenLogicalOperandsAreNotBoolean_ReportsGOR2008()
    {
        var result = Transpile("x 1 ve 2 olsun;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2008");
        Assert.Contains("Mantıksal işlem türü uyuşmuyor", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("mantık", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BinaryExpression_WhenArithmeticOperandsAreInvalid_ReportsGOR2008()
    {
        var result = Transpile("x doğru + 1 olsun;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2008");
        Assert.Contains("Toplama türü uyuşmuyor", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("mantık", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sayı", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FunctionDefinition_WhenTypedFunctionFallsThrough_ReportsGOR2009()
    {
        var result = Transpile("fonksiyon hesapla(a: sayı): sayı { a yazdır; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2009");
        Assert.Contains("yürütme yolları", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hesapla", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FunctionDefinition_WhenAllBranchesReturn_DoesNotReportGOR2009()
    {
        var result = Transpile("fonksiyon sec(a: sayı): sayı { eğer a büyüktür 0 { döndür a; } değilse { döndür 0; } }");

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2009");
    }

    [Fact]
    public void UnaryExpression_WhenDegilOperandIsNotBoolean_ReportsGOR2010()
    {
        var result = Transpile("x değil 1 olsun;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2010");
        Assert.Contains("'değil'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("mantık", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnaryExpression_WhenNegateOperandIsNotNumeric_ReportsGOR2010()
    {
        var result = Transpile("x - doğru olsun;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2010");
        Assert.Contains("'-'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("sayısal", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VariableDeclaration_WhenShadowsFunctionParameter_ReportsGOR2011()
    {
        var result = Transpile("fonksiyon topla(a: sayı): sayı { a 10 olsun; döndür a; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2011");
        Assert.Contains("'a'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("gölgeliyor", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VariableDeclaration_WhenShadowsOuterLocal_ReportsGOR2011()
    {
        var result = Transpile("başla { x 5 olsun; { x 10 olsun; } }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2011");
        Assert.Contains("'x'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("gölgeliyor", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VariableDeclaration_WhenInNonOverlappingScopes_DoesNotReportGOR2011()
    {
        var result = Transpile("başla { x 5 olsun; } başla { x 10 olsun; }");

        var gor2011 = result.Diagnostics.FirstOrDefault(d => d.Code == "GOR2011");
        Assert.Null(gor2011);
    }

    [Fact]
    public void DuplicateVariable_InSameScope_ReportsGOR2002NotGOR2011()
    {
        var result = Transpile("başla { x 5 olsun; x 10 olsun; }");

        var gor2011 = result.Diagnostics.FirstOrDefault(d => d.Code == "GOR2011");
        Assert.Null(gor2011);

        var gor2002 = Assert.Single(result.Diagnostics, d => d.Code == "GOR2002");
        Assert.Contains("yinelenen", gor2002.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BreakStatement_WhenOutsideLoop_ReportsGOR2012()
    {
        var result = Transpile("kır;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2012");
        Assert.Contains("döngü", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContinueStatement_WhenOutsideLoop_ReportsGOR2012()
    {
        var result = Transpile("devam;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2012");
        Assert.Contains("döngü", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BreakStatement_WhenInsideWhileLoop_DoesNotReport()
    {
        var result = Transpile("döngü(doğru) { kır; }");

        var gor2012 = result.Diagnostics.FirstOrDefault(d => d.Code == "GOR2012");
        Assert.Null(gor2012);
    }

    [Fact]
    public void ContinueStatement_WhenInsideForLoop_DoesNotReport()
    {
        var result = Transpile("tekrarla(i 0 olsun; i küçüktür 5; i = i + 1) { devam; }");

        var gor2012 = result.Diagnostics.FirstOrDefault(d => d.Code == "GOR2012");
        Assert.Null(gor2012);
    }

    [Fact]
    public void VariableDeclaration_WhenNeverUsed_ReportsGOR2013()
    {
        var result = Transpile("başla { x 5 olsun; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2013");
        Assert.Contains("kullan", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'x'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VariableDeclaration_WhenUsed_DoesNotReportGOR2013()
    {
        var result = Transpile("başla { x 5 olsun; x yazdır; }");

        var gor2013 = result.Diagnostics.FirstOrDefault(d => d.Code == "GOR2013");
        Assert.Null(gor2013);
    }

    [Fact]
    public void Parameter_WhenUsed_DoesNotReportGOR2013()
    {
        var result = Transpile("fonksiyon topla(a: sayı): sayı { döndür a; }");

        var gor2013 = result.Diagnostics.FirstOrDefault(d => d.Code == "GOR2013");
        Assert.Null(gor2013);
    }

    [Fact]
    public void Statement_WhenAfterReturn_ReportsGOR2014()
    {
        var result = Transpile("fonksiyon f(): sayı { döndür 1; x 2 olsun; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2014");
        Assert.Contains("Erişilemeyen kod", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("döndür", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Statement_WhenAfterBreakInLoop_ReportsGOR2014()
    {
        var result = Transpile("döngü(doğru) { kır; x 2 olsun; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2014");
        Assert.Contains("kır", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2012");
    }

    [Fact]
    public void Statement_WhenAfterContinueInLoop_ReportsGOR2014()
    {
        var result = Transpile("tekrarla(i 0 olsun; i küçüktür 2; i = i + 1) { devam; x 2 olsun; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2014");
        Assert.Contains("devam", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2012");
    }

    [Fact]
    public void Block_WhenNoTerminator_DoesNotReportGOR2014()
    {
        var result = Transpile("başla { x 1 olsun; x yazdır; }");

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2014");
    }

    [Fact]
    public void ForLoop_WhenConditionMissing_ReportsGOR2015()
    {
        var result = Transpile("tekrarla(i 0 olsun; ; i = i + 1) { i yazdır; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2015");
        Assert.Contains("koşulu eksik", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sonsuz", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForLoop_WhenConditionPresent_DoesNotReportGOR2015()
    {
        var result = Transpile("tekrarla(i 0 olsun; i küçüktür 3; i = i + 1) { i yazdır; }");

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2015");
    }

    [Fact]
    public void WhileLoop_WhenConditionPresent_DoesNotReportGOR2015()
    {
        var result = Transpile("döngü(i küçüktür 3) { i yazdır; }");

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2015");
    }

    [Fact]
    public void FunctionDefinition_WhenNeverCalled_ReportsGOR2016()
    {
        var result = Transpile("fonksiyon hesapla(a: sayı): sayı { döndür a + 1; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2016");
        Assert.Contains("kullanılmıyor", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hesapla", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FunctionDefinition_WhenCalled_DoesNotReportGOR2016()
    {
        var result = Transpile("fonksiyon hesapla(a: sayı): sayı { döndür a + 1; } hesapla(1) yazdır;");

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2016");
    }

    [Fact]
    public void FunctionParameter_WhenNeverUsed_ReportsGOR2017()
    {
        var result = Transpile("fonksiyon hesapla(a: sayı, b: sayı): sayı { döndür a + 1; } hesapla(1, 2) yazdır;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2017");
        Assert.Contains("Parametre kullanılmıyor", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'b'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionParameter_WhenUsed_DoesNotReportGOR2017()
    {
        var result = Transpile("fonksiyon hesapla(a: sayı, b: sayı): sayı { döndür a + b; } hesapla(1, 2) yazdır;");

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2017");
    }

    [Fact]
    public void Assignment_WhenTargetUndefined_ReportsGOR2018()
    {
        var result = Transpile("x = 5;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2018");
        Assert.Contains("tanımlı değil", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assignment_WhenNoOp_ReportsGOR2019()
    {
        var result = Transpile("x 1 olsun; x = x;");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2019");
        Assert.Contains("Etkisiz atama", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Condition_WhenConstantBoolean_ReportsGOR2020()
    {
        var result = Transpile("eğer doğru { 1 yazdır; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2020");
        Assert.Contains("Sabit koşul", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IfElse_WhenBranchesEquivalent_ReportsGOR2021()
    {
        var result = Transpile("c doğru olsun; eğer c { x 1 olsun; } değilse { x 1 olsun; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2021");
        Assert.Contains("Gereksiz dal", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForLoop_WhenStepMovesAwayFromCondition_ReportsGOR2022()
    {
        var result = Transpile("tekrarla(i 10 olsun; i büyüktür 0; i = i + 1) { i yazdır; }");

        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == "GOR2022");
        Assert.Contains("ilerleme", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForLoop_WhenStepMatchesCondition_DoesNotReportGOR2022()
    {
        var result = Transpile("tekrarla(i 10 olsun; i büyüktür 0; i = i - 1) { i yazdır; }");

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "GOR2022");
    }
}
