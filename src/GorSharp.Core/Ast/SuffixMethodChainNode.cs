namespace GorSharp.Core.Ast;

/// <summary>
/// A chained suffix-based natural call form, e.g. "liste'ye 10 ekle sonra 20 ekle;".
/// </summary>
public class SuffixMethodChainNode : AstNode
{
    public string TargetToken { get; }
    public string TargetStem { get; }
    public string? SuffixCase { get; }
    public IReadOnlyList<SuffixMethodChainStep> Steps { get; }
    public string? TailPropertyWord { get; }
    public string? TailResolvedMember { get; }
    public bool TailIsWriteLine { get; }

    public SuffixMethodChainNode(
        string targetToken,
        string targetStem,
        string? suffixCase,
        IReadOnlyList<SuffixMethodChainStep> steps,
        string? tailPropertyWord,
        string? tailResolvedMember,
        bool tailIsWriteLine,
        SourceLocation location)
        : base(location)
    {
        TargetToken = targetToken;
        TargetStem = targetStem;
        SuffixCase = suffixCase;
        Steps = steps;
        TailPropertyWord = tailPropertyWord;
        TailResolvedMember = tailResolvedMember;
        TailIsWriteLine = tailIsWriteLine;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitSuffixMethodChain(this);
}

public record SuffixMethodChainStep(
    string Verb,
    string? ResolvedMethod,
    AstNode Argument,
    SourceLocation Location);
