namespace GorSharp.Core.Ast;

/// <summary>
/// Binary operator types (arithmetic + Turkish comparison/logical operators).
/// </summary>
public enum BinaryOperator
{
    // Arithmetic
    Add,        // +
    Subtract,   // -
    Multiply,   // *
    Divide,     // /
    Modulo,     // %

    // Comparison (Turkish only)
    Esittir,        // == (eşittir)
    EsitDegildir,   // != (eşitDeğildir)
    Buyuktur,       // >  (büyüktür)
    Kucuktur,       // <  (küçüktür)
    BuyukEsittir,   // >= (büyükEşittir)
    KucukEsittir,   // <= (küçükEşittir)

    // Logical (Turkish only)
    Ve,     // && (ve)
    Veya    // || (veya)
}

/// <summary>
/// Binary expression: `a + b`, `x büyüktür 5`, `a ve b`
/// </summary>
public class BinaryExpressionNode : AstNode
{
    public AstNode Left { get; }
    public BinaryOperator Operator { get; }
    public AstNode Right { get; }

    public BinaryExpressionNode(AstNode left, BinaryOperator @operator, AstNode right, SourceLocation location)
        : base(location)
    {
        Left = left;
        Operator = @operator;
        Right = right;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitBinaryExpression(this);
}
