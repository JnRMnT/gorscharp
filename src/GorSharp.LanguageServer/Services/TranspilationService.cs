using GorSharp.Core.Ast;
using GorSharp.Core.Diagnostics;
using GorSharp.Core.Sozluk;
using GorSharp.Morphology;
using GorSharp.Morphology.Normalization;
using GorSharp.Parser;
using GorSharp.Transpiler;

namespace GorSharp.LanguageServer.Services;

/// <summary>
/// Encapsulates the full Gör# → C# transpilation pipeline for LSP use.
/// </summary>
public class TranspilationService
{
    private readonly BuiltInSignaturesService _builtInSignatures = new();
    private readonly SymbolAnalysisService _symbolAnalysis;
    private readonly ParsingModeService _parsingMode;
    private readonly IMorphologyNormalizationPass _morphologyNormalization;
    private readonly SozlukService _sozlukService = new();
    private SozlukData? _sozluk;

    public TranspilationService(ParsingModeService parsingMode)
    {
        _parsingMode = parsingMode;
        _symbolAnalysis = new SymbolAnalysisService(_builtInSignatures);
        _morphologyNormalization = new ZemberekMorphologyNormalizationPass(
            new SuffixResolver(new SozlukData()),
            new MorphologyNormalizationOptions
            {
                Enabled = true,
                EmitCandidateDiagnostics = true
            });
    }

    public void LoadSozluk(string? sozlukPath)
    {
        if (sozlukPath is not null && File.Exists(sozlukPath))
        {
            _sozlukService.Load(sozlukPath);
            _sozluk = _sozlukService.Data;
        }
    }

    public TranspilationResult Transpile(string sourceCode, string fileName)
    {
        var parser = new GorSharpParserService(options: _parsingMode.Current);
        var (ast, diagnostics) = parser.Parse(sourceCode, fileName);

        var normalization = _morphologyNormalization.Normalize(ast, sourceCode, fileName);
        ast = normalization.Ast;
        diagnostics = diagnostics.Concat(normalization.Diagnostics).ToList();

        var symbolResult = _symbolAnalysis.AnalyzeAst(ast, diagnostics);
        diagnostics = diagnostics.Concat(CollectSemanticDiagnostics(symbolResult.Ast, symbolResult.Symbols, fileName, _builtInSignatures)).ToList();

        string? csharpCode = null;

        if (diagnostics.All(d => d.Severity != Core.Diagnostics.DiagnosticSeverity.Error))
        {
            try
            {
                var emitter = new CSharpEmitter(_sozluk);
                csharpCode = emitter.Emit(ast);
            }
            catch (Exception ex)
            {
                diagnostics = diagnostics.Append(new Core.Diagnostics.Diagnostic(
                    DiagnosticSeverity.Error,
                    "GOR0100",
                    $"Dönüştürme hatası: {ex.Message}",
                    fileName, 1, 0)).ToList();
            }
        }

        return new TranspilationResult(ast, csharpCode, diagnostics);
    }

    public SozlukData? Sozluk => _sozluk;

