namespace GorSharp.Core.Ast;

/// <summary>
/// The type of a literal value.
/// </summary>
public enum LiteralType
{
    Integer,
    Double,
    String,
    Boolean,
    Null
}

/// <summary>
/// A literal value: 5, 3.14, "Ali", doğru, yanlış, boş
/// </summary>
public class LiteralNode : AstNode
{
    public object? Value { get; }
    public LiteralType LiteralType { get; }

    public LiteralNode(object? value, LiteralType literalType, SourceLocation location)
        : base(location)
    {
        Value = value;
        LiteralType = literalType;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitLiteral(this);
}
