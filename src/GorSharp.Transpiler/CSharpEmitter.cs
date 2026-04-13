using System.Text;
using GorSharp.Core.Ast;
using GorSharp.Core.Sozluk;

namespace GorSharp.Transpiler;

/// <summary>
/// Visits an AST and emits C# source code. Each emitted line includes a source location comment
/// for the mirror feature: /* gör:LINE */
/// </summary>
public class CSharpEmitter : IAstVisitor<string>
{
    private int _indentLevel;
    private readonly StringBuilder _sb = new();
    private readonly SozlukData? _sozluk;

    public CSharpEmitter(SozlukData? sozluk = null)
    {
        _sozluk = sozluk;
    }

    public string Emit(ProgramNode program)
    {
        _sb.Clear();
        _indentLevel = 0;

        foreach (var statement in program.Statements)
        {
            var code = statement.Accept(this);
            AppendLine(code, statement.Location.Line);
        }

        return _sb.ToString();
    }

    public string VisitProgram(ProgramNode node)
    {
        // Handled by Emit directly
        return string.Empty;
    }

    public string VisitAssignment(AssignmentNode node)
    {
        var value = node.Value.Accept(this);

        if (node.IsDeclaration)
        {
            if (node.ExplicitType is not null)
            {
                var csType = MapType(node.ExplicitType);
                return $"{csType} {node.Name} = {value};";
            }
            return $"var {node.Name} = {value};";
        }

        return $"{node.Name} = {value};";
    }

    public string VisitLiteral(LiteralNode node)
    {
        return node.LiteralType switch
        {
            LiteralType.Integer => node.Value!.ToString()!,
            LiteralType.Double => ((double)node.Value!).ToString(System.Globalization.CultureInfo.InvariantCulture),
            LiteralType.String => $"\"{node.Value}\"",
            LiteralType.Boolean => (bool)node.Value! ? "true" : "false",
            LiteralType.Null => "null",
            _ => throw new InvalidOperationException($"Bilinmeyen literal tipi: {node.LiteralType}")
        };
    }

    public string VisitIdentifier(IdentifierNode node)
    {
        return node.Name;
    }

    public string VisitBinaryExpression(BinaryExpressionNode node)
    {
        var left = node.Left.Accept(this);
        var right = node.Right.Accept(this);
        var op = MapBinaryOperator(node.Operator);
        return $"{left} {op} {right}";
    }

    public string VisitUnaryExpression(UnaryExpressionNode node)
    {
        var operand = node.Operand.Accept(this);
        return node.Operator switch
        {
            UnaryOperator.Negate => $"-{operand}",
            UnaryOperator.Degil => $"!{operand}",
            _ => throw new InvalidOperationException($"Bilinmeyen tekli operatör: {node.Operator}")
        };
    }

    public string VisitPrint(PrintNode node)
    {
        var expr = node.Expression.Accept(this);
        var method = node.IsWriteLine ? "Console.WriteLine" : "Console.Write";
        return $"{method}({expr});";
    }

    public string VisitBlock(BlockNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        _indentLevel++;
        foreach (var statement in node.Statements)
        {
            var code = statement.Accept(this);
            sb.Append(new string(' ', _indentLevel * 4));
            sb.AppendLine(code);
        }
        _indentLevel--;
        sb.Append(new string(' ', _indentLevel * 4));
        sb.Append('}');
        return sb.ToString();
    }

    public string VisitIf(IfNode node)
    {
        var sb = new StringBuilder();
        var condition = node.Condition.Accept(this);
        var thenBlock = node.ThenBlock.Accept(this);
        sb.Append($"if ({condition}) {thenBlock}");

        foreach (var (elseIfCondition, elseIfBody) in node.ElseIfClauses)
        {
            var eiCond = elseIfCondition.Accept(this);
            var eiBlock = elseIfBody.Accept(this);
            sb.AppendLine();
            sb.Append(new string(' ', _indentLevel * 4));
            sb.Append($"else if ({eiCond}) {eiBlock}");
        }

        if (node.ElseBlock is not null)
        {
            var elseBlock = node.ElseBlock.Accept(this);
            sb.AppendLine();
            sb.Append(new string(' ', _indentLevel * 4));
            sb.Append($"else {elseBlock}");
        }

        return sb.ToString();
    }

    public string VisitWhile(WhileNode node)
    {
        var condition = node.Condition.Accept(this);
        var body = node.Body.Accept(this);
        return $"while ({condition}) {body}";
    }

    public string VisitFor(ForNode node)
    {
        var init = node.Initializer?.Accept(this).TrimEnd(';') ?? "";
        var condition = node.Condition?.Accept(this) ?? "";
        var update = node.Step?.Accept(this).TrimEnd(';') ?? "";
        var body = node.Body.Accept(this);
        return $"for ({init}; {condition}; {update}) {body}";
    }

    public string VisitReturn(ReturnNode node)
    {
        if (node.Expression is not null)
        {
            var expr = node.Expression.Accept(this);
            return $"return {expr};";
        }
        return "return;";
    }

    public string VisitBreak(BreakNode node)
    {
        return "break;";
    }

    public string VisitContinue(ContinueNode node)
    {
        return "continue;";
    }