    private static IReadOnlyList<Core.Diagnostics.Diagnostic> CollectSemanticDiagnostics(
        ProgramNode ast,
        SymbolTable symbols,
        string fileName,
        BuiltInSignaturesService builtInSignatures)
    {
        var diagnostics = new List<Core.Diagnostics.Diagnostic>();

        // GOR2001: Undefined symbol references.
        foreach (var reference in symbols.References.Where(r => r.ResolvedDeclarationId is null))
        {
            var kindText = reference.Kind == GSymbolKind.Function ? "Fonksiyon" : "Değişken";
            diagnostics.Add(new Core.Diagnostics.Diagnostic(
                Core.Diagnostics.DiagnosticSeverity.Error,
                "GOR2001",
                $"{kindText} tanımlı değil: '{reference.Name}'.",
                fileName,
                reference.Location.Line,
                reference.Location.Column));
        }

        // GOR2002: Duplicate declarations in same scope.
        var valueKinds = new HashSet<GSymbolKind> { GSymbolKind.Variable, GSymbolKind.Parameter };
        var duplicateGroups = symbols.Declarations
            .GroupBy(d => new
            {
                d.ScopeId,
                d.Name,
                Domain = d.Kind == GSymbolKind.Function ? "Function" : (valueKinds.Contains(d.Kind) ? "Value" : d.Kind.ToString())
            })
            .Where(g => g.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            foreach (var duplicate in group.OrderBy(d => d.DeclarationOrder).Skip(1))
            {
                diagnostics.Add(new Core.Diagnostics.Diagnostic(
                    Core.Diagnostics.DiagnosticSeverity.Error,
                    "GOR2002",
                    $"Aynı kapsamda yinelenen tanım: '{duplicate.Name}'.",
                    fileName,
                    duplicate.Location.Line,
                    duplicate.Location.Column));
            }
        }

        // GOR2003: Function arity mismatch.
        var signaturesByDeclarationId = symbols.FunctionSignatures.ToDictionary(s => s.DeclarationId, s => s);
        foreach (var callRef in symbols.References.Where(r => r.Kind == GSymbolKind.Function && r.ResolvedDeclarationId is not null && r.ArgumentCount is not null))
        {
            if (!signaturesByDeclarationId.TryGetValue(callRef.ResolvedDeclarationId!.Value, out var signature))
                continue;

            var expected = signature.Parameters.Count;
            var actual = callRef.ArgumentCount!.Value;
            if (expected == actual)
                continue;

            diagnostics.Add(new Core.Diagnostics.Diagnostic(
                Core.Diagnostics.DiagnosticSeverity.Error,
                "GOR2003",
                $"Fonksiyon parametre sayısı uyuşmuyor: '{callRef.Name}' {expected} bekliyor, {actual} verildi.",
                fileName,
                callRef.Location.Line,
                callRef.Location.Column));
        }

        var semanticWalker = new SemanticTypeDiagnosticsWalker(symbols, builtInSignatures, fileName, diagnostics);
        semanticWalker.VisitProgram(ast);

        return diagnostics;
    }

    private sealed class SemanticTypeDiagnosticsWalker : IAstVisitor<object?>
    {
        private readonly SymbolTable _symbols;
        private readonly BuiltInSignaturesService _builtInSignatures;
        private readonly string _fileName;
        private readonly List<Core.Diagnostics.Diagnostic> _diagnostics;

        private int _currentScopeId = 1;
        private int _nextScopeId = 2;
        private string? _currentFunctionName;
        private string? _currentFunctionReturnType;

        public SemanticTypeDiagnosticsWalker(
            SymbolTable symbols,
            BuiltInSignaturesService builtInSignatures,
            string fileName,
            List<Core.Diagnostics.Diagnostic> diagnostics)
        {
            _symbols = symbols;
            _builtInSignatures = builtInSignatures;
            _fileName = fileName;
            _diagnostics = diagnostics;
        }

        public object? VisitProgram(ProgramNode node)
        {
            foreach (var statement in node.Statements)
                statement.Accept(this);

            return null;
        }

        public object? VisitAssignment(AssignmentNode node)
        {
            var valueType = InferType(node.Value);

            if (node.IsDeclaration && !string.IsNullOrWhiteSpace(node.ExplicitType))
            {
                if (!IsTypeAssignable(node.ExplicitType, valueType))
                {
                    AddDiagnostic(
                        "GOR2004",
                        $"Tür uyumsuzluğu: '{node.Name}' için '{node.ExplicitType}' bekleniyor, '{FormatType(valueType)}' verildi.",
                        node.Location);
                }
            }
            else if (!node.IsDeclaration)
            {
                var declaration = _symbols.ResolveVisibleDeclaration(node.Name, GSymbolKind.Variable, _currentScopeId);
                if (declaration is not null &&
                    !string.IsNullOrWhiteSpace(declaration.InferredType) &&
                    !IsTypeAssignable(declaration.InferredType, valueType))
                {
                    AddDiagnostic(
                        "GOR2004",
                        $"Atama türü uyumsuzluğu: '{node.Name}' değişkeni '{declaration.InferredType}' bekliyor, '{FormatType(valueType)}' verildi.",
                        node.Location);
                }
            }

            node.Value.Accept(this);
            return null;
        }

        public object? VisitLiteral(LiteralNode node) => null;

        public object? VisitIdentifier(IdentifierNode node) => null;

