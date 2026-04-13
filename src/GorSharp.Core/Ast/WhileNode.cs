namespace GorSharp.Core.Ast;

/// <summary>
/// While loop: döngü condition { }
/// </summary>
public class WhileNode : AstNode
{
    public AstNode Condition { get; }
    public BlockNode Body { get; }

    public WhileNode(AstNode condition, BlockNode body, SourceLocation location)
        : base(location)
    {
        Condition = condition;
        Body = body;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitWhile(this);
}
