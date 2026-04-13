using GorSharp.Core.Ast;
using GorSharp.Parser;
using Xunit;

namespace GorSharp.Tests.Parser;

public class ParserTests
{
    private static ProgramNode Parse(string source)
    {
        var service = new GorSharpParserService();
        var (ast, diagnostics) = service.Parse(source);
        Assert.Empty(diagnostics);
        return ast;
    }

    private static IReadOnlyList<GorSharp.Core.Diagnostics.Diagnostic> ParseDiagnostics(string source, GorSharpParsingOptions options)
    {
        var service = new GorSharpParserService(options: options);
        var (_, diagnostics) = service.Parse(source);
        return diagnostics;
    }

    // ── Assignments ─────────────────────────────────────────────

    [Fact]
    public void InferredAssignment_ParsesAsDeclaration()
    {
        var ast = Parse("x 5 olsun;");
        var node = Assert.IsType<AssignmentNode>(Assert.Single(ast.Statements));
        Assert.Equal("x", node.Name);
        Assert.Null(node.ExplicitType);
        Assert.True(node.IsDeclaration);
    }

    [Fact]
    public void TypedAssignment_ParsesWithType()
    {
        var ast = Parse("x: sayı 5 olsun;");
        var node = Assert.IsType<AssignmentNode>(Assert.Single(ast.Statements));
        Assert.Equal("x", node.Name);
        Assert.Equal("sayı", node.ExplicitType);
        Assert.True(node.IsDeclaration);
    }

    [Theory]
    [InlineData("x değişkeni 5 olsun;")]
    [InlineData("x olarak 5 olsun;")]
    [InlineData("x: sayı değişkeni 5 olsun;")]
    public void Declaration_AllowsNaturalParticles(string source)
    {
        var ast = Parse(source);
        var node = Assert.IsType<AssignmentNode>(Assert.Single(ast.Statements));
        Assert.Equal("x", node.Name);
        Assert.True(node.IsDeclaration);
    }

    [Fact]
    public void EqualsAssignment_ParsesAsReassignment()
    {
        var ast = Parse("x = 5;");
        var node = Assert.IsType<AssignmentNode>(Assert.Single(ast.Statements));
        Assert.Equal("x", node.Name);
        Assert.False(node.IsDeclaration);
    }

    // ── Print ───────────────────────────────────────────────────

    [Fact]
    public void Write_ParsesCorrectly()
    {
        var ast = Parse("\"Merhaba\" yazdır;");
        var node = Assert.IsType<PrintNode>(Assert.Single(ast.Statements));
        Assert.False(node.IsWriteLine);
    }

    [Fact]
    public void WriteLine_ParsesCorrectly()
    {
        var ast = Parse("\"Merhaba\" yeniSatıraYazdır;");
        var node = Assert.IsType<PrintNode>(Assert.Single(ast.Statements));
        Assert.True(node.IsWriteLine);
    }

    // ── If / Else ───────────────────────────────────────────────

    [Fact]
    public void IfStatement_ParsesConditionAndBody()
    {
        var ast = Parse("eğer x büyüktür 5 { x yazdır; }");
        var node = Assert.IsType<IfNode>(Assert.Single(ast.Statements));
        Assert.NotNull(node.Condition);
        Assert.NotNull(node.ThenBlock);
        Assert.Empty(node.ElseIfClauses);
        Assert.Null(node.ElseBlock);
    }

    [Fact]
    public void IfElse_ParsesElseBlock()
    {
        var ast = Parse("eğer x büyüktür 5 { } değilse { }");
        var node = Assert.IsType<IfNode>(Assert.Single(ast.Statements));
        Assert.NotNull(node.ElseBlock);
    }

    [Fact]
    public void IfElseIfElse_ParsesAllBranches()
    {
        var ast = Parse("eğer x büyüktür 5 { } yoksa eğer x eşittir 5 { } değilse { }");
        var node = Assert.IsType<IfNode>(Assert.Single(ast.Statements));
        Assert.Single(node.ElseIfClauses);
        Assert.NotNull(node.ElseBlock);
    }

    [Theory]
    [InlineData("eğer x büyüktür 5 ise { }")]
    [InlineData("eğer x büyüktür 5 olursa { }")]
    [InlineData("eğer x büyüktür 5 mi { }")]
    [InlineData("eğer x büyüktür 5 şayet { }")]
    public void IfStatement_AllowsNoopParticles(string source)
    {
        var ast = Parse(source);
        var node = Assert.IsType<IfNode>(Assert.Single(ast.Statements));
        Assert.NotNull(node.Condition);
    }

