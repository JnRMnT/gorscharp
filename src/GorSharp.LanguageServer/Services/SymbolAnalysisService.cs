using GorSharp.Core.Ast;
using GorSharp.Core.Diagnostics;
using GorSharp.Parser;

namespace GorSharp.LanguageServer.Services;

public class SymbolAnalysisService
{
    private readonly GorSharpParserService _parser = new();
    private readonly BuiltInSignaturesService? _builtInSignatures;

    public SymbolAnalysisService()
    {
    }

    public SymbolAnalysisService(BuiltInSignaturesService builtInSignatures)
    {
        _builtInSignatures = builtInSignatures;
    }

    public SymbolAnalysisResult Analyze(string sourceCode, string fileName)
    {
        var (ast, diagnostics) = _parser.Parse(sourceCode, fileName);
        return AnalyzeAst(ast, diagnostics);
    }

    public SymbolAnalysisResult AnalyzeAst(ProgramNode ast, IReadOnlyList<Diagnostic> diagnostics)
    {
        var builder = new SymbolTableBuilder(_builtInSignatures);
        builder.VisitProgram(ast);
        return new SymbolAnalysisResult(ast, diagnostics, builder.Build());
    }
}

public enum GSymbolKind
{
    Function,
    Variable,
    Parameter,
}

public record ScopeRecord(int Id, int? ParentId);

public record SymbolRecord(
    int Id,
    string Name,
    GSymbolKind Kind,
    SourceLocation Location,
    int ScopeId,
    int DeclarationOrder,
    string? InferredType = null);

public record SymbolReferenceRecord(
    string Name,
    GSymbolKind Kind,
    SourceLocation Location,
    int ScopeId,
    int? ArgumentCount,
    int? ContainingFunctionDeclarationId,
    int? ResolvedDeclarationId);

public record CallEdgeRecord(
    int CallerDeclarationId,
    int CalleeDeclarationId,
    SourceLocation CallLocation);

public record FunctionSignatureRecord(
    int DeclarationId,
    string Name,
    IReadOnlyList<(string Name, string Type)> Parameters,
    string? ReturnType,
    SourceLocation Location);

public record SymbolOccurrence(
    string Name,
    GSymbolKind Kind,
    SourceLocation Location,
    bool IsDeclaration,
    int ScopeId,
    int? DeclarationId,
    int? ResolvedDeclarationId);

public class SymbolTable
{
    public IReadOnlyList<ScopeRecord> Scopes { get; }
    public IReadOnlyList<SymbolRecord> Declarations { get; }
    public IReadOnlyList<SymbolReferenceRecord> References { get; }
    public IReadOnlyList<FunctionSignatureRecord> FunctionSignatures { get; }
    public IReadOnlyList<CallEdgeRecord> CallEdges { get; }

    public SymbolTable(
        IReadOnlyList<ScopeRecord> scopes,
        IReadOnlyList<SymbolRecord> declarations,
        IReadOnlyList<SymbolReferenceRecord> references,
        IReadOnlyList<FunctionSignatureRecord> functionSignatures,
        IReadOnlyList<CallEdgeRecord> callEdges)
    {
        Scopes = scopes;
        Declarations = declarations;
        References = references;
        FunctionSignatures = functionSignatures;
        CallEdges = callEdges;
    }

    public FunctionSignatureRecord? FindFunctionSignature(string name)
    {
        return FunctionSignatures.FirstOrDefault(s => s.Name == name);
    }

    public SymbolRecord? FindDeclarationById(int declarationId)
    {
        return Declarations.FirstOrDefault(d => d.Id == declarationId);
    }

    public SymbolRecord? ResolveVisibleDeclaration(string name, GSymbolKind kind, int startScopeId)
    {
        var scopeParents = Scopes.ToDictionary(s => s.Id, s => s.ParentId);

        int? scopeId = startScopeId;
        while (scopeId is not null)
        {
            var match = Declarations
                .Where(d =>
                    d.ScopeId == scopeId.Value &&
                    d.Name == name &&
                    IsDeclarationCompatible(kind, d.Kind))
                .OrderByDescending(d => d.DeclarationOrder)
                .FirstOrDefault();

            if (match is not null)
                return match;

            scopeId = scopeParents.TryGetValue(scopeId.Value, out var parentId)
                ? parentId
                : null;
        }

        return null;
    }

    public IReadOnlyList<SymbolOccurrence> FindAllOccurrencesForDeclaration(int declarationId)
    {
        var result = new List<SymbolOccurrence>();

        var declaration = FindDeclarationById(declarationId);
        if (declaration is not null)
        {
            result.Add(new SymbolOccurrence(
                declaration.Name,
                declaration.Kind,
                declaration.Location,
                true,
                declaration.ScopeId,
                declaration.Id,
                declaration.Id));
        }

        foreach (var reference in References.Where(r => r.ResolvedDeclarationId == declarationId))
        {
            result.Add(new SymbolOccurrence(
                reference.Name,
                reference.Kind,
                reference.Location,
                false,
                reference.ScopeId,
                null,
                declarationId));
        }

        return result;
    }

