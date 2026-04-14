using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using GorSharp.Core.Ast;
using GorSharp.Morphology;

namespace GorSharp.Parser;

/// <summary>
/// Visits the ANTLR parse tree and builds our AST.
/// </summary>
public class AstBuildingVisitor : GorSharpBaseVisitor<AstNode>
{
    private readonly SuffixResolver _suffixResolver;
    private readonly List<GorSharp.Core.Diagnostics.Diagnostic>? _diagnostics;
    private readonly string _fileName;
    private readonly GorSharpParsingOptions _options;

    public AstBuildingVisitor(
        SuffixResolver? suffixResolver = null,
        List<GorSharp.Core.Diagnostics.Diagnostic>? diagnostics = null,
        string fileName = "<giriş>",
        GorSharpParsingOptions? options = null)
    {
        _suffixResolver = suffixResolver ?? new SuffixResolver(new GorSharp.Core.Sozluk.SozlukData());
        _diagnostics = diagnostics;
        _fileName = fileName;
        _options = options ?? new GorSharpParsingOptions();
    }

    private static SourceLocation Loc(ParserRuleContext ctx) =>
        new(ctx.Start.Line, ctx.Start.Column, ctx.Stop?.StopIndex - ctx.Start.StartIndex + 1 ?? 0);

    private static SourceLocation Loc(IToken token) =>
        new(token.Line, token.Column, token.Text.Length);

    public override AstNode VisitProgram(GorSharpParser.ProgramContext ctx)
    {
        var statements = ctx.statement().Select(s => Visit(s)).ToList();
        return new ProgramNode(statements, Loc(ctx));
    }

    // ── Assignment ──────────────────────────────────────────────

    public override AstNode VisitTypedDeclaration(GorSharpParser.TypedDeclarationContext ctx)
    {
        ReportStrictModeParticle(ctx.declarationParticle()?.Start);
        var name = ctx.IDENTIFIER().GetText();
        var type = ctx.typeName().GetText();
        var value = Visit(ctx.expression());
        return new AssignmentNode(name, type, value, isDeclaration: true, Loc(ctx));
    }

    public override AstNode VisitInferredDeclaration(GorSharpParser.InferredDeclarationContext ctx)
    {
        ReportStrictModeParticle(ctx.declarationParticle()?.Start);
        var name = ctx.IDENTIFIER().GetText();
        var value = Visit(ctx.expression());
        return new AssignmentNode(name, null, value, isDeclaration: true, Loc(ctx));
    }

    public override AstNode VisitEqualsAssignment(GorSharpParser.EqualsAssignmentContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        var value = Visit(ctx.expression());
        return new AssignmentNode(name, null, value, isDeclaration: false, Loc(ctx));
    }

    // ── Print ───────────────────────────────────────────────────

    public override AstNode VisitWriteStatement(GorSharpParser.WriteStatementContext ctx)
    {
        var expr = Visit(ctx.expression());
        var suffixToken = ctx.IDENTIFIER();
        if (suffixToken is not null)
        {
            expr = WrapWithSuffix(expr, suffixToken, ctx);
        }
        return new PrintNode(expr, isWriteLine: false, Loc(ctx));
    }

    public override AstNode VisitWriteLineStatement(GorSharpParser.WriteLineStatementContext ctx)
    {
        var expr = Visit(ctx.expression());
        var suffixToken = ctx.IDENTIFIER();
        if (suffixToken is not null)
        {
            expr = WrapWithSuffix(expr, suffixToken, ctx);
        }
        return new PrintNode(expr, isWriteLine: true, Loc(ctx));
    }

    /// <summary>
    /// Wraps the expression in a SuffixedExpressionNode with resolved case information.
    /// </summary>
    private AstNode WrapWithSuffix(AstNode expr, Antlr4.Runtime.Tree.ITerminalNode suffixToken, ParserRuleContext ctx)
    {
        var suffixText = suffixToken.GetText();
        var resolvedCase = _suffixResolver.DetectCaseFromBareSuffix(suffixText);

        // Diagnostic if suffix is not recognized
        if (string.IsNullOrWhiteSpace(resolvedCase))
        {
            var symbol = suffixToken.Symbol;
            _diagnostics?.Add(new GorSharp.Core.Diagnostics.Diagnostic(
                GorSharp.Core.Diagnostics.DiagnosticSeverity.Error,
                "GOR1007",
                $"Tanınmayan ek: '{suffixText}'",
                _fileName,
                symbol.Line,
                symbol.Column));
        }

        return new SuffixedExpressionNode(expr, suffixText, resolvedCase, Loc(ctx));
    }

