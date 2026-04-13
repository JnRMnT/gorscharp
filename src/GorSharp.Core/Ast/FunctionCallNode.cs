namespace GorSharp.Core.Ast;

/// <summary>
/// Function call in SVO form: topla(3, 5)
/// </summary>
public class FunctionCallNode : AstNode
{
    public string FunctionName { get; }
    public IReadOnlyList<AstNode> Arguments { get; }

    public FunctionCallNode(string functionName, IReadOnlyList<AstNode> arguments, SourceLocation location)
        : base(location)
    {
        FunctionName = functionName;
        Arguments = arguments;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFunctionCall(this);
}