    public string VisitFunctionCall(FunctionCallNode node)
    {
        var args = string.Join(", ", node.Arguments.Select(a => a.Accept(this)));
        return $"{node.FunctionName}({args})";
    }

    public string VisitSuffixMethodCall(SuffixMethodCallNode node)
    {
        var args = string.Join(", ", node.Arguments.Select(a => a.Accept(this)));
        var method = ResolveSuffixMethod(node) ?? node.Verb;
        return $"{node.TargetStem}.{method}({args});";
    }

    public string VisitSuffixMethodChain(SuffixMethodChainNode node)
    {
        var parts = node.Steps.Select(step =>
        {
            var arg = step.Argument.Accept(this);
            var method = ResolveSuffixChainMethod(node, step) ?? step.Verb;
            return $"{node.TargetStem}.{method}({arg});";
        }).ToList();

        if (!string.IsNullOrWhiteSpace(node.TailPropertyWord))
        {
            var member = ResolveSuffixChainTailProperty(node) ?? node.TailPropertyWord;
            var writeMethod = node.TailIsWriteLine ? "Console.WriteLine" : "Console.Write";
            parts.Add($"{writeMethod}({node.TargetStem}.{member});");
        }

        return string.Join(" ", parts);
    }

    public string VisitSuffixPropertyAccess(SuffixPropertyAccessNode node)
    {
        var member = ResolveSuffixProperty(node) ?? node.PropertyWord;
        return $"{node.TargetStem}.{member}";
    }

    public string VisitFunctionDefinition(FunctionDefinitionNode node)
    {
        var returnType = node.ReturnType is not null ? MapType(node.ReturnType) : "void";
        var parameters = string.Join(", ", node.Parameters.Select(p => $"{MapType(p.Type)} {p.Name}"));
        var body = node.Body.Accept(this);
        return $"static {returnType} {node.Name}({parameters}) {body}";
    }

    private void AppendLine(string code, int gorLine)
    {
        var indent = new string(' ', _indentLevel * 4);
        _sb.AppendLine($"{indent}{code} /* gör:{gorLine} */");
    }

    private static string MapBinaryOperator(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Esittir => "==",
        BinaryOperator.EsitDegildir => "!=",
        BinaryOperator.Buyuktur => ">",
        BinaryOperator.Kucuktur => "<",
        BinaryOperator.BuyukEsittir => ">=",
        BinaryOperator.KucukEsittir => "<=",
        BinaryOperator.Ve => "&&",
        BinaryOperator.Veya => "||",
        _ => throw new InvalidOperationException($"Bilinmeyen operatör: {op}")
    };

    private string MapType(string turkishType)
    {
        if (_sozluk?.Types.TryGetValue(turkishType, out var entry) == true)
            return entry.CSharp;

        // Fallback for unknown types (user-defined classes)
        return turkishType;
    }

    private string? ResolveSuffixMethod(SuffixMethodCallNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.ResolvedMethod))
        {
            return node.ResolvedMethod;
        }

        if (_sozluk is null || string.IsNullOrWhiteSpace(node.SuffixCase))
        {
            return null;
        }

        if (!_sozluk.Suffixes.TryGetValue(node.SuffixCase, out var suffixEntry))
        {
            return null;
        }

        return suffixEntry.TryResolveVerbMethodName(node.Verb, out var method) ? method : null;
    }

    private string? ResolveSuffixProperty(SuffixPropertyAccessNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.ResolvedMember))
        {
            return node.ResolvedMember;
        }

        if (_sozluk is null || string.IsNullOrWhiteSpace(node.SuffixCase))
        {
            return null;
        }

        if (!_sozluk.Suffixes.TryGetValue(node.SuffixCase, out var suffixEntry))
        {
            return null;
        }

        return suffixEntry.TryResolvePropertyMemberName(node.PropertyWord, out var member) ? member : null;
    }

    private string? ResolveSuffixChainMethod(SuffixMethodChainNode node, SuffixMethodChainStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.ResolvedMethod))
            return step.ResolvedMethod;

        if (_sozluk is null || string.IsNullOrWhiteSpace(node.SuffixCase))
            return null;

        if (!_sozluk.Suffixes.TryGetValue(node.SuffixCase, out var suffixEntry))
            return null;

        return suffixEntry.TryResolveVerbMethodName(step.Verb, out var method) ? method : null;
    }

    private string? ResolveSuffixChainTailProperty(SuffixMethodChainNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.TailResolvedMember))
            return node.TailResolvedMember;

        if (_sozluk is null || string.IsNullOrWhiteSpace(node.TailPropertyWord))
            return null;

        if (!string.IsNullOrWhiteSpace(node.SuffixCase)
            && _sozluk.Suffixes.TryGetValue(node.SuffixCase, out var suffixEntry)
            && suffixEntry.TryResolvePropertyMemberName(node.TailPropertyWord, out var memberFromCase))
        {
            return memberFromCase;
        }

        foreach (var entry in _sozluk.Suffixes.Values)
        {
            if (entry.TryResolvePropertyMemberName(node.TailPropertyWord, out var member))
                return member;
        }

        return null;
    }
}