    public override AstNode VisitSuffixMethodStatement(GorSharpParser.SuffixMethodStatementContext ctx)
    {
        var targetToken = ctx.IDENTIFIER(0).GetText();
        var expressions = ctx.expression();
        var identifiers = ctx.IDENTIFIER().ToArray();

        var hasTailPrint = ctx.YAZDIR() is not null || ctx.YENISATIRA_YAZDIR() is not null;
        var tailPropertyWord = hasTailPrint ? identifiers[^1].GetText() : null;
        var tailIsWriteLine = ctx.YENISATIRA_YAZDIR() is not null;

        var verbs = hasTailPrint
            ? identifiers.Skip(1).Take(identifiers.Length - 2).ToArray()
            : identifiers.Skip(1).ToArray();

        var firstVerb = verbs[0].GetText();
        var firstArgument = Visit(expressions[0]);
        var firstResolution = _suffixResolver.Resolve(targetToken, firstVerb);
        var stem = firstResolution?.Stem ?? targetToken;
        var suffixCase = firstResolution?.SuffixCase;

        if (verbs.Length == 1 && !hasTailPrint)
        {
            if (string.IsNullOrWhiteSpace(firstResolution?.CSharpMethod))
                ReportMorphologyMappingMissing(targetToken, firstVerb, firstResolution?.SuffixCase, Loc(verbs[0].Symbol));

            return new SuffixMethodCallNode(
                targetToken,
                stem,
                firstVerb,
                suffixCase,
                firstResolution?.CSharpMethod,
                [firstArgument],
                Loc(ctx));
        }

        var steps = new List<SuffixMethodChainStep>
        {
            new(
                firstVerb,
                firstResolution?.CSharpMethod,
                firstArgument,
                Loc(verbs[0].Symbol))
        };

        for (int i = 1; i < verbs.Length; i++)
        {
            var verb = verbs[i].GetText();
            var argument = Visit(expressions[i]);
            string? resolvedMethod;

            if (suffixCase is not null)
            {
                resolvedMethod = _suffixResolver.ResolveVerbMethodFromCase(suffixCase, verb);
            }
            else
            {
                var resolution = _suffixResolver.Resolve(targetToken, verb);
                suffixCase = resolution?.SuffixCase;
                resolvedMethod = resolution?.CSharpMethod;
            }

            if (string.IsNullOrWhiteSpace(resolvedMethod))
                ReportMorphologyMappingMissing(targetToken, verb, suffixCase, Loc(verbs[i].Symbol));

            steps.Add(new SuffixMethodChainStep(
                verb,
                resolvedMethod,
                argument,
                Loc(verbs[i].Symbol)));
        }

        string? tailResolvedMember = null;
        if (hasTailPrint && tailPropertyWord is not null)
        {
            if (suffixCase is not null)
            {
                tailResolvedMember = _suffixResolver.ResolvePropertyMemberFromCase(suffixCase, tailPropertyWord);
            }

            if (string.IsNullOrWhiteSpace(tailResolvedMember) && suffixCase is null)
            {
                var tailResolution = _suffixResolver.ResolveProperty(targetToken, tailPropertyWord);
                suffixCase = tailResolution?.SuffixCase;
                tailResolvedMember = tailResolution?.CSharpMember;
            }

            if (string.IsNullOrWhiteSpace(tailResolvedMember))
                tailResolvedMember = _suffixResolver.ResolvePropertyMember(tailPropertyWord);

            if (string.IsNullOrWhiteSpace(tailResolvedMember))
                ReportMorphologyMappingMissing(targetToken, tailPropertyWord, suffixCase, Loc(identifiers[^1].Symbol), isProperty: true);
        }

        return new SuffixMethodChainNode(
            targetToken,
            stem,
            suffixCase,
            steps,
            tailPropertyWord,
            tailResolvedMember,
            tailIsWriteLine,
            Loc(ctx));
    }

