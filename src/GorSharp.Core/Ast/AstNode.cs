namespace GorSharp.Core.Ast;

/// <summary>
/// Source location in a .gör file. Used for mirror mapping and error reporting.
/// </summary>
public record SourceLocation(int Line, int Column, int Length = 0)
{
    public static readonly SourceLocation Empty = new(0, 0, 0);
}

/// <summary>
/// Base class for all AST nodes. Every node carries its source location.
/// </summary>
public abstract class AstNode
{
    public SourceLocation Location { get; }

    protected AstNode(SourceLocation location)
    {
        Location = location;
    }

    public abstract T Accept<T>(IAstVisitor<T> visitor);
}

/// <summary>
/// Visitor interface for AST traversal. Each node type has a corresponding Visit method.
/// </summary>
public interface IAstVisitor<out T>
{
    T VisitProgram(ProgramNode node);
    T VisitAssignment(AssignmentNode node);
    T VisitLiteral(LiteralNode node);
    T VisitIdentifier(IdentifierNode node);
    T VisitBinaryExpression(BinaryExpressionNode node);
    T VisitUnaryExpression(UnaryExpressionNode node);
    T VisitPrint(PrintNode node);
    T VisitBlock(BlockNode node);
    T VisitIf(IfNode node);
    T VisitWhile(WhileNode node);
    T VisitFor(ForNode node);
    T VisitReturn(ReturnNode node);
    T VisitBreak(BreakNode node);
    T VisitContinue(ContinueNode node);
    T VisitFunctionCall(FunctionCallNode node);
    T VisitSuffixMethodCall(SuffixMethodCallNode node);
    T VisitSuffixMethodChain(SuffixMethodChainNode node);
    T VisitSuffixPropertyAccess(SuffixPropertyAccessNode node);
    T VisitFunctionDefinition(FunctionDefinitionNode node);
}
