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
    private readonly GeneratedCSharpCompilationService _generatedCSharpCompilation = new();
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

                var sourceMap = GeneratedCSharpSourceMap.Create(csharpCode);
                diagnostics = diagnostics
                    .Concat(_generatedCSharpCompilation.Compile(csharpCode, fileName, sourceMap))
                    .ToList();
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
            var message = $"{kindText} tanımlı değil: '{reference.Name}'.";

            if (reference.Kind == GSymbolKind.Function)
            {
                var visibleValue = symbols.ResolveVisibleDeclaration(reference.Name, GSymbolKind.Variable, reference.ScopeId);
                if (visibleValue is not null)
                {
                    message =
                        $"Fonksiyon tanımlı değil: '{reference.Name}'. Ancak aynı adla bir değişken bulundu; çağrı yapmak yerine değeri kullanmak için parantezleri kaldırın veya bir fonksiyon tanımlayın.";
                }
            }

            diagnostics.Add(new Core.Diagnostics.Diagnostic(
                Core.Diagnostics.DiagnosticSeverity.Error,
                "GOR2001",
                message,
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

                var duplicateDeclarationIds = duplicateGroups
                    .SelectMany(g => g.Select(d => d.Id))
                    .ToHashSet();

        // GOR2011: Variable shadowing.
        var scopeLookup = symbols.Scopes.ToDictionary(s => s.Id);
        foreach (var decl in symbols.Declarations.Where(d => valueKinds.Contains(d.Kind)))
        {
            // Look for same-named declaration in parent scopes
            var parentScopeId = scopeLookup.TryGetValue(decl.ScopeId, out var scope) ? scope.ParentId : null;
            while (parentScopeId is not null)
            {
                var shadowedDecl = symbols.Declarations
                    .Where(d => d.ScopeId == parentScopeId.Value && d.Name == decl.Name && valueKinds.Contains(d.Kind))
                    .OrderByDescending(d => d.DeclarationOrder)
                    .FirstOrDefault();

                if (shadowedDecl is not null)
                {
                    var shadowKind = shadowedDecl.Kind == GSymbolKind.Parameter ? "parametre" : "değişken";
                    diagnostics.Add(new Core.Diagnostics.Diagnostic(
                        Core.Diagnostics.DiagnosticSeverity.Warning,
                        DiagnosticCodes.VariableShadowing,
                        $"Değişken '{decl.Name}' dış kapsamdaki {shadowKind} '{shadowedDecl.Name}' tanımını gölgeliyor.",
                        fileName,
                        decl.Location.Line,
                        decl.Location.Column));
                    break;
                }

                parentScopeId = scopeLookup.TryGetValue(parentScopeId.Value, out var parentScope) ? parentScope.ParentId : null;
            }
        }

        // GOR2013: Unused variable declaration.
        var usedDeclarationIds = symbols.References
            .Where(r => r.ResolvedDeclarationId is not null && r.Kind == GSymbolKind.Variable)
            .Select(r => r.ResolvedDeclarationId!.Value)
            .ToHashSet();

        foreach (var decl in symbols.Declarations.Where(d => d.Kind == GSymbolKind.Variable))
        {
            // Ignore throwaway names and duplicate-declaration cascades.
            if (decl.Name == "_" || duplicateDeclarationIds.Contains(decl.Id))
                continue;

            if (usedDeclarationIds.Contains(decl.Id))
                continue;

            diagnostics.Add(new Core.Diagnostics.Diagnostic(
                Core.Diagnostics.DiagnosticSeverity.Warning,
                DiagnosticCodes.UnusedVariable,
                $"Değişken kullanilmiyor: '{decl.Name}'.",
                fileName,
                decl.Location.Line,
                decl.Location.Column));
        }

        // GOR2016: Unused function declaration.
        var calledFunctionDeclarationIds = symbols.References
            .Where(r => r.Kind == GSymbolKind.Function && r.ResolvedDeclarationId is not null)
            .Select(r => r.ResolvedDeclarationId!.Value)
            .ToHashSet();

        foreach (var signature in symbols.FunctionSignatures)
        {
            if (calledFunctionDeclarationIds.Contains(signature.DeclarationId))
                continue;

            diagnostics.Add(new Core.Diagnostics.Diagnostic(
                Core.Diagnostics.DiagnosticSeverity.Warning,
                DiagnosticCodes.UnusedFunction,
                $"Fonksiyon kullanılmıyor: '{signature.Name}'.",
                fileName,
                signature.Location.Line,
                signature.Location.Column));
        }

        // GOR2017: Unused function parameter declaration.
        foreach (var decl in symbols.Declarations.Where(d => d.Kind == GSymbolKind.Parameter))
        {
            if (decl.Name == "_")
                continue;

            if (usedDeclarationIds.Contains(decl.Id))
                continue;

            diagnostics.Add(new Core.Diagnostics.Diagnostic(
                Core.Diagnostics.DiagnosticSeverity.Warning,
                DiagnosticCodes.UnusedParameter,
                $"Parametre kullanılmıyor: '{decl.Name}'.",
                fileName,
                decl.Location.Line,
                decl.Location.Column));
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
        private bool _inLoop;

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
                if (declaration is null)
                {
                    AddDiagnostic(
                        DiagnosticCodes.AssignmentToUndefinedVariable,
                        $"Atama yapılamıyor: '{node.Name}' değişkeni bu kapsamda tanımlı değil. Önce 'olsun' ile tanımlayın.",
                        node.Location);
                }
                else if (!string.IsNullOrWhiteSpace(declaration.InferredType) &&
                         !IsTypeAssignable(declaration.InferredType, valueType))
                {
                    AddDiagnostic(
                        "GOR2004",
                        $"Atama türü uyumsuzluğu: '{node.Name}' değişkeni '{declaration.InferredType}' bekliyor, '{FormatType(valueType)}' verildi.",
                        node.Location);
                }

                if (IsNoOpAssignment(node))
                {
                    AddDiagnostic(
                        DiagnosticCodes.NoOpAssignment,
                        $"Etkisiz atama: '{node.Name}' için işlem sonucu değeri değiştirmiyor.",
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
            ValidateBinaryExpressionOperands(node);
            node.Left.Accept(this);
            node.Right.Accept(this);
            return null;
        }

        public object? VisitUnaryExpression(UnaryExpressionNode node)
        {
            ValidateUnaryExpressionOperand(node);
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

            var hasTerminator = false;
            string? terminatorText = null;

            foreach (var statement in node.Statements)
            {
                if (hasTerminator)
                {
                    AddDiagnostic(
                        DiagnosticCodes.UnreachableCode,
                        $"Erişilemeyen kod: bu ifade önceki '{terminatorText}' ifadesinden sonra çalışmaz.",
                        statement.Location);
                }

                statement.Accept(this);

                if (IsBlockTerminator(statement, out var foundTerminatorText))
                {
                    hasTerminator = true;
                    terminatorText = foundTerminatorText;
                }
            }

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

            if (node.ElseIfClauses.Count == 0 && node.ElseBlock is not null && AreBlocksEquivalent(node.ThenBlock, node.ElseBlock))
            {
                AddDiagnostic(
                    DiagnosticCodes.RedundantBranch,
                    "Gereksiz dal: 'eğer' ve 'değilse' blokları aynı sonucu üretiyor.",
                    node.Location);
            }

            return null;
        }

        public object? VisitWhile(WhileNode node)
        {
            ValidateCondition(node.Condition, node.Condition.Location);
            node.Condition.Accept(this);

            var previousInLoop = _inLoop;
            _inLoop = true;
            node.Body.Accept(this);
            _inLoop = previousInLoop;

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
            else
            {
                AddDiagnostic(
                    DiagnosticCodes.ConditionlessForLoop,
                    "Döngü koşulu eksik: 'tekrarla' koşulsuz çalışır ve sonsuz döngüye neden olabilir.",
                    node.Location);
            }

            if (TryGetLoopProgressMismatchMessage(node, out var mismatchMessage))
            {
                AddDiagnostic(
                    DiagnosticCodes.LoopProgressMismatch,
                    mismatchMessage,
                    node.Location);
            }

            node.Step?.Accept(this);

            var previousInLoop = _inLoop;
            _inLoop = true;
            node.Body.Accept(this);
            _inLoop = previousInLoop;

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

        public object? VisitBreak(BreakNode node)
        {
            if (!_inLoop)
            {
                AddDiagnostic(
                    DiagnosticCodes.BreakContinueOutsideLoop,
                    "Kır komutu döngü içinde kullanılmalı.",
                    node.Location);
            }
            return null;
        }

        public object? VisitContinue(ContinueNode node)
        {
            if (!_inLoop)
            {
                AddDiagnostic(
                    DiagnosticCodes.BreakContinueOutsideLoop,
                    "Devam komutu döngü içinde kullanılmalı.",
                    node.Location);
            }
            return null;
        }

        public object? VisitFunctionCall(FunctionCallNode node)
        {
            ValidateFunctionCallArguments(node);

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

            if (!string.IsNullOrWhiteSpace(node.ReturnType) && CanFallThrough(node.Body))
            {
                AddDiagnostic(
                    "GOR2009",
                    $"Fonksiyonun tüm yürütme yolları değer döndürmüyor: '{node.Name}' için '{node.ReturnType}' dönüş türü tanımlı, ancak bazı yollar 'döndür' ile bitmiyor.",
                    node.Location);
            }

            _currentScopeId = previousScopeId;
            _currentFunctionName = previousFunctionName;
            _currentFunctionReturnType = previousFunctionReturnType;
            return null;
        }

        public object? VisitSuffixedExpression(SuffixedExpressionNode node)
        {
            // Visit the underlying expression, suffix is semantic only
            return node.Expression.Accept(this);
        }

        private int CreateScope() => _nextScopeId++;

        private void ValidateCondition(AstNode condition, SourceLocation location)
        {
            if (condition is LiteralNode literal && literal.LiteralType == LiteralType.Boolean)
            {
                var boolText = literal.Value is bool b && b ? "doğru" : "yanlış";
                AddDiagnostic(
                    DiagnosticCodes.ConstantCondition,
                    $"Sabit koşul: ifade her zaman '{boolText}' değerlendiriliyor.",
                    location);
            }

            var conditionType = InferType(condition);
            if (string.IsNullOrWhiteSpace(conditionType) || NormalizeType(conditionType) is "mantık" or "t")
                return;

            AddDiagnostic(
                "GOR2005",
                $"Koşul ifadesi 'mantık' türünde olmalı, '{FormatType(conditionType)}' bulundu.",
                location);
        }

        private void ValidateBinaryExpressionOperands(BinaryExpressionNode node)
        {
            var leftType = NormalizeType(InferType(node.Left));
            var rightType = NormalizeType(InferType(node.Right));

            if (IsUnknownType(leftType) || IsUnknownType(rightType))
                return;

            switch (node.Operator)
            {
                case BinaryOperator.Ve:
                case BinaryOperator.Veya:
                    if (leftType != "mantık" || rightType != "mantık")
                    {
                        AddDiagnostic(
                            "GOR2008",
                            $"Mantıksal işlem türü uyuşmuyor: '{DescribeOperator(node.Operator)}' için her iki taraf da 'mantık' olmalı, solda '{FormatType(leftType)}' ve sağda '{FormatType(rightType)}' bulundu.",
                            node.Location);
                    }
                    break;

                case BinaryOperator.Add:
                    if (leftType == "metin" || rightType == "metin")
                        return;

                    if (!IsNumericType(leftType) || !IsNumericType(rightType))
                    {
                        AddDiagnostic(
                            "GOR2008",
                            $"Toplama türü uyuşmuyor: '+' için iki sayı veya metin birleştirme beklenir, solda '{FormatType(leftType)}' ve sağda '{FormatType(rightType)}' bulundu.",
                            node.Location);
                    }
                    break;

                case BinaryOperator.Subtract:
                case BinaryOperator.Multiply:
                case BinaryOperator.Divide:
                case BinaryOperator.Modulo:
                    if (!IsNumericType(leftType) || !IsNumericType(rightType))
                    {
                        AddDiagnostic(
                            "GOR2008",
                            $"Aritmetik işlem türü uyuşmuyor: '{DescribeOperator(node.Operator)}' için her iki taraf da sayısal olmalı, solda '{FormatType(leftType)}' ve sağda '{FormatType(rightType)}' bulundu.",
                            node.Location);
                    }
                    break;

                case BinaryOperator.Buyuktur:
                case BinaryOperator.Kucuktur:
                case BinaryOperator.BuyukEsittir:
                case BinaryOperator.KucukEsittir:
                    if (!IsNumericType(leftType) || !IsNumericType(rightType))
                    {
                        AddDiagnostic(
                            "GOR2008",
                            $"Karşılaştırma türü uyuşmuyor: '{DescribeOperator(node.Operator)}' için her iki taraf da sayısal olmalı, solda '{FormatType(leftType)}' ve sağda '{FormatType(rightType)}' bulundu.",
                            node.Location);
                    }
                    break;
            }
        }

        private void ValidateUnaryExpressionOperand(UnaryExpressionNode node)
        {
            var operandType = NormalizeType(InferType(node.Operand));
            if (IsUnknownType(operandType))
                return;

            switch (node.Operator)
            {
                case UnaryOperator.Degil:
                    if (operandType != "mantık")
                    {
                        AddDiagnostic(
                            "GOR2010",
                            $"Tekli işlem türü uyuşmuyor: 'değil' yalnızca 'mantık' üzerinde kullanılabilir, '{FormatType(operandType)}' bulundu.",
                            node.Location);
                    }
                    break;

                case UnaryOperator.Negate:
                    if (!IsNumericType(operandType))
                    {
                        AddDiagnostic(
                            "GOR2010",
                            $"Tekli işlem türü uyuşmuyor: '-' yalnızca sayısal değerlerde kullanılabilir, '{FormatType(operandType)}' bulundu.",
                            node.Location);
                    }
                    break;
            }
        }

        private bool IsBlockTerminator(AstNode statement, out string terminatorText)
        {
            switch (statement)
            {
                case ReturnNode:
                    terminatorText = "döndür";
                    return true;
                case BreakNode when _inLoop:
                    terminatorText = "kır";
                    return true;
                case ContinueNode when _inLoop:
                    terminatorText = "devam";
                    return true;
                default:
                    terminatorText = string.Empty;
                    return false;
            }
        }

        private static bool IsNoOpAssignment(AssignmentNode node)
        {
            if (node.Value is IdentifierNode id)
                return id.Name == node.Name;

            if (node.Value is not BinaryExpressionNode binary || binary.Left is not IdentifierNode leftId || leftId.Name != node.Name)
                return false;

            if (binary.Right is not LiteralNode literal)
                return false;

            return binary.Operator switch
            {
                BinaryOperator.Add or BinaryOperator.Subtract => IsNumericLiteralValue(literal, 0),
                BinaryOperator.Multiply or BinaryOperator.Divide => IsNumericLiteralValue(literal, 1),
                _ => false
            };
        }

        private static bool IsNumericLiteralValue(LiteralNode literal, double value)
        {
            if (literal.LiteralType == LiteralType.Integer && literal.Value is int i)
                return i == (int)value;

            if (literal.LiteralType == LiteralType.Double && literal.Value is double d)
                return Math.Abs(d - value) < double.Epsilon;

            return false;
        }

        private static bool AreBlocksEquivalent(BlockNode left, BlockNode right)
        {
            if (left.Statements.Count != right.Statements.Count)
                return false;

            for (var i = 0; i < left.Statements.Count; i++)
            {
                if (!AreNodesEquivalent(left.Statements[i], right.Statements[i]))
                    return false;
            }

            return true;
        }

        private static bool AreNodesEquivalent(AstNode left, AstNode right)
        {
            if (left.GetType() != right.GetType())
                return false;

            return (left, right) switch
            {
                (ReturnNode l, ReturnNode r) => (l.Expression, r.Expression) switch
                {
                    (null, null) => true,
                    (not null, not null) => AreNodesEquivalent(l.Expression, r.Expression),
                    _ => false
                },
                (AssignmentNode l, AssignmentNode r) => l.Name == r.Name && AreNodesEquivalent(l.Value, r.Value),
                (PrintNode l, PrintNode r) => l.IsWriteLine == r.IsWriteLine && AreNodesEquivalent(l.Expression, r.Expression),
                (IdentifierNode l, IdentifierNode r) => l.Name == r.Name,
                (LiteralNode l, LiteralNode r) => Equals(l.Value, r.Value) && l.LiteralType == r.LiteralType,
                (BinaryExpressionNode l, BinaryExpressionNode r) => l.Operator == r.Operator && AreNodesEquivalent(l.Left, r.Left) && AreNodesEquivalent(l.Right, r.Right),
                (UnaryExpressionNode l, UnaryExpressionNode r) => l.Operator == r.Operator && AreNodesEquivalent(l.Operand, r.Operand),
                (BreakNode, BreakNode) => true,
                (ContinueNode, ContinueNode) => true,
                _ => false
            };
        }

        private static bool TryGetLoopProgressMismatchMessage(ForNode node, out string message)
        {
            message = string.Empty;

            if (node.Condition is not BinaryExpressionNode condition ||
                condition.Left is not IdentifierNode loopId ||
                condition.Right is not LiteralNode boundLiteral ||
                boundLiteral.LiteralType is not (LiteralType.Integer or LiteralType.Double) ||
                node.Step is not AssignmentNode stepAssign ||
                stepAssign.Name != loopId.Name ||
                stepAssign.Value is not BinaryExpressionNode stepExpr ||
                stepExpr.Left is not IdentifierNode stepLeft ||
                stepLeft.Name != loopId.Name ||
                stepExpr.Right is not LiteralNode stepLiteral)
            {
                return false;
            }

            if (!TryGetSignedDelta(stepExpr, stepLiteral, out var delta) || Math.Abs(delta) < double.Epsilon)
                return false;

            var expectsIncrease = condition.Operator is BinaryOperator.Kucuktur or BinaryOperator.KucukEsittir;
            var expectsDecrease = condition.Operator is BinaryOperator.Buyuktur or BinaryOperator.BuyukEsittir;

            if ((expectsIncrease && delta < 0) || (expectsDecrease && delta > 0))
            {
                var direction = delta > 0 ? "artıyor" : "azalıyor";
                message = $"Döngü ilerleme uyumsuzluğu: '{loopId.Name}' {direction}, ancak koşul bu yönde sonlanmayı zorlaştırabilir.";
                return true;
            }

            return false;
        }

        private static bool TryGetSignedDelta(BinaryExpressionNode stepExpr, LiteralNode stepLiteral, out double delta)
        {
            delta = 0;

            var magnitude = stepLiteral switch
            {
                { LiteralType: LiteralType.Integer, Value: int i } => (double)i,
                { LiteralType: LiteralType.Double, Value: double d } => d,
                _ => double.NaN
            };

            if (double.IsNaN(magnitude))
                return false;

            delta = stepExpr.Operator switch
            {
                BinaryOperator.Add => magnitude,
                BinaryOperator.Subtract => -magnitude,
                _ => 0
            };

            return stepExpr.Operator is BinaryOperator.Add or BinaryOperator.Subtract;
        }

        private void ValidateFunctionCallArguments(FunctionCallNode node)
        {
            var signature = ResolveSignature(node);
            if (signature is null)
                return;

            if (signature.Parameters.Count != node.Arguments.Count)
                return;

            for (var i = 0; i < signature.Parameters.Count; i++)
            {
                var (parameterName, expectedType) = signature.Parameters[i];
                var actualType = InferType(node.Arguments[i]);

                if (IsTypeAssignable(expectedType, actualType))
                    continue;

                AddDiagnostic(
                    "GOR2007",
                    $"Fonksiyon argüman türü uyuşmuyor: '{node.FunctionName}' için '{parameterName}' parametresinde '{expectedType}' bekleniyor, '{FormatType(actualType)}' verildi.",
                    node.Arguments[i].Location);
            }
        }

        private FunctionCallSignature? ResolveSignature(FunctionCallNode node)
        {
            var declaration = _symbols.ResolveVisibleDeclaration(node.FunctionName, GSymbolKind.Function, _currentScopeId);
            if (declaration is not null)
            {
                var userSignature = _symbols.FunctionSignatures.FirstOrDefault(s => s.DeclarationId == declaration.Id);
                if (userSignature is not null)
                {
                    return new FunctionCallSignature(userSignature.Parameters);
                }
            }

            if (_builtInSignatures.TryGetBuiltInSignature(node.FunctionName, out var builtInSignature))
            {
                var parameters = builtInSignature.Parameters
                    .Select(p => (p.Name, p.Type))
                    .ToList();

                return new FunctionCallSignature(parameters);
            }

            return null;
        }

        private static bool CanFallThrough(BlockNode block)
        {
            foreach (var statement in block.Statements)
            {
                if (GuaranteesReturn(statement))
                    return false;
            }

            return true;
        }

        private static bool GuaranteesReturn(AstNode node)
        {
            return node switch
            {
                ReturnNode => true,
                BlockNode block => !CanFallThrough(block),
                IfNode ifNode => IfGuaranteesReturn(ifNode),
                _ => false
            };
        }

        private static bool IfGuaranteesReturn(IfNode ifNode)
        {
            if (!GuaranteesReturn(ifNode.ThenBlock))
                return false;

            foreach (var (_, block) in ifNode.ElseIfClauses)
            {
                if (!GuaranteesReturn(block))
                    return false;
            }

            return ifNode.ElseBlock is not null && GuaranteesReturn(ifNode.ElseBlock);
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

        private static bool IsUnknownType(string? type)
        {
            var normalized = NormalizeType(type);
            return string.IsNullOrWhiteSpace(normalized) || normalized == "t";
        }

        private static string DescribeOperator(BinaryOperator op) => op switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Ve => "ve",
            BinaryOperator.Veya => "veya",
            BinaryOperator.Buyuktur => "büyüktür",
            BinaryOperator.Kucuktur => "küçüktür",
            BinaryOperator.BuyukEsittir => "büyükEşittir",
            BinaryOperator.KucukEsittir => "küçükEşittir",
            _ => op.ToString()
        };

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

        private sealed record FunctionCallSignature(
            IReadOnlyList<(string Name, string Type)> Parameters);
    }
}

public record TranspilationResult(
    ProgramNode Ast,
    string? CSharpCode,
    IReadOnlyList<Core.Diagnostics.Diagnostic> Diagnostics);
