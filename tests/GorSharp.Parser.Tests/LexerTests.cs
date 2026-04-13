using Antlr4.Runtime;
using GorSharp.Parser;
using Xunit;
using Xunit.Abstractions;

namespace GorSharp.Tests.Parser;

public class LexerTests
{
    private readonly ITestOutputHelper _output;

    public LexerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TypedDeclaration_TokenizesCorrectly()
    {
        var input = new AntlrInputStream("y: sayı 10 olsun;");
        var lexer = new GorSharpLexer(input);
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        foreach (var t in tokens.GetTokens())
        {
            var name = GorSharpLexer.DefaultVocabulary.GetSymbolicName(t.Type);
            if (string.IsNullOrEmpty(name)) name = "EOF";
            _output.WriteLine($"  {name} = '{t.Text}'");
        }

        var tokenList = tokens.GetTokens().Where(t => t.Type != -1).ToList();
        Assert.Equal("IDENTIFIER", GorSharpLexer.DefaultVocabulary.GetSymbolicName(tokenList[0].Type));
        Assert.Equal("COLON", GorSharpLexer.DefaultVocabulary.GetSymbolicName(tokenList[1].Type));
        Assert.Equal("SAYI", GorSharpLexer.DefaultVocabulary.GetSymbolicName(tokenList[2].Type));
    }

    [Fact]
    public void TypedDeclaration_ParsesCorrectly()
    {
        var input = new AntlrInputStream("y: sayı 10 olsun;");
        var lexer = new GorSharpLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new GorSharpParser(tokens);

        var tree = parser.program();
        _output.WriteLine(tree.ToStringTree(parser));

        var visitor = new AstBuildingVisitor();
        var ast = (GorSharp.Core.Ast.ProgramNode)visitor.Visit(tree);
        var stmt = ast.Statements[0];
        _output.WriteLine($"Statement type: {stmt.GetType().Name}");

        var assignment = Assert.IsType<GorSharp.Core.Ast.AssignmentNode>(stmt);
        _output.WriteLine($"Name={assignment.Name}, Type={assignment.ExplicitType}, IsDecl={assignment.IsDeclaration}");
        Assert.True(assignment.IsDeclaration);
        Assert.Equal("sayı", assignment.ExplicitType);
    }
}
