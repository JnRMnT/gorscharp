namespace GorSharp.Core.Ast;

/// <summary>
/// Return statement: döndür expression;
/// </summary>
public class ReturnNode : AstNode
{
    public AstNode? Expression { get; }

    public ReturnNode(AstNode? expression, SourceLocation location)
        : base(location)
    {
        Expression = expression;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitReturn(this);
}
