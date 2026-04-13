using GorSharp.Core.Ast;
using Xunit;

namespace GorSharp.Tests.Core;

public class AstNodeTests
{
    private static readonly SourceLocation TestLoc = new(1, 0, 1);

    [Fact]
    public void AssignmentNode_StoresProperties()
    {
        var value = new LiteralNode(5, LiteralType.Integer, TestLoc);
        var node = new AssignmentNode("x", "sayı", value, isDeclaration: true, TestLoc);

        Assert.Equal("x", node.Name);
        Assert.Equal("sayı", node.ExplicitType);
        Assert.True(node.IsDeclaration);
        Assert.Same(value, node.Value);
    }

    [Fact]
    public void AssignmentNode_InferredType_HasNullExplicitType()
    {
        var value = new LiteralNode(5, LiteralType.Integer, TestLoc);
        var node = new AssignmentNode("x", null, value, isDeclaration: true, TestLoc);

        Assert.Null(node.ExplicitType);
        Assert.True(node.IsDeclaration);
    }

    [Fact]
    public void LiteralNode_StoresValueAndType()
    {
        var intNode = new LiteralNode(42, LiteralType.Integer, TestLoc);
        Assert.Equal(42, intNode.Value);
        Assert.Equal(LiteralType.Integer, intNode.LiteralType);

        var strNode = new LiteralNode("merhaba", LiteralType.String, TestLoc);
        Assert.Equal("merhaba", strNode.Value);
        Assert.Equal(LiteralType.String, strNode.LiteralType);

        var boolNode = new LiteralNode(true, LiteralType.Boolean, TestLoc);
        Assert.Equal(true, boolNode.Value);

        var nullNode = new LiteralNode(null, LiteralType.Null, TestLoc);
        Assert.Null(nullNode.Value);
    }

    [Fact]
    public void PrintNode_StoresIsWriteLine()
    {
        var expr = new LiteralNode("test", LiteralType.String, TestLoc);
        var write = new PrintNode(expr, isWriteLine: false, TestLoc);
        var writeLine = new PrintNode(expr, isWriteLine: true, TestLoc);

        Assert.False(write.IsWriteLine);
        Assert.True(writeLine.IsWriteLine);
    }

    [Fact]
    public void BinaryExpressionNode_StoresOperator()
    {
        var left = new LiteralNode(3, LiteralType.Integer, TestLoc);
        var right = new LiteralNode(5, LiteralType.Integer, TestLoc);
        var node = new BinaryExpressionNode(left, BinaryOperator.Add, right, TestLoc);

        Assert.Equal(BinaryOperator.Add, node.Operator);
        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
    }

    [Fact]
    public void IfNode_StoresElseIfClauses()
    {
        var condition = new LiteralNode(true, LiteralType.Boolean, TestLoc);
        var thenBlock = new BlockNode([], TestLoc);
        var elseIfClauses = new List<(AstNode, BlockNode)>
        {
            (new LiteralNode(false, LiteralType.Boolean, TestLoc), new BlockNode([], TestLoc))
        };
        var elseBlock = new BlockNode([], TestLoc);
        var node = new IfNode(condition, thenBlock, elseIfClauses, elseBlock, TestLoc);

        Assert.Single(node.ElseIfClauses);
        Assert.NotNull(node.ElseBlock);
    }

    [Fact]
    public void WhileNode_StoresConditionAndBody()
    {
        var condition = new LiteralNode(true, LiteralType.Boolean, TestLoc);
        var body = new BlockNode([], TestLoc);
        var node = new WhileNode(condition, body, TestLoc);

        Assert.Same(condition, node.Condition);
        Assert.Same(body, node.Body);
    }

    [Fact]
    public void ForNode_AllowsNullParts()
    {
        var body = new BlockNode([], TestLoc);
        var node = new ForNode(null, null, null, body, TestLoc);

        Assert.Null(node.Initializer);
        Assert.Null(node.Condition);
        Assert.Null(node.Step);
    }

    [Fact]
    public void FunctionDefinitionNode_StoresParameters()
    {
        var body = new BlockNode([], TestLoc);
        var parameters = new List<(string Name, string Type)> { ("a", "sayı"), ("b", "sayı") };
        var node = new FunctionDefinitionNode("topla", parameters, "sayı", body, TestLoc);

        Assert.Equal("topla", node.Name);
        Assert.Equal(2, node.Parameters.Count);
        Assert.Equal("sayı", node.ReturnType);
    }

    [Fact]
    public void FunctionCallNode_StoresArguments()
    {
        var args = new List<AstNode>
        {
            new LiteralNode(3, LiteralType.Integer, TestLoc),
            new LiteralNode(5, LiteralType.Integer, TestLoc)
        };
        var node = new FunctionCallNode("topla", args, TestLoc);

        Assert.Equal("topla", node.FunctionName);
        Assert.Equal(2, node.Arguments.Count);
    }

    [Fact]
    public void ReturnNode_OptionalExpression()
    {
        var withExpr = new ReturnNode(new LiteralNode(42, LiteralType.Integer, TestLoc), TestLoc);
        var withoutExpr = new ReturnNode(null, TestLoc);

        Assert.NotNull(withExpr.Expression);
        Assert.Null(withoutExpr.Expression);
    }
}