    public SymbolOccurrence? FindOccurrenceAt(int lineZeroBased, int character)
    {
        var lineOneBased = lineZeroBased + 1;

        // Declarations first so F12 on declaration resolves immediately.
        foreach (var d in Declarations)
        {
            if (ContainsPosition(d.Location, d.Name, lineOneBased, character))
            {
                return new SymbolOccurrence(
                    d.Name,
                    d.Kind,
                    d.Location,
                    true,
                    d.ScopeId,
                    d.Id,
                    d.Id);
            }
        }

        foreach (var r in References)
        {
            if (ContainsPosition(r.Location, r.Name, lineOneBased, character))
            {
                return new SymbolOccurrence(
                    r.Name,
                    r.Kind,
                    r.Location,
                    false,
                    r.ScopeId,
                    null,
                    r.ResolvedDeclarationId);
            }
        }

        return null;
    }

    public IReadOnlyList<SymbolRecord> GetCompletionCandidatesAt(int lineZeroBased, int character)
    {
        var lineOneBased = lineZeroBased + 1;
        var parents = Scopes.ToDictionary(s => s.Id, s => s.ParentId);

        var activeScope = GuessActiveScope(lineOneBased, character);
        var visibleScopes = new HashSet<int>();
        int? scope = activeScope;
        while (scope is not null)
        {
            visibleScopes.Add(scope.Value);
            scope = parents.TryGetValue(scope.Value, out var p) ? p : null;
        }

        var declaredBeforeCursor = Declarations
            .Where(d =>
                visibleScopes.Contains(d.ScopeId) &&
                (d.Location.Line < lineOneBased ||
                 (d.Location.Line == lineOneBased && d.Location.Column <= character)))
            .ToList();

        // Keep globally declared functions visible even when declared later.
        var globalFunctions = Declarations
            .Where(d => d.ScopeId == 1 && d.Kind == GSymbolKind.Function)
            .ToList();

        return declaredBeforeCursor
            .Concat(globalFunctions)
            .GroupBy(d => d.Name)
            .Select(g => g.OrderByDescending(x => x.DeclarationOrder).First())
            .OrderBy(d => d.Kind == GSymbolKind.Function ? 0 : (d.Kind == GSymbolKind.Parameter ? 1 : 2))
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .ToList();
    }

    private int GuessActiveScope(int lineOneBased, int character)
    {
        var candidates = Declarations
            .Where(d =>
                d.Location.Line < lineOneBased ||
                (d.Location.Line == lineOneBased && d.Location.Column <= character))
            .OrderByDescending(d => d.Location.Line)
            .ThenByDescending(d => d.Location.Column)
            .ToList();

        if (candidates.Count > 0)
            return candidates[0].ScopeId;

        return 1; // global
    }

    private static bool ContainsPosition(SourceLocation location, string symbolName, int lineOneBased, int character)
    {
        if (location.Line != lineOneBased)
            return false;

        var start = location.Column;
        // Use symbol token width for hit-testing. AST node spans can cover full statements
        // (e.g. declarations) and incorrectly swallow adjacent identifiers.
        var length = Math.Max(symbolName.Length, 1);
        var endExclusive = start + length;
        return character >= start && character < endExclusive;
    }

    private static bool IsDeclarationCompatible(GSymbolKind referenceKind, GSymbolKind declarationKind)
    {
        if (referenceKind == GSymbolKind.Function)
            return declarationKind == GSymbolKind.Function;

        return declarationKind is GSymbolKind.Variable or GSymbolKind.Parameter;
    }
}

public record SymbolAnalysisResult(
    ProgramNode Ast,
    IReadOnlyList<Diagnostic> Diagnostics,
    SymbolTable Symbols);

internal sealed class SymbolTableBuilder : IAstVisitor<object?>
{
    private readonly BuiltInSignaturesService? _builtInSignatures;
    private readonly List<ScopeRecord> _scopes = new();
    private readonly List<SymbolRecord> _declarations = new();
    private readonly List<SymbolReferenceRecord> _references = new();
    private readonly List<FunctionSignatureRecord> _functionSignatures = new();
    private readonly List<CallEdgeRecord> _callEdges = new();

    private int _nextScopeId = 1;
    private int _nextDeclarationId = 1;
    private int _nextDeclarationOrder = 1;
    private int _currentScopeId;
    private int? _currentFunctionDeclarationId;