    // ── Expressions ─────────────────────────────────────────────

    public override AstNode VisitOrExpression(GorSharpParser.OrExpressionContext ctx)
    {
        var exprs = ctx.andExpression();
        var result = Visit(exprs[0]);
        for (int i = 1; i < exprs.Length; i++)
        {
            var right = Visit(exprs[i]);
            result = new BinaryExpressionNode(result, BinaryOperator.Veya, right, Loc(ctx));
        }
        return result;
    }

    public override AstNode VisitAndExpression(GorSharpParser.AndExpressionContext ctx)
    {
        var exprs = ctx.norExpression();
        var result = Visit(exprs[0]);
        for (int i = 1; i < exprs.Length; i++)
        {
            var right = Visit(exprs[i]);
            result = new BinaryExpressionNode(result, BinaryOperator.Ve, right, Loc(ctx));
        }
        return result;
    }

    public override AstNode VisitNeitherNorExpression(GorSharpParser.NeitherNorExpressionContext ctx)
    {
        var left = Visit(ctx.equalityExpression(0));
        var right = Visit(ctx.equalityExpression(1));

        var notLeft = new UnaryExpressionNode(UnaryOperator.Degil, left, Loc(ctx));
        var notRight = new UnaryExpressionNode(UnaryOperator.Degil, right, Loc(ctx));
        return new BinaryExpressionNode(notLeft, BinaryOperator.Ve, notRight, Loc(ctx));
    }

    public override AstNode VisitNorPrimaryExpression(GorSharpParser.NorPrimaryExpressionContext ctx)
    {
        return Visit(ctx.equalityExpression());
    }

    public override AstNode VisitEqualityExpression(GorSharpParser.EqualityExpressionContext ctx)
    {
        var exprs = ctx.comparisonExpression();
        var result = Visit(exprs[0]);
        for (int i = 1; i < exprs.Length; i++)
        {
            var op = ctx.GetChild(2 * i - 1).GetText() switch
            {
                "eşittir" or "e\u015fittir" => BinaryOperator.Esittir,
                _ => BinaryOperator.EsitDegildir
            };
            var right = Visit(exprs[i]);
            result = new BinaryExpressionNode(result, op, right, Loc(ctx));
        }
        return result;
    }

    public override AstNode VisitStandardComparisonExpr(GorSharpParser.StandardComparisonExprContext ctx)
    {
        var exprs = ctx.additiveExpression();
        var result = Visit(exprs[0]);
        for (int i = 1; i < exprs.Length; i++)
        {
            var opText = ctx.GetChild(2 * i - 1).GetText();
            var op = MapComparisonOp(opText);
            var right = Visit(exprs[i]);
            result = new BinaryExpressionNode(result, op, right, Loc(ctx));
        }
        return result;
    }

    public override AstNode VisitAblativeComparisonExpr(GorSharpParser.AblativeComparisonExprContext ctx)
    {
        var left = Visit(ctx.additiveExpression());
        var ablativeNumber = ctx.ABLATIVE_NUMBER().GetText();
        var resolution = _suffixResolver.DetectAblativeNumber(ablativeNumber)
            ?? throw new InvalidOperationException($"Geçersiz ayrılma karşılaştırması: {ablativeNumber}");

        var right = new LiteralNode(resolution.Value, LiteralType.Integer, Loc(ctx.ABLATIVE_NUMBER().Symbol));

        var hasEsit = ctx.ESIT() is not null;
        BinaryOperator op;
        if (ctx.BUYUK() is not null)
        {
            op = hasEsit ? BinaryOperator.BuyukEsittir : BinaryOperator.Buyuktur;
        }
        else
        {
            op = hasEsit ? BinaryOperator.KucukEsittir : BinaryOperator.Kucuktur;
        }

        return new BinaryExpressionNode(left, op, right, Loc(ctx));
    }

    public override AstNode VisitAdditiveExpression(GorSharpParser.AdditiveExpressionContext ctx)
    {
        var exprs = ctx.multiplicativeExpression();
        var result = Visit(exprs[0]);
        for (int i = 1; i < exprs.Length; i++)
        {
            var op = ctx.GetChild(2 * i - 1).GetText() == "+" ? BinaryOperator.Add : BinaryOperator.Subtract;
            var right = Visit(exprs[i]);
            result = new BinaryExpressionNode(result, op, right, Loc(ctx));
        }
        return result;
    }

