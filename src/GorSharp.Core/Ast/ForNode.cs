namespace GorSharp.Core.Ast;

/// <summary>
/// For loop: tekrarla (init; condition; step) { }
/// </summary>
public class ForNode : AstNode
{
    public AstNode? Initializer { get; }
    public AstNode? Condition { get; }
    public AstNode? Step { get; }
    public BlockNode Body { get; }

    public ForNode(AstNode? initializer, AstNode? condition, AstNode? step, BlockNode body, SourceLocation location)
        : base(location)
    {
        Initializer = initializer;
        Condition = condition;
        Step = step;
        Body = body;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFor(this);
}
