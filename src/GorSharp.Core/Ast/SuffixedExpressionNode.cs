namespace GorSharp.Core.Ast;

/// <summary>
/// Represents an expression with an optional Turkish suffix.
/// Example: "Geçti"yi → SuffixedExpressionNode(StringLiteralNode("Geçti"), "yi", "accusative")
/// Used for print operands and other suffix-enabled contexts.
/// </summary>
public class SuffixedExpressionNode : AstNode
{
    /// <summary>The base expression (string literal, identifier, etc.).</summary>
    public AstNode Expression { get; }

    /// <summary>The raw suffix text (e.g., "yi", "den").</summary>
    public string SuffixText { get; }

    /// <summary>
    /// The detected case for this suffix (e.g., "accusative", "dative").
    /// Null if the suffix could not be resolved (may trigger a diagnostic).
    /// </summary>
    public string? ResolvedCase { get; }

    public SuffixedExpressionNode(AstNode expression, string suffixText, string? resolvedCase, SourceLocation location)
        : base(location)
    {
        Expression = expression;
        SuffixText = suffixText;
        ResolvedCase = resolvedCase;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitSuffixedExpression(this);
}
