namespace GorSharp.Core.Ast;

/// <summary>
/// A block of statements enclosed in { }.
/// </summary>
public class BlockNode : AstNode
{
    public IReadOnlyList<AstNode> Statements { get; }

    public BlockNode(IReadOnlyList<AstNode> statements, SourceLocation location)
        : base(location)
    {
        Statements = statements;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBlock(this);
}
