namespace GorSharp.Core.Ast;

/// <summary>
/// Unary operator types.
/// </summary>
public enum UnaryOperator
{
    Negate,     // - (numeric negation)
    Degil       // ! (değil — logical not)
}

/// <summary>
/// Unary expression: `değil aktif`, `-sayı`
/// </summary>
public class UnaryExpressionNode : AstNode
{
    public UnaryOperator Operator { get; }
    public AstNode Operand { get; }

    public UnaryExpressionNode(UnaryOperator @operator, AstNode operand, SourceLocation location)
        : base(location)
    {
        Operator = @operator;
        Operand = operand;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitUnaryExpression(this);
}
