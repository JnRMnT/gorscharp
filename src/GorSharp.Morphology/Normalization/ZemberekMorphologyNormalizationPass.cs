using System.Text.RegularExpressions;
using GorSharp.Core.Ast;
using GorSharp.Core.Diagnostics;

namespace GorSharp.Morphology.Normalization;

/// <summary>
/// Skeleton normalization pass for future natural-language morphology rewrites.
/// Current phase only detects likely suffix-based method patterns and emits GOR3xxx diagnostics.
/// </summary>
public sealed class ZemberekMorphologyNormalizationPass : IMorphologyNormalizationPass
{
    private static readonly Regex SuffixMethodCandidateRegex = new(
        @"\b(?<token>[\p{L}_][\p{L}\p{N}_]*['’](?:ye|ya|e|a|den|dan|ten|tan))\b\s+.+\b(?<verb>ekle|çıkar|sil|al)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly SuffixResolver _suffixResolver;
    private readonly MorphologyNormalizationOptions _options;

    public ZemberekMorphologyNormalizationPass(
        SuffixResolver suffixResolver,
        MorphologyNormalizationOptions? options = null)
    {
        _suffixResolver = suffixResolver;
        _options = options ?? new MorphologyNormalizationOptions();
    }

    public MorphologyNormalizationResult Normalize(ProgramNode ast, string sourceCode, string fileName)
    {
        if (!_options.Enabled || !_options.EmitCandidateDiagnostics)
        {
            return new MorphologyNormalizationResult(ast, []);
        }

        var diagnostics = new List<Diagnostic>();

        CollectAstDiagnostics(ast, fileName, diagnostics);

        if (diagnostics.Count > 0)
        {
            return new MorphologyNormalizationResult(ast, diagnostics);
        }

        var match = SuffixMethodCandidateRegex.Match(sourceCode);
        if (!match.Success)
        {
            return new MorphologyNormalizationResult(ast, diagnostics);
        }

        var token = match.Groups["token"].Value;
        var verb = match.Groups["verb"].Value;
        var (line, column) = ComputeLineColumn(sourceCode, match.Index);

        string? detectedCase = null;
        Exception? morphologyException = null;

        try
        {
            detectedCase = _suffixResolver.DetectCase(token);
        }
        catch (Exception ex)
        {
            morphologyException = ex;
        }

        if (morphologyException is not null)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCodes.MorphologyInconclusive,
                $"Morfolojik çözümleme başarısız: {morphologyException.Message}",
                fileName,
                line,
                column));

            return new MorphologyNormalizationResult(ast, diagnostics);
        }

        if (string.IsNullOrWhiteSpace(detectedCase))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.MorphologyAmbiguous,
                $"Doğal ifade adayı bulundu ancak sonek durumu çözümlenemedi: '{token} {verb}'.",
                fileName,
                line,
                column));

            return new MorphologyNormalizationResult(ast, diagnostics);
        }

        diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Info,
            DiagnosticCodes.MorphologyCandidateDetected,
            $"Morfoloji normalizasyon adayı tespit edildi: '{token} {verb}' ({detectedCase}).",
            fileName,
            line,
            column));

        // Skeleton phase: AST is intentionally unchanged.
        return new MorphologyNormalizationResult(ast, diagnostics);
    }

    private static void CollectAstDiagnostics(ProgramNode ast, string fileName, List<Diagnostic> diagnostics)
    {
        foreach (var statement in ast.Statements)
        {
            CollectStatement(statement, fileName, diagnostics);
        }
    }

    private static void CollectStatement(AstNode node, string fileName, List<Diagnostic> diagnostics)
    {
        if (node is SuffixMethodCallNode suffixNode)
        {
            if (!string.IsNullOrWhiteSpace(suffixNode.ResolvedMethod))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Info,
                    DiagnosticCodes.MorphologyCandidateDetected,
                    $"Morfoloji normalizasyonu uygulanabilir: '{suffixNode.TargetToken} {suffixNode.Verb}' -> '{suffixNode.ResolvedMethod}'.",
                    fileName,
                    suffixNode.Location.Line,
                    suffixNode.Location.Column));
            }
            else
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.MorphologyMappingMissing,
                    $"Sonek çözümleme bulundu ancak fiil eşlemesi yok: '{suffixNode.TargetToken} {suffixNode.Verb}'.",
                    fileName,
                    suffixNode.Location.Line,
                    suffixNode.Location.Column));
            }
        }

        switch (node)
        {
            case BlockNode block:
                foreach (var child in block.Statements)
                    CollectStatement(child, fileName, diagnostics);
                break;
            case IfNode ifNode:
                CollectStatement(ifNode.ThenBlock, fileName, diagnostics);
                foreach (var (_, block) in ifNode.ElseIfClauses)
                    CollectStatement(block, fileName, diagnostics);
                if (ifNode.ElseBlock is not null)
                    CollectStatement(ifNode.ElseBlock, fileName, diagnostics);
                break;
            case WhileNode whileNode:
                CollectStatement(whileNode.Body, fileName, diagnostics);
                break;
            case ForNode forNode:
                CollectStatement(forNode.Body, fileName, diagnostics);
                break;
            case FunctionDefinitionNode functionDefinitionNode:
                CollectStatement(functionDefinitionNode.Body, fileName, diagnostics);
                break;
        }
    }

    private static (int Line, int Column) ComputeLineColumn(string text, int index)
    {
        var line = 1;
        var column = 0;

        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 0;
                continue;
            }

            column++;
        }

        return (line, column);
    }
}
