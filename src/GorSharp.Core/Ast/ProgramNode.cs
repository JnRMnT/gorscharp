namespace GorSharp.Core.Ast;

/// <summary>
/// Root node of a Gör# program. Contains a list of top-level statements.
/// </summary>
public class ProgramNode : AstNode
{
    public IReadOnlyList<AstNode> Statements { get; }

    public ProgramNode(IReadOnlyList<AstNode> statements, SourceLocation location)
        : base(location)
    {
        Statements = statements;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitProgram(this);
}