    public override AstNode VisitMultiplicativeExpression(GorSharpParser.MultiplicativeExpressionContext ctx)
    {
        var exprs = ctx.unaryExpression();
        var result = Visit(exprs[0]);
        for (int i = 1; i < exprs.Length; i++)
        {
            var op = ctx.GetChild(2 * i - 1).GetText() switch
            {
                "*" => BinaryOperator.Multiply,
                "/" => BinaryOperator.Divide,
                _ => BinaryOperator.Modulo
            };
            var right = Visit(exprs[i]);
            result = new BinaryExpressionNode(result, op, right, Loc(ctx));
        }
        return result;
    }

    // ── Unary ───────────────────────────────────────────────────

    public override AstNode VisitNotExpression(GorSharpParser.NotExpressionContext ctx)
    {
        var operand = Visit(ctx.unaryExpression());
        return new UnaryExpressionNode(UnaryOperator.Degil, operand, Loc(ctx));
    }

    public override AstNode VisitNegateExpression(GorSharpParser.NegateExpressionContext ctx)
    {
        var operand = Visit(ctx.unaryExpression());
        return new UnaryExpressionNode(UnaryOperator.Negate, operand, Loc(ctx));
    }

    public override AstNode VisitPrimaryExpression(GorSharpParser.PrimaryExpressionContext ctx)
    {
        return Visit(ctx.primary());
    }

    // ── Primary ─────────────────────────────────────────────────

    public override AstNode VisitIntLiteralExpr(GorSharpParser.IntLiteralExprContext ctx)
    {
        return new LiteralNode(int.Parse(ctx.INTEGER_LITERAL().GetText()), LiteralType.Integer, Loc(ctx.Start));
    }

    public override AstNode VisitDoubleLiteralExpr(GorSharpParser.DoubleLiteralExprContext ctx)
    {
        return new LiteralNode(double.Parse(ctx.DOUBLE_LITERAL().GetText(), System.Globalization.CultureInfo.InvariantCulture), LiteralType.Double, Loc(ctx.Start));
    }

    public override AstNode VisitStringLiteralExpr(GorSharpParser.StringLiteralExprContext ctx)
    {
        var text = ctx.STRING_LITERAL().GetText();
        return new LiteralNode(text[1..^1], LiteralType.String, Loc(ctx.Start));
    }

    public override AstNode VisitTrueLiteralExpr(GorSharpParser.TrueLiteralExprContext ctx)
    {
        return new LiteralNode(true, LiteralType.Boolean, Loc(ctx.Start));
    }

    public override AstNode VisitFalseLiteralExpr(GorSharpParser.FalseLiteralExprContext ctx)
    {
        return new LiteralNode(false, LiteralType.Boolean, Loc(ctx.Start));
    }

    public override AstNode VisitTrueAliasLiteralExpr(GorSharpParser.TrueAliasLiteralExprContext ctx)
    {
        return new LiteralNode(true, LiteralType.Boolean, Loc(ctx.Start));
    }

    public override AstNode VisitFalseAliasLiteralExpr(GorSharpParser.FalseAliasLiteralExprContext ctx)
    {
        return new LiteralNode(false, LiteralType.Boolean, Loc(ctx.Start));
    }

    public override AstNode VisitNullLiteralExpr(GorSharpParser.NullLiteralExprContext ctx)
    {
        return new LiteralNode(null, LiteralType.Null, Loc(ctx.Start));
    }

    public override AstNode VisitFunctionCallExpr(GorSharpParser.FunctionCallExprContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        var args = ctx.argList()?.expression().Select(e => Visit(e)).ToList() ?? [];
        return new FunctionCallNode(name, args, Loc(ctx.IDENTIFIER().Symbol));
    }

    public override AstNode VisitSuffixPropertyExpr(GorSharpParser.SuffixPropertyExprContext ctx)
    {
        var targetToken = ctx.IDENTIFIER(0).GetText();
        var propertyWord = ctx.IDENTIFIER(1).GetText();

        var resolution = _suffixResolver.ResolveProperty(targetToken, propertyWord);
        var stem = resolution?.Stem ?? targetToken;
        var suffixCase = resolution?.SuffixCase;
        var resolvedMember = resolution?.CSharpMember;

        return new SuffixPropertyAccessNode(
            targetToken,
            stem,
            propertyWord,
            suffixCase,
            resolvedMember,
            Loc(ctx));
    }