        public object? VisitBinaryExpression(BinaryExpressionNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            return null;
        }

        public object? VisitUnaryExpression(UnaryExpressionNode node)
        {
            node.Operand.Accept(this);
            return null;
        }

        public object? VisitPrint(PrintNode node)
        {
            node.Expression.Accept(this);
            return null;
        }

        public object? VisitBlock(BlockNode node)
        {
            var previousScopeId = _currentScopeId;
            _currentScopeId = CreateScope();

            foreach (var statement in node.Statements)
                statement.Accept(this);

            _currentScopeId = previousScopeId;
            return null;
        }

        public object? VisitIf(IfNode node)
        {
            ValidateCondition(node.Condition, node.Condition.Location);
            node.Condition.Accept(this);
            node.ThenBlock.Accept(this);

            foreach (var (condition, block) in node.ElseIfClauses)
            {
                ValidateCondition(condition, condition.Location);
                condition.Accept(this);
                block.Accept(this);
            }

            node.ElseBlock?.Accept(this);
            return null;
        }

        public object? VisitWhile(WhileNode node)
        {
            ValidateCondition(node.Condition, node.Condition.Location);
            node.Condition.Accept(this);
            node.Body.Accept(this);
            return null;
        }

        public object? VisitFor(ForNode node)
        {
            var previousScopeId = _currentScopeId;
            _currentScopeId = CreateScope();

            node.Initializer?.Accept(this);

            if (node.Condition is not null)
            {
                ValidateCondition(node.Condition, node.Condition.Location);
                node.Condition.Accept(this);
            }

            node.Step?.Accept(this);
            node.Body.Accept(this);

            _currentScopeId = previousScopeId;
            return null;
        }

        public object? VisitReturn(ReturnNode node)
        {
            var actualType = node.Expression is null ? "boş" : InferType(node.Expression);

            if (string.IsNullOrWhiteSpace(_currentFunctionReturnType))
            {
                if (node.Expression is not null)
                {
                    AddDiagnostic(
                        "GOR2006",
                        $"Dönüş türü belirtilmeyen '{_currentFunctionName ?? "fonksiyon"}' içinde değer döndürülemez.",
                        node.Location);
                }
            }
            else if (!IsTypeAssignable(_currentFunctionReturnType, actualType))
            {
                AddDiagnostic(
                    "GOR2006",
                    $"Dönüş türü uyuşmuyor: '{_currentFunctionName ?? "fonksiyon"}' '{_currentFunctionReturnType}' bekliyor, '{FormatType(actualType)}' döndürüyor.",
                    node.Location);
            }

            node.Expression?.Accept(this);
            return null;
        }

        public object? VisitBreak(BreakNode node) => null;

        public object? VisitContinue(ContinueNode node) => null;

        public object? VisitFunctionCall(FunctionCallNode node)
        {
            foreach (var argument in node.Arguments)
                argument.Accept(this);

            return null;
        }

        public object? VisitSuffixMethodCall(SuffixMethodCallNode node)
        {
            foreach (var arg in node.Arguments)
                arg.Accept(this);

            return null;
        }

        public object? VisitSuffixMethodChain(SuffixMethodChainNode node)
        {
            foreach (var step in node.Steps)
                step.Argument.Accept(this);

            return null;
        }

        public object? VisitSuffixPropertyAccess(SuffixPropertyAccessNode node)
        {
            return null;
        }

        public object? VisitFunctionDefinition(FunctionDefinitionNode node)
        {
            var previousScopeId = _currentScopeId;
            var previousFunctionName = _currentFunctionName;
            var previousFunctionReturnType = _currentFunctionReturnType;

            _currentScopeId = CreateScope();
            _currentFunctionName = node.Name;
            _currentFunctionReturnType = node.ReturnType;

            node.Body.Accept(this);

            _currentScopeId = previousScopeId;
            _currentFunctionName = previousFunctionName;
            _currentFunctionReturnType = previousFunctionReturnType;
            return null;
        }

        private int CreateScope() => _nextScopeId++;

        private void ValidateCondition(AstNode condition, SourceLocation location)
        {
            var conditionType = InferType(condition);
            if (string.IsNullOrWhiteSpace(conditionType) || NormalizeType(conditionType) is "mantık" or "t")
                return;

            AddDiagnostic(
                "GOR2005",
                $"Koşul ifadesi 'mantık' türünde olmalı, '{FormatType(conditionType)}' bulundu.",
                location);
        }