    [Fact]
    public void IfStatement_ParsesAblativeComparison()
    {
        var ast = Parse("eğer puan 90'dan büyük veya eşit ise { }");
        var node = Assert.IsType<IfNode>(Assert.Single(ast.Statements));
        var comparison = Assert.IsType<BinaryExpressionNode>(node.Condition);
        Assert.Equal(BinaryOperator.BuyukEsittir, comparison.Operator);
    }

    // ── While Loop ──────────────────────────────────────────────

    [Fact]
    public void WhileLoop_ParsesCorrectly()
    {
        var ast = Parse("döngü x büyüktür 0 { }");
        var node = Assert.IsType<WhileNode>(Assert.Single(ast.Statements));
        Assert.NotNull(node.Condition);
        Assert.NotNull(node.Body);
    }

    [Fact]
    public void WhileLoop_AllowsIkenParticle()
    {
        var ast = Parse("döngü x büyüktür 0 iken { }");
        var node = Assert.IsType<WhileNode>(Assert.Single(ast.Statements));
        Assert.NotNull(node.Condition);
    }

    [Theory]
    [InlineData("döngü x büyüktür 0 boyunca { }")]
    [InlineData("döngü x büyüktür 0 sürece { }")]
    public void WhileLoop_AllowsNaturalLoopParticles(string source)
    {
        var ast = Parse(source);
        var node = Assert.IsType<WhileNode>(Assert.Single(ast.Statements));
        Assert.NotNull(node.Condition);
    }

    // ── For Loop ────────────────────────────────────────────────

    [Fact]
    public void ForLoop_ParsesCorrectly()
    {
        var ast = Parse("tekrarla (i 0 olsun; i küçüktür 10; i = i + 1) { }");
        var node = Assert.IsType<ForNode>(Assert.Single(ast.Statements));
        Assert.NotNull(node.Initializer);
        Assert.NotNull(node.Condition);
        Assert.NotNull(node.Step);
    }

    [Fact]
    public void StrictMode_RejectsNaturalParticleUsage()
    {
        var diagnostics = ParseDiagnostics("x değişkeni 5 olsun;", new GorSharpParsingOptions { NaturalMode = false });
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("GOR1001", diagnostic.Code);
    }

    // ── Function Definition ─────────────────────────────────────

    [Fact]
    public void FunctionDefinition_ParsesParametersAndReturnType()
    {
        var ast = Parse("fonksiyon topla(a: sayı, b: sayı): sayı { döndür a + b; }");
        var node = Assert.IsType<FunctionDefinitionNode>(Assert.Single(ast.Statements));
        Assert.Equal("topla", node.Name);
        Assert.Equal(2, node.Parameters.Count);
        Assert.Equal("sayı", node.Parameters[0].Type);
        Assert.Equal("sayı", node.ReturnType);
    }

    [Fact]
    public void FunctionDefinition_VoidFunction_HasNoReturnType()
    {
        var ast = Parse("fonksiyon selamla() { \"merhaba\" yazdır; }");
        var node = Assert.IsType<FunctionDefinitionNode>(Assert.Single(ast.Statements));
        Assert.Null(node.ReturnType);
    }

    // ── Return / Break / Continue ───────────────────────────────

    [Fact]
    public void Return_ParsesWithExpression()
    {
        var ast = Parse("döndür 42;");
        var node = Assert.IsType<ReturnNode>(Assert.Single(ast.Statements));
        Assert.NotNull(node.Expression);
    }

    [Fact]
    public void Return_ParsesWithoutExpression()
    {
        var ast = Parse("döndür;");
        var node = Assert.IsType<ReturnNode>(Assert.Single(ast.Statements));
        Assert.Null(node.Expression);
    }

    [Fact]
    public void Break_ParsesCorrectly()
    {
        var ast = Parse("kır;");
        Assert.IsType<BreakNode>(Assert.Single(ast.Statements));
    }

    [Fact]
    public void Continue_ParsesCorrectly()
    {
        var ast = Parse("devam;");
        Assert.IsType<ContinueNode>(Assert.Single(ast.Statements));
    }

    // ── Function Call in Expression ─────────────────────────────

