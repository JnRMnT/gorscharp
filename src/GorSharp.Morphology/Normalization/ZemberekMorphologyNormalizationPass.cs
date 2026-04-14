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
        var normalizedAst = NormalizeProgram(ast, fileName, diagnostics);

        CollectAstDiagnostics(normalizedAst, fileName, diagnostics);

        if (diagnostics.Count > 0)
        {
            return new MorphologyNormalizationResult(normalizedAst, diagnostics);
        }

        var match = SuffixMethodCandidateRegex.Match(sourceCode);
        if (!match.Success)
        {
            return new MorphologyNormalizationResult(normalizedAst, diagnostics);
        }

        var token = match.Groups["token"].Value;
        var verb = match.Groups["verb"].Value;
        var (line, column) = ComputeLineColumn(sourceCode, match.Index);

        var caseAnalysis = _suffixResolver.AnalyzeCase(token);
        if (!string.IsNullOrWhiteSpace(caseAnalysis.MorphologyFailureMessage))
        {
            diagnostics.Add(new Diagnostic(
                string.IsNullOrWhiteSpace(caseAnalysis.SuffixCase) ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                DiagnosticCodes.MorphologyRuntimeUnavailable,
                string.IsNullOrWhiteSpace(caseAnalysis.SuffixCase)
                    ? $"Zemberek kullanılamadı ve sonek durumu çözümlenemedi: {caseAnalysis.MorphologyFailureMessage}"
                    : $"Zemberek kullanılamadı, ek belirteçleriyle devam edildi: {caseAnalysis.MorphologyFailureMessage}",
                fileName,
                line,
                column));
        }

        if (string.IsNullOrWhiteSpace(caseAnalysis.SuffixCase))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.MorphologyAmbiguous,
                $"Doğal ifade adayı bulundu ancak sonek durumu çözümlenemedi: '{token} {verb}'. İşaretten çıkarılabilen bir durum veya Zemberek analizi bulunamadı.",
                fileName,
                line,
                column));

            return new MorphologyNormalizationResult(ast, diagnostics);
        }

        diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Info,
            DiagnosticCodes.MorphologyCandidateDetected,
            $"Morfoloji normalizasyon adayı tespit edildi: '{token} {verb}' ({DescribeSuffixCase(caseAnalysis.SuffixCase!)}).",
            fileName,
            line,
            column));

        // Skeleton phase: AST is intentionally unchanged.
        return new MorphologyNormalizationResult(normalizedAst, diagnostics);
    }

    private ProgramNode NormalizeProgram(ProgramNode ast, string fileName, List<Diagnostic> diagnostics)
    {
        var statements = ast.Statements
            .Select(statement => NormalizeStatement(statement, fileName, diagnostics))
            .ToList();

        return new ProgramNode(statements, ast.Location);
    }

    private AstNode NormalizeStatement(AstNode node, string fileName, List<Diagnostic> diagnostics)
    {
        switch (node)
        {
            case SuffixMethodCallNode methodNode:
                return NormalizeSuffixMethodCall(methodNode, fileName, diagnostics);

            case SuffixMethodChainNode chainNode:
                return NormalizeSuffixMethodChain(chainNode, fileName, diagnostics);

            case SuffixPropertyAccessNode propertyNode:
                return NormalizeSuffixPropertyAccess(propertyNode, fileName, diagnostics);

            case AssignmentNode assignment:
                return new AssignmentNode(
                    assignment.Name,
                    assignment.ExplicitType,
                    NormalizeExpression(assignment.Value, fileName, diagnostics),
                    assignment.IsDeclaration,
                    assignment.Location);

            case PrintNode printNode:
                return new PrintNode(
                    NormalizeExpression(printNode.Expression, fileName, diagnostics),
                    printNode.IsWriteLine,
                    printNode.Location);

            case ReturnNode returnNode:
                return new ReturnNode(
                    returnNode.Expression is null ? null : NormalizeExpression(returnNode.Expression, fileName, diagnostics),
                    returnNode.Location);

            case BlockNode blockNode:
                return new BlockNode(
                    blockNode.Statements.Select(child => NormalizeStatement(child, fileName, diagnostics)).ToList(),
                    blockNode.Location);

            case IfNode ifNode:
                return new IfNode(
                    NormalizeExpression(ifNode.Condition, fileName, diagnostics),
                    (BlockNode)NormalizeStatement(ifNode.ThenBlock, fileName, diagnostics),
                    ifNode.ElseIfClauses
                        .Select(clause =>
                            (
                                Condition: NormalizeExpression(clause.Condition, fileName, diagnostics),
                                Block: (BlockNode)NormalizeStatement(clause.Block, fileName, diagnostics)
                            ))
                        .ToList(),
                    ifNode.ElseBlock is null ? null : (BlockNode)NormalizeStatement(ifNode.ElseBlock, fileName, diagnostics),
                    ifNode.Location);

            case WhileNode whileNode:
                return new WhileNode(
                    NormalizeExpression(whileNode.Condition, fileName, diagnostics),
                    (BlockNode)NormalizeStatement(whileNode.Body, fileName, diagnostics),
                    whileNode.Location);

            case ForNode forNode:
                return new ForNode(
                    forNode.Initializer is null ? null : NormalizeExpression(forNode.Initializer, fileName, diagnostics),
                    forNode.Condition is null ? null : NormalizeExpression(forNode.Condition, fileName, diagnostics),
                    forNode.Step is null ? null : NormalizeExpression(forNode.Step, fileName, diagnostics),
                    (BlockNode)NormalizeStatement(forNode.Body, fileName, diagnostics),
                    forNode.Location);

            case FunctionDefinitionNode functionDefinitionNode:
                return new FunctionDefinitionNode(
                    functionDefinitionNode.Name,
                    functionDefinitionNode.Parameters,
                    functionDefinitionNode.ReturnType,
                    (BlockNode)NormalizeStatement(functionDefinitionNode.Body, fileName, diagnostics),
                    functionDefinitionNode.Location);

            default:
                return NormalizeExpression(node, fileName, diagnostics);
        }
    }

    private AstNode NormalizeExpression(AstNode node, string fileName, List<Diagnostic> diagnostics)
    {
        return node switch
        {
            BinaryExpressionNode binary => new BinaryExpressionNode(
                NormalizeExpression(binary.Left, fileName, diagnostics),
                binary.Operator,
                NormalizeExpression(binary.Right, fileName, diagnostics),
                binary.Location),

            UnaryExpressionNode unary => new UnaryExpressionNode(
                unary.Operator,
                NormalizeExpression(unary.Operand, fileName, diagnostics),
                unary.Location),

            FunctionCallNode functionCall => new FunctionCallNode(
                functionCall.FunctionName,
                functionCall.Arguments.Select(arg => NormalizeExpression(arg, fileName, diagnostics)).ToList(),
                functionCall.Location),

            SuffixedExpressionNode suffixed => new SuffixedExpressionNode(
                NormalizeExpression(suffixed.Expression, fileName, diagnostics),
                suffixed.SuffixText,
                suffixed.ResolvedCase,
                suffixed.Location),

            SuffixPropertyAccessNode propertyNode => NormalizeSuffixPropertyAccess(propertyNode, fileName, diagnostics),

            _ => node
        };
    }

    private SuffixMethodCallNode NormalizeSuffixMethodCall(
        SuffixMethodCallNode node,
        string fileName,
        List<Diagnostic> diagnostics)
    {
        var suffixCase = node.SuffixCase;
        var resolvedMethod = node.ResolvedMethod;

        if (string.IsNullOrWhiteSpace(suffixCase))
        {
            suffixCase = _suffixResolver.TryInferCaseFromVerb(node.Verb);
            if (!string.IsNullOrWhiteSpace(suffixCase))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Info,
                    DiagnosticCodes.MorphologyCandidateDetected,
                    $"Morfoloji normalizasyonu uygulandı: '{node.Verb}' ifadesinden {DescribeSuffixCase(suffixCase)} durumu çıkarıldı.",
                    fileName,
                    node.Location.Line,
                    node.Location.Column));
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedMethod) && !string.IsNullOrWhiteSpace(suffixCase))
        {
            resolvedMethod = _suffixResolver.ResolveVerbMethodFromCase(suffixCase, node.Verb);
        }

        var arguments = node.Arguments
            .Select(arg => NormalizeExpression(arg, fileName, diagnostics))
            .ToList();

        return new SuffixMethodCallNode(
            node.TargetToken,
            node.TargetStem,
            node.Verb,
            suffixCase,
            resolvedMethod,
            arguments,
            node.Location);
    }

    private SuffixMethodChainNode NormalizeSuffixMethodChain(
        SuffixMethodChainNode node,
        string fileName,
        List<Diagnostic> diagnostics)
    {
        var suffixCase = node.SuffixCase;
        if (string.IsNullOrWhiteSpace(suffixCase) && node.Steps.Count > 0)
        {
            suffixCase = _suffixResolver.TryInferCaseFromVerb(node.Steps[0].Verb);
            if (string.IsNullOrWhiteSpace(suffixCase) && !string.IsNullOrWhiteSpace(node.TailPropertyWord))
            {
                suffixCase = _suffixResolver.TryInferCaseFromProperty(node.TailPropertyWord);
            }

            if (!string.IsNullOrWhiteSpace(suffixCase))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Info,
                    DiagnosticCodes.MorphologyCandidateDetected,
                    $"Morfoloji normalizasyonu uygulandı: zincir ifadesi için {DescribeSuffixCase(suffixCase)} durumu çıkarıldı.",
                    fileName,
                    node.Location.Line,
                    node.Location.Column));
            }
        }

        var steps = node.Steps
            .Select(step =>
            {
                var resolvedMethod = step.ResolvedMethod;
                if (string.IsNullOrWhiteSpace(resolvedMethod) && !string.IsNullOrWhiteSpace(suffixCase))
                {
                    resolvedMethod = _suffixResolver.ResolveVerbMethodFromCase(suffixCase, step.Verb);
                }

                return new SuffixMethodChainStep(
                    step.Verb,
                    resolvedMethod,
                    NormalizeExpression(step.Argument, fileName, diagnostics),
                    step.Location);
            })
            .ToList();

        var tailResolvedMember = node.TailResolvedMember;
        if (string.IsNullOrWhiteSpace(tailResolvedMember)
            && !string.IsNullOrWhiteSpace(node.TailPropertyWord)
            && !string.IsNullOrWhiteSpace(suffixCase))
        {
            tailResolvedMember = _suffixResolver.ResolvePropertyMemberFromCase(suffixCase, node.TailPropertyWord);
        }

        return new SuffixMethodChainNode(
            node.TargetToken,
            node.TargetStem,
            suffixCase,
            steps,
            node.TailPropertyWord,
            tailResolvedMember,
            node.TailIsWriteLine,
            node.Location);
    }

    private SuffixPropertyAccessNode NormalizeSuffixPropertyAccess(
        SuffixPropertyAccessNode node,
        string fileName,
        List<Diagnostic> diagnostics)
    {
        var suffixCase = node.SuffixCase;
        var resolvedMember = node.ResolvedMember;

        if (string.IsNullOrWhiteSpace(suffixCase))
        {
            suffixCase = _suffixResolver.TryInferCaseFromProperty(node.PropertyWord);
            if (!string.IsNullOrWhiteSpace(suffixCase))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Info,
                    DiagnosticCodes.MorphologyCandidateDetected,
                    $"Morfoloji normalizasyonu uygulandı: '{node.PropertyWord}' ifadesinden {DescribeSuffixCase(suffixCase)} durumu çıkarıldı.",
                    fileName,
                    node.Location.Line,
                    node.Location.Column));
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedMember) && !string.IsNullOrWhiteSpace(suffixCase))
        {
            resolvedMember = _suffixResolver.ResolvePropertyMemberFromCase(suffixCase, node.PropertyWord);
        }

        return new SuffixPropertyAccessNode(
            node.TargetToken,
            node.TargetStem,
            node.PropertyWord,
            suffixCase,
            resolvedMember,
            node.Location);
    }

    private void CollectAstDiagnostics(ProgramNode ast, string fileName, List<Diagnostic> diagnostics)
    {
        foreach (var statement in ast.Statements)
        {
            CollectStatement(statement, fileName, diagnostics);
        }
    }

    private void CollectStatement(AstNode node, string fileName, List<Diagnostic> diagnostics)
    {
        if (node is SuffixMethodCallNode suffixNode)
        {
            AddRuntimeDiagnosticIfNeeded(suffixNode.TargetToken, suffixNode.Location, fileName, diagnostics);

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
                    BuildMappingMissingMessage(suffixNode.TargetToken, suffixNode.Verb, suffixNode.SuffixCase, isProperty: false),
                    fileName,
                    suffixNode.Location.Line,
                    suffixNode.Location.Column));
            }
        }

        if (node is SuffixPropertyAccessNode propertyNode)
        {
            AddRuntimeDiagnosticIfNeeded(propertyNode.TargetToken, propertyNode.Location, fileName, diagnostics);

            if (string.IsNullOrWhiteSpace(propertyNode.ResolvedMember))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.MorphologyMappingMissing,
                    BuildMappingMissingMessage(propertyNode.TargetToken, propertyNode.PropertyWord, propertyNode.SuffixCase, isProperty: true),
                    fileName,
                    propertyNode.Location.Line,
                    propertyNode.Location.Column));
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

    private void AddRuntimeDiagnosticIfNeeded(string targetToken, SourceLocation location, string fileName, List<Diagnostic> diagnostics)
    {
        var analysis = _suffixResolver.AnalyzeCase(targetToken);
        if (string.IsNullOrWhiteSpace(analysis.MorphologyFailureMessage))
            return;

        diagnostics.Add(new Diagnostic(
            string.IsNullOrWhiteSpace(analysis.SuffixCase) ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
            DiagnosticCodes.MorphologyRuntimeUnavailable,
            string.IsNullOrWhiteSpace(analysis.SuffixCase)
                ? $"Zemberek kullanılamadı ve '{targetToken}' için sonek durumu çözümlenemedi: {analysis.MorphologyFailureMessage}"
                : $"Zemberek kullanılamadı, '{targetToken}' için ek belirteçleriyle devam edildi: {analysis.MorphologyFailureMessage}",
            fileName,
            location.Line,
            location.Column));
    }

    private string BuildMappingMissingMessage(string targetToken, string word, string? suffixCase, bool isProperty)
    {
        if (string.IsNullOrWhiteSpace(suffixCase))
            return $"Sonek çözümleme bulundu ancak sözlük eşlemesi yok: '{targetToken} {word}'.";

        var suggestions = isProperty
            ? _suffixResolver.GetKnownPropertiesForCase(suffixCase)
            : _suffixResolver.GetKnownVerbsForCase(suffixCase);

        var category = isProperty ? "özellikler" : "fiiller";
        var message = $"Sonek çözümleme bulundu ancak {DescribeSuffixCase(suffixCase)} durumu için sözlük eşlemesi yok: '{targetToken} {word}'.";

        if (suggestions.Count == 0)
            return message;

        return $"{message} Bilinen {category}: {string.Join(", ", suggestions)}.";
    }

    private static string DescribeSuffixCase(string suffixCase) => suffixCase switch
    {
        "dative" => "yönelme",
        "ablative" => "ayrılma",
        "genitive" => "ilgi",
        "locative" => "bulunma",
        "accusative" => "belirtme",
        _ => suffixCase
    };

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
