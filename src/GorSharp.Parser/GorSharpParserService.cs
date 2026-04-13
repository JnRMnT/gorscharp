using System.IO;
using Antlr4.Runtime;
using GorSharp.Core.Ast;
using GorSharp.Core.Diagnostics;
using GorSharp.Core.Sozluk;
using GorSharp.Morphology;

namespace GorSharp.Parser;

/// <summary>
/// Parses Gör# source code into an AST.
/// </summary>
public class GorSharpParserService
{
    private readonly SuffixResolver _suffixResolver;
    private readonly GorSharpParsingOptions _options;

    public GorSharpParserService(SuffixResolver? suffixResolver = null, GorSharpParsingOptions? options = null)
    {
        _suffixResolver = suffixResolver ?? new SuffixResolver(new SozlukData());
        _options = options ?? new GorSharpParsingOptions();
    }

    public (ProgramNode Ast, IReadOnlyList<Diagnostic> Diagnostics) Parse(string sourceCode, string fileName = "<giriş>")
    {
        var diagnostics = new List<Diagnostic>();

        var inputStream = new AntlrInputStream(sourceCode);
        var lexer = new GorSharpLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new GorSharpParser(tokenStream);

        // Collect errors in Turkish
        var errorListener = new TurkishErrorListener(fileName, diagnostics);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(errorListener);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);

        var tree = parser.program();
        var visitor = new AstBuildingVisitor(_suffixResolver, diagnostics, fileName, _options);
        var ast = (ProgramNode)visitor.Visit(tree);

        return (ast, diagnostics);
    }
}

internal class TurkishErrorListener : BaseErrorListener, IAntlrErrorListener<int>
{
    private readonly string _fileName;
    private readonly List<Diagnostic> _diagnostics;

    public TurkishErrorListener(string fileName, List<Diagnostic> diagnostics)
    {
        _fileName = fileName;
        _diagnostics = diagnostics;
    }

    public override void SyntaxError(
        TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
        string msg, RecognitionException e)
    {
        _diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Error,
            DiagnosticCodes.SyntaxError,
            $"Sözdizimi hatası: {msg}",
            _fileName,
            line,
            charPositionInLine));
    }

    public void SyntaxError(
        TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine,
        string msg, RecognitionException e)
    {
        _diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Error,
            DiagnosticCodes.SyntaxError,
            $"Sözdizimi hatası: {msg}",
            _fileName,
            line,
            charPositionInLine));
    }
}
