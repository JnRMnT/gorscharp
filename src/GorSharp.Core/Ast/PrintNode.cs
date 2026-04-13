namespace GorSharp.Core.Ast;

/// <summary>
/// Print statement (SOV): `"Merhaba" yazdır;` or `x yeniSatıraYazdır;`
/// </summary>
public class PrintNode : AstNode
{
    /// <summary>The expression to print.</summary>
    public AstNode Expression { get; }

    /// <summary>True for yeniSatıraYazdır (Console.WriteLine), false for yazdır (Console.Write).</summary>
    public bool IsWriteLine { get; }

    public PrintNode(AstNode expression, bool isWriteLine, SourceLocation location)
        : base(location)
    {
        Expression = expression;
        IsWriteLine = isWriteLine;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitPrint(this);
}