        private void AddDiagnostic(string code, string message, SourceLocation location)
        {
            _diagnostics.Add(new Core.Diagnostics.Diagnostic(
                Core.Diagnostics.DiagnosticSeverity.Error,
                code,
                message,
                _fileName,
                location.Line,
                location.Column));
        }

        private string? InferType(AstNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return literal.LiteralType switch
                    {
                        LiteralType.Integer => "sayı",
                        LiteralType.Double => "onlu",
                        LiteralType.String => "metin",
                        LiteralType.Boolean => "mantık",
                        LiteralType.Null => "boş",
                        _ => "T"
                    };

                case IdentifierNode identifier:
                    return _symbols.ResolveVisibleDeclaration(identifier.Name, GSymbolKind.Variable, _currentScopeId)?.InferredType;

                case FunctionCallNode functionCall:
                {
                    var declaration = _symbols.ResolveVisibleDeclaration(functionCall.FunctionName, GSymbolKind.Function, _currentScopeId);
                    if (declaration is not null)
                    {
                        var signature = _symbols.FunctionSignatures.FirstOrDefault(s => s.DeclarationId == declaration.Id);
                        if (!string.IsNullOrWhiteSpace(signature?.ReturnType))
                            return signature.ReturnType;
                    }

                    if (_builtInSignatures.TryGetBuiltInSignature(functionCall.FunctionName, out var builtInSignature) &&
                        !string.IsNullOrWhiteSpace(builtInSignature.ReturnType))
                    {
                        return builtInSignature.ReturnType;
                    }

                    return null;
                }

                case UnaryExpressionNode unary:
                {
                    var operandType = InferType(unary.Operand);
                    return unary.Operator switch
                    {
                        UnaryOperator.Degil => "mantık",
                        UnaryOperator.Negate when IsNumericType(operandType) => NormalizeType(operandType),
                        _ => null
                    };
                }

                case BinaryExpressionNode binary:
                {
                    var leftType = NormalizeType(InferType(binary.Left));
                    var rightType = NormalizeType(InferType(binary.Right));

                    return binary.Operator switch
                    {
                        BinaryOperator.Esittir or
                        BinaryOperator.EsitDegildir or
                        BinaryOperator.Buyuktur or
                        BinaryOperator.Kucuktur or
                        BinaryOperator.BuyukEsittir or
                        BinaryOperator.KucukEsittir or
                        BinaryOperator.Ve or
                        BinaryOperator.Veya => "mantık",

                        BinaryOperator.Add when leftType == "metin" || rightType == "metin" => "metin",
                        BinaryOperator.Add or
                        BinaryOperator.Subtract or
                        BinaryOperator.Multiply or
                        BinaryOperator.Divide or
                        BinaryOperator.Modulo when IsNumericType(leftType) && IsNumericType(rightType)
                            => leftType == "onlu" || rightType == "onlu" ? "onlu" : "sayı",

                        _ => null
                    };
                }

                default:
                    return null;
            }
        }

        private static bool IsTypeAssignable(string? expectedType, string? actualType)
        {
            var expected = NormalizeType(expectedType);
            var actual = NormalizeType(actualType);

            if (expected is null || actual is null || expected == "t" || actual == "t")
                return true;

            if (expected == actual)
                return true;

            if (expected == "onlu" && actual == "sayı")
                return true;

            if (expected == "nesne")
                return true;

            return false;
        }

        private static bool IsNumericType(string? type)
        {
            var normalized = NormalizeType(type);
            return normalized is "sayı" or "onlu";
        }

        private static string FormatType(string? type) => NormalizeType(type) ?? "bilinmiyor";

        private static string? NormalizeType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return null;

            return type.Trim() switch
            {
                "ondalık" => "onlu",
                "bool" => "mantık",
                "int" => "sayı",
                "double" => "onlu",
                "string" => "metin",
                _ => type.Trim()
            };
        }
    }
}

public record TranspilationResult(
    ProgramNode Ast,
    string? CSharpCode,
    IReadOnlyList<Core.Diagnostics.Diagnostic> Diagnostics);