    public override AstNode VisitIdentifierExpr(GorSharpParser.IdentifierExprContext ctx)
    {
        return new IdentifierNode(ctx.IDENTIFIER().GetText(), Loc(ctx.IDENTIFIER().Symbol));
    }

    public override AstNode VisitParenExpr(GorSharpParser.ParenExprContext ctx)
    {
        return Visit(ctx.expression());
    }

    // ── If / Else ───────────────────────────────────────────────

    public override AstNode VisitIfStatement(GorSharpParser.IfStatementContext ctx)
    {
        foreach (var particle in ctx.conditionParticle())
        {
            ReportStrictModeParticle(particle.Start);
        }

        var expressions = ctx.expression();
        var blocks = ctx.block();

        // First expression + block = the main if
        var condition = Visit(expressions[0]);
        var thenBlock = (BlockNode)Visit(blocks[0]);

        // Else-if clauses: expressions[1..n-1] + blocks[1..n-1]
        var elseIfClauses = new List<(AstNode Condition, BlockNode Block)>();
        for (int i = 1; i < expressions.Length; i++)
        {
            elseIfClauses.Add((Visit(expressions[i]), (BlockNode)Visit(blocks[i])));
        }

        // Else block is the last block if there are more blocks than expressions
        BlockNode? elseBlock = blocks.Length > expressions.Length
            ? (BlockNode)Visit(blocks[^1])
            : null;

        return new IfNode(condition, thenBlock, elseIfClauses, elseBlock, Loc(ctx));
    }

    // ── While Loop ──────────────────────────────────────────────

    public override AstNode VisitWhileStatement(GorSharpParser.WhileStatementContext ctx)
    {
        ReportStrictModeParticle(ctx.loopParticle()?.Start);
        var condition = Visit(ctx.expression());
        var body = (BlockNode)Visit(ctx.block());
        return new WhileNode(condition, body, Loc(ctx));
    }

    // ── For Loop ────────────────────────────────────────────────

    public override AstNode VisitForStatement(GorSharpParser.ForStatementContext ctx)
    {
        var init = ctx.forInit() is { } fi ? Visit(fi) : null;
        var condition = ctx.expression() is { } expr ? Visit(expr) : null;
        var update = ctx.forUpdate() is { } fu ? VisitForUpdateAsAssignment(fu) : null;
        var body = (BlockNode)Visit(ctx.block());
        return new ForNode(init, condition, update, body, Loc(ctx));
    }

    public override AstNode VisitForTypedInit(GorSharpParser.ForTypedInitContext ctx)
    {
        ReportStrictModeParticle(ctx.declarationParticle()?.Start);
        var name = ctx.IDENTIFIER().GetText();
        var type = ctx.typeName().GetText();
        var value = Visit(ctx.expression());
        return new AssignmentNode(name, type, value, isDeclaration: true, Loc(ctx));
    }

    public override AstNode VisitForInferredInit(GorSharpParser.ForInferredInitContext ctx)
    {
        ReportStrictModeParticle(ctx.declarationParticle()?.Start);
        var name = ctx.IDENTIFIER().GetText();
        var value = Visit(ctx.expression());
        return new AssignmentNode(name, null, value, isDeclaration: true, Loc(ctx));
    }

    public override AstNode VisitForEqualsInit(GorSharpParser.ForEqualsInitContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        var value = Visit(ctx.expression());
        return new AssignmentNode(name, null, value, isDeclaration: false, Loc(ctx));
    }

    private AstNode VisitForUpdateAsAssignment(GorSharpParser.ForUpdateContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        var value = Visit(ctx.expression());
        return new AssignmentNode(name, null, value, isDeclaration: false, Loc(ctx));
    }

    // ── Function Definition ─────────────────────────────────────

