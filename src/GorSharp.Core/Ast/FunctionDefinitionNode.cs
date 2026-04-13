namespace GorSharp.Core.Ast;

/// <summary>
/// Function definition: fonksiyon topla(a: sayı, b: sayı): sayı { döndür a + b; }
/// </summary>
public class FunctionDefinitionNode : AstNode
{
    public string Name { get; }
    public IReadOnlyList<(string Name, string Type)> Parameters { get; }
    public string? ReturnType { get; }
    public BlockNode Body { get; }

    public FunctionDefinitionNode(
        string name,
        IReadOnlyList<(string Name, string Type)> parameters,
        string? returnType,
        BlockNode body,
        SourceLocation location)
        : base(location)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
        Body = body;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFunctionDefinition(this);
}