    // scopeId -> declarations in this scope
    private readonly Dictionary<int, List<SymbolRecord>> _scopeDeclarations = new();
    private readonly Dictionary<int, int?> _scopeParents = new();

    public SymbolTableBuilder(BuiltInSignaturesService? builtInSignatures)
    {
        _builtInSignatures = builtInSignatures;
        // Global scope
        _currentScopeId = CreateScope(parentId: null);
    }

    public SymbolTable Build()
    {
        ResolveReferences();
        BuildCallEdges();
        return new SymbolTable(_scopes, _declarations, _references, _functionSignatures, _callEdges);
    }

    public object? VisitProgram(ProgramNode node)
    {
        foreach (var statement in node.Statements)
            statement.Accept(this);
        return null;
    }

    public object? VisitAssignment(AssignmentNode node)
    {
        if (node.IsDeclaration)
        {
            // If explicit type is provided, use it; otherwise infer from value
            var inferredType = node.ExplicitType ?? InferTypeFromExpression(node.Value);
            Declare(node.Name, GSymbolKind.Variable, node.Location, _currentScopeId, inferredType);
        }

        node.Value.Accept(this);
        return null;
    }

    public object? VisitLiteral(LiteralNode node) => null;

    public object? VisitIdentifier(IdentifierNode node)
    {
        _references.Add(new SymbolReferenceRecord(
            node.Name,
            GSymbolKind.Variable,
            node.Location,
            _currentScopeId,
            null,
            _currentFunctionDeclarationId,
            null));
        return null;
    }

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
        var previousScope = _currentScopeId;
        _currentScopeId = CreateScope(previousScope);

        foreach (var statement in node.Statements)
            statement.Accept(this);