    public override AstNode VisitFunctionDefinition(GorSharpParser.FunctionDefinitionContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        var parameters = new List<(string Name, string Type)>();

        if (ctx.paramList() is { } paramList)
        {
            foreach (var p in paramList.param())
            {
                parameters.Add((p.IDENTIFIER().GetText(), p.typeName().GetText()));
            }
        }

        var returnType = ctx.typeName()?.GetText();
        var body = (BlockNode)Visit(ctx.block());
        return new FunctionDefinitionNode(name, parameters, returnType, body, Loc(ctx.IDENTIFIER().Symbol));
    }

    // ── Return / Break / Continue ───────────────────────────────

    public override AstNode VisitReturnStatement(GorSharpParser.ReturnStatementContext ctx)
    {
        var expr = ctx.expression() is { } e ? Visit(e) : null;
        return new ReturnNode(expr, Loc(ctx));
    }

    public override AstNode VisitBreakStatement(GorSharpParser.BreakStatementContext ctx)
    {
        return new BreakNode(Loc(ctx));
    }

    public override AstNode VisitContinueStatement(GorSharpParser.ContinueStatementContext ctx)
    {
        return new ContinueNode(Loc(ctx));
    }

    // ── Block ───────────────────────────────────────────────────

    public override AstNode VisitBlock(GorSharpParser.BlockContext ctx)
    {
        var statements = ctx.statement().Select(s => Visit(s)).ToList();
        return new BlockNode(statements, Loc(ctx));
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static BinaryOperator MapComparisonOp(string text)
    {
        // Normalize Turkish chars
        if (text.Contains("büyükE") || text.Contains("b\u00fcy\u00fckE"))
            return BinaryOperator.BuyukEsittir;
        if (text.Contains("küçükE") || text.Contains("k\u00fc\u00e7\u00fckE"))
            return BinaryOperator.KucukEsittir;
        if (text.Contains("büyük") || text.Contains("b\u00fcy\u00fck"))
            return BinaryOperator.Buyuktur;
        if (text.Contains("küçük") || text.Contains("k\u00fc\u00e7\u00fck"))
            return BinaryOperator.Kucuktur;

        throw new InvalidOperationException($"Bilinmeyen karşılaştırma operatörü: {text}");
    }

    private void ReportStrictModeParticle(IToken? token)
    {
        if (_options.NaturalMode || token is null)
        {
            return;
        }

        _diagnostics?.Add(new GorSharp.Core.Diagnostics.Diagnostic(
            GorSharp.Core.Diagnostics.DiagnosticSeverity.Error,
            GorSharp.Core.Diagnostics.DiagnosticCodes.StrictModeNaturalParticle,
            $"Doğal dil parçacığı sıkı modda kullanılamaz: '{token.Text}'.",
            _fileName,
            token.Line,
            token.Column));
    }

    private void ReportMorphologyMappingMissing(string targetToken, string word, string? suffixCase, SourceLocation loc, bool isProperty = false)
    {
        if (!_suffixResolver.HasConfiguredSuffixMappings)
        {
            return;
        }

        var message = string.IsNullOrWhiteSpace(suffixCase)
            ? $"Sonek çözümleme bulundu ancak sözlük eşlemesi yok: '{targetToken} {word}'."
            : BuildMorphologyMappingMissingMessage(targetToken, word, suffixCase!, isProperty);

        _diagnostics?.Add(new GorSharp.Core.Diagnostics.Diagnostic(
            GorSharp.Core.Diagnostics.DiagnosticSeverity.Warning,
            GorSharp.Core.Diagnostics.DiagnosticCodes.MorphologyMappingMissing,
            message,
            _fileName,
            loc.Line,
            loc.Column));
    }

    private string BuildMorphologyMappingMissingMessage(string targetToken, string word, string suffixCase, bool isProperty)
    {
        var suggestions = isProperty
            ? _suffixResolver.GetKnownPropertiesForCase(suffixCase)
            : _suffixResolver.GetKnownVerbsForCase(suffixCase);

        var category = isProperty ? "özellikler" : "fiiller";
        var baseMessage = $"Sonek çözümleme bulundu ancak {DescribeSuffixCase(suffixCase)} durumu için sözlük eşlemesi yok: '{targetToken} {word}'.";

        if (suggestions.Count == 0)
            return baseMessage;

        return $"{baseMessage} Bilinen {category}: {string.Join(", ", suggestions)}.";
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
}