    [Fact]
    public void FunctionCall_ParsesInPrintContext()
    {
        var ast = Parse("topla(3, 5) yazdır;");
        var print = Assert.IsType<PrintNode>(Assert.Single(ast.Statements));
        var call = Assert.IsType<FunctionCallNode>(print.Expression);
        Assert.Equal("topla", call.FunctionName);
        Assert.Equal(2, call.Arguments.Count);
    }

    [Fact]
    public void SuffixMethodCall_ParsesNaturalStatement()
    {
        var ast = Parse("liste'ye 10 ekle;");
        var node = Assert.IsType<SuffixMethodCallNode>(Assert.Single(ast.Statements));
        Assert.Equal("liste'ye", node.TargetToken);
        Assert.Equal("liste", node.TargetStem);
        Assert.Equal("ekle", node.Verb);
        Assert.Single(node.Arguments);
    }

    [Fact]
    public void SuffixMethodCall_ParsesChainedStatement()
    {
        var ast = Parse("liste'ye 10 ekle sonra 20 ekle;");
        var node = Assert.IsType<SuffixMethodChainNode>(Assert.Single(ast.Statements));
        Assert.Equal("liste'ye", node.TargetToken);
        Assert.Equal("liste", node.TargetStem);
        Assert.Equal("dative", node.SuffixCase);
        Assert.Equal(2, node.Steps.Count);
        Assert.Equal("ekle", node.Steps[0].Verb);
        Assert.Equal("ekle", node.Steps[1].Verb);
    }

    [Fact]
    public void SuffixMethodCall_ParsesChainedPropertyPrintTail()
    {
        var ast = Parse("liste'ye 10 ekle sonra uzunluğu yazdır;");
        var node = Assert.IsType<SuffixMethodChainNode>(Assert.Single(ast.Statements));
        Assert.Equal("liste'ye", node.TargetToken);
        Assert.Single(node.Steps);
        Assert.Equal("ekle", node.Steps[0].Verb);
        Assert.Equal("uzunluğu", node.TailPropertyWord);
        Assert.False(node.TailIsWriteLine);
    }

    [Fact]
    public void SuffixPropertyAccess_ParsesAsExpression()
    {
        var ast = Parse("liste'nin uzunluğu yazdır;");
        var print = Assert.IsType<PrintNode>(Assert.Single(ast.Statements));
        var node = Assert.IsType<SuffixPropertyAccessNode>(print.Expression);
        Assert.Equal("liste'nin", node.TargetToken);
        Assert.Equal("liste", node.TargetStem);
        Assert.Equal("uzunluğu", node.PropertyWord);
        Assert.Equal("genitive", node.SuffixCase);
        Assert.Null(node.ResolvedMember);
    }

    // ── Expressions ─────────────────────────────────────────────

    [Theory]
    [InlineData("x 3 + 5 olsun;")]
    [InlineData("x 3 - 5 olsun;")]
    [InlineData("x 3 * 5 olsun;")]
    [InlineData("x 3 / 5 olsun;")]
    [InlineData("x 3 % 5 olsun;")]
    public void ArithmeticExpressions_ParseWithoutErrors(string source)
    {
        var ast = Parse(source);
        Assert.Single(ast.Statements);
    }

    [Theory]
    [InlineData("x doğru ve yanlış olsun;")]
    [InlineData("x doğru veya yanlış olsun;")]
    [InlineData("x doğru ya da yanlış olsun;")]
    [InlineData("x doğru hem de yanlış olsun;")]
    public void LogicalExpressions_ParseCorrectly(string source)
    {
        var ast = Parse(source);
        Assert.Single(ast.Statements);
    }

    [Fact]
    public void NeNeDeExpression_ParsesCorrectly()
    {
        var ast = Parse("x ne a eşittir 1 ne de b eşittir 2 olsun;");
        var assignment = Assert.IsType<AssignmentNode>(Assert.Single(ast.Statements));
        var expr = Assert.IsType<BinaryExpressionNode>(assignment.Value);
        Assert.Equal(BinaryOperator.Ve, expr.Operator);
        Assert.IsType<UnaryExpressionNode>(expr.Left);
        Assert.IsType<UnaryExpressionNode>(expr.Right);
    }

    [Fact]
    public void BooleanAliases_ParseCorrectly()
    {
        var ast = Parse("aktif evet olsun; pasif hayır olsun;");
        Assert.Equal(2, ast.Statements.Count);
    }
}