        _currentScopeId = previousScope;
        return null;
    }

    public object? VisitIf(IfNode node)
    {
        node.Condition.Accept(this);
        node.ThenBlock.Accept(this);

        foreach (var (condition, block) in node.ElseIfClauses)
        {
            condition.Accept(this);
            block.Accept(this);
        }

        node.ElseBlock?.Accept(this);
        return null;
    }

    public object? VisitWhile(WhileNode node)
    {
        node.Condition.Accept(this);
        node.Body.Accept(this);
        return null;
    }

    public object? VisitFor(ForNode node)
    {
        var previousScope = _currentScopeId;
        _currentScopeId = CreateScope(previousScope);

        node.Initializer?.Accept(this);
        node.Condition?.Accept(this);
        node.Step?.Accept(this);
        node.Body.Accept(this);

        _currentScopeId = previousScope;
        return null;
    }

    public object? VisitReturn(ReturnNode node)
    {
        node.Expression?.Accept(this);
        return null;
    }

    public object? VisitBreak(BreakNode node) => null;

    public object? VisitContinue(ContinueNode node) => null;

    public object? VisitFunctionCall(FunctionCallNode node)
    {
        _references.Add(new SymbolReferenceRecord(
            node.FunctionName,
            GSymbolKind.Function,
            node.Location,
            _currentScopeId,
            node.Arguments.Count,
            _currentFunctionDeclarationId,
            null));

        foreach (var arg in node.Arguments)
            arg.Accept(this);

        return null;
    }

    public object? VisitSuffixMethodCall(SuffixMethodCallNode node)
    {
        _references.Add(new SymbolReferenceRecord(
            node.TargetStem,
            GSymbolKind.Variable,
            node.Location,
            _currentScopeId,
            null,
            _currentFunctionDeclarationId,
            null));

        foreach (var arg in node.Arguments)
            arg.Accept(this);

        return null;
    }

    public object? VisitSuffixMethodChain(SuffixMethodChainNode node)
    {
        _references.Add(new SymbolReferenceRecord(
            node.TargetStem,
            GSymbolKind.Variable,
            node.Location,
            _currentScopeId,
            null,
            _currentFunctionDeclarationId,
            null));

        foreach (var step in node.Steps)
            step.Argument.Accept(this);

        return null;
    }

    public object? VisitSuffixPropertyAccess(SuffixPropertyAccessNode node)
    {
        _references.Add(new SymbolReferenceRecord(
            node.TargetStem,
            GSymbolKind.Variable,
            node.Location,
            _currentScopeId,
            null,
            _currentFunctionDeclarationId,
            null));

        return null;
    }

    public object? VisitFunctionDefinition(FunctionDefinitionNode node)
    {
        var functionSymbol = Declare(node.Name, GSymbolKind.Function, node.Location, _currentScopeId);

        _functionSignatures.Add(new FunctionSignatureRecord(
            functionSymbol.Id,
            node.Name,
            node.Parameters,
            node.ReturnType,
            node.Location));

        var previousScope = _currentScopeId;
        var previousFunctionDeclarationId = _currentFunctionDeclarationId;
        _currentScopeId = CreateScope(previousScope);
        _currentFunctionDeclarationId = functionSymbol.Id;

        foreach (var parameter in node.Parameters)
        {
            Declare(parameter.Name, GSymbolKind.Parameter, node.Location, _currentScopeId, parameter.Type);
        }

        node.Body.Accept(this);

        _currentScopeId = previousScope;
        _currentFunctionDeclarationId = previousFunctionDeclarationId;
        return null;
    }

    private int CreateScope(int? parentId)
    {
        var scopeId = _nextScopeId++;
        _scopes.Add(new ScopeRecord(scopeId, parentId));
        _scopeParents[scopeId] = parentId;
        _scopeDeclarations[scopeId] = new List<SymbolRecord>();
        return scopeId;
    }

    private SymbolRecord Declare(string name, GSymbolKind kind, SourceLocation location, int scopeId, string? inferredType = null)
    {
        var symbol = new SymbolRecord(
            _nextDeclarationId++,
            name,
            kind,
            location,
            scopeId,
            _nextDeclarationOrder++,
            inferredType);

        _declarations.Add(symbol);
        _scopeDeclarations[scopeId].Add(symbol);
        return symbol;
    }

    private void ResolveReferences()
    {
        for (var i = 0; i < _references.Count; i++)
        {
            var reference = _references[i];
            var declaration = ResolveDeclaration(reference.Name, reference.Kind, reference.ScopeId);
            _references[i] = reference with { ResolvedDeclarationId = declaration?.Id };
        }
    }

    private void BuildCallEdges()
    {
        foreach (var reference in _references.Where(r =>
                     r.Kind == GSymbolKind.Function &&
                     r.ContainingFunctionDeclarationId is not null &&
                     r.ResolvedDeclarationId is not null))
        {
            _callEdges.Add(new CallEdgeRecord(
                reference.ContainingFunctionDeclarationId!.Value,
                reference.ResolvedDeclarationId!.Value,
                reference.Location));
        }
    }

    private SymbolRecord? ResolveDeclaration(string name, GSymbolKind kind, int startScopeId)
    {
        int? scopeId = startScopeId;
        while (scopeId is not null)
        {
            if (_scopeDeclarations.TryGetValue(scopeId.Value, out var declarations))
            {
                var match = declarations
                    .Where(d => d.Name == name && IsKindCompatible(kind, d.Kind))
                    .OrderByDescending(d => d.DeclarationOrder)
                    .FirstOrDefault();

                if (match is not null)
                    return match;
            }

            scopeId = _scopeParents[scopeId.Value];
        }

        return null;
    }

    private static bool IsKindCompatible(GSymbolKind referenceKind, GSymbolKind declarationKind)
    {
        if (referenceKind == GSymbolKind.Function)
            return declarationKind == GSymbolKind.Function;

        // Identifier references may resolve to variable or parameter declarations.
        return declarationKind is GSymbolKind.Variable or GSymbolKind.Parameter;
    }

    /// <summary>
    /// Infer the Turkish type name from an expression node.
    /// Maps literal types to Gör# type names (sayı, onlu, metin, mantık).
    /// </summary>
    private string? InferTypeFromExpression(AstNode node)
    {
        return node switch
        {
            LiteralNode lit => InferTypeFromLiteral(lit),
            FunctionCallNode call => InferTypeFromFunctionCall(call),
            IdentifierNode identifier => InferTypeFromIdentifier(identifier),
            _ => null
        };
    }

    private string? InferTypeFromFunctionCall(FunctionCallNode node)
    {
        var declaration = ResolveDeclaration(node.FunctionName, GSymbolKind.Function, _currentScopeId);
        if (declaration is not null)
        {
            var signature = _functionSignatures.FirstOrDefault(s => s.DeclarationId == declaration.Id);
            if (!string.IsNullOrWhiteSpace(signature?.ReturnType))
                return signature.ReturnType;
        }

        if (_builtInSignatures is not null &&
            _builtInSignatures.TryGetBuiltInSignature(node.FunctionName, out var builtInSignature) &&
            !string.IsNullOrWhiteSpace(builtInSignature.ReturnType))
        {
            return builtInSignature.ReturnType;
        }

        return null;
    }

    private string? InferTypeFromIdentifier(IdentifierNode node)
    {
        var declaration = ResolveDeclaration(node.Name, GSymbolKind.Variable, _currentScopeId);
        return declaration?.InferredType;
    }

    private static string InferTypeFromLiteral(LiteralNode lit)
    {
        return lit.LiteralType switch
        {
            LiteralType.Integer => "sayı",
            LiteralType.Double => "onlu",
            LiteralType.String => "metin",
            LiteralType.Boolean => "mantık",
            LiteralType.Null => "boş",
            _ => "T"
        };
    }
}
