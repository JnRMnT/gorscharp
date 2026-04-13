namespace GorSharp.Core.Ast;

/// <summary>
/// If/else-if/else statement: eğer condition { } yoksa eğer condition { } değilse { }
/// </summary>
public class IfNode : AstNode
{
    public AstNode Condition { get; }
    public BlockNode ThenBlock { get; }
    public IReadOnlyList<(AstNode Condition, BlockNode Block)> ElseIfClauses { get; }
    public BlockNode? ElseBlock { get; }

    public IfNode(
        AstNode condition,
        BlockNode thenBlock,
        IReadOnlyList<(AstNode Condition, BlockNode Block)> elseIfClauses,
        BlockNode? elseBlock,
        SourceLocation location)
        : base(location)
    {
        Condition = condition;
        ThenBlock = thenBlock;
        ElseIfClauses = elseIfClauses;
        ElseBlock = elseBlock;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIf(this);
}
