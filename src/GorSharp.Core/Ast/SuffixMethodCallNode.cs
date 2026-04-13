namespace GorSharp.Core.Ast;

/// <summary>
/// Suffix-based natural call form, e.g. "liste'ye 10 ekle;".
/// </summary>
public class SuffixMethodCallNode : AstNode
{
    public string TargetToken { get; }
    public string TargetStem { get; }
    public string Verb { get; }
    public string? SuffixCase { get; }
    public string? ResolvedMethod { get; }
    public IReadOnlyList<AstNode> Arguments { get; }

    public SuffixMethodCallNode(
        string targetToken,
        string targetStem,
        string verb,
        string? suffixCase,
        string? resolvedMethod,
        IReadOnlyList<AstNode> arguments,
        SourceLocation location)
        : base(location)
    {
        TargetToken = targetToken;
        TargetStem = targetStem;
        Verb = verb;
        SuffixCase = suffixCase;
        ResolvedMethod = resolvedMethod;
        Arguments = arguments;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitSuffixMethodCall(this);
}
