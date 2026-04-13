namespace GorSharp.Core.Ast;

/// <summary>
/// Suffix-based natural property form, e.g. "liste'nin uzunluğu".
/// </summary>
public class SuffixPropertyAccessNode : AstNode
{
    public string TargetToken { get; }
    public string TargetStem { get; }
    public string PropertyWord { get; }
    public string? SuffixCase { get; }
    public string? ResolvedMember { get; }

    public SuffixPropertyAccessNode(
        string targetToken,
        string targetStem,
        string propertyWord,
        string? suffixCase,
        string? resolvedMember,
        SourceLocation location)
        : base(location)
    {
        TargetToken = targetToken;
        TargetStem = targetStem;
        PropertyWord = propertyWord;
        SuffixCase = suffixCase;
        ResolvedMember = resolvedMember;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitSuffixPropertyAccess(this);
}
