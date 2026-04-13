namespace GorSharp.Core.Ast;

/// <summary>
/// Break/continue: kır / devam
/// </summary>
public class BreakNode : AstNode
{
    public BreakNode(SourceLocation location) : base(location) { }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBreak(this);
}

public class ContinueNode : AstNode
{
    public ContinueNode(SourceLocation location) : base(location) { }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitContinue(this);
}
