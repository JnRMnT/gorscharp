namespace GorSharp.Core.Ast;

/// <summary>
/// An identifier (variable name, function name). Supports Turkish characters.
/// </summary>
public class IdentifierNode : AstNode
{
    public string Name { get; }

    public IdentifierNode(string name, SourceLocation location)
        : base(location)
    {
        Name = name;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitIdentifier(this);
}
