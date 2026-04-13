namespace GorSharp.Core.Ast;

/// <summary>
/// Assignment statement: `x 5 olsun;` or `x: sayı 5 olsun;` or `x = 5;`
/// </summary>
public class AssignmentNode : AstNode
{
    /// <summary>Variable name.</summary>
    public string Name { get; }

    /// <summary>Explicit type annotation (e.g., "sayı"). Null when type is inferred.</summary>
    public string? ExplicitType { get; }

    /// <summary>The value expression being assigned.</summary>
    public AstNode Value { get; }

    /// <summary>True if this is a declaration (first assignment), false for reassignment.</summary>
    public bool IsDeclaration { get; }

    public AssignmentNode(string name, string? explicitType, AstNode value, bool isDeclaration, SourceLocation location)
        : base(location)
    {
        Name = name;
        ExplicitType = explicitType;
        Value = value;
        IsDeclaration = isDeclaration;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitAssignment(this);
}
