using System.Globalization;
using System.Text;
using GorSharp.Core.Ast;
using GorSharp.Parser;

namespace GorSharp.LanguageServer.Services;

public class FormatterService
{
    private readonly GorSharpParserService _parser = new();

    public string? Format(string sourceCode, string fileName)
    {
        var (ast, diagnostics) = _parser.Parse(sourceCode, fileName);
        if (diagnostics.Any(d => d.Severity == Core.Diagnostics.DiagnosticSeverity.Error))
            return null;

        var formatter = new GorSharpFormatter();
        return formatter.Format(ast);
    }
}

internal sealed class GorSharpFormatter : IAstVisitor<string>
{
    private int _indentLevel;

    public string Format(ProgramNode program)
    {
        _indentLevel = 0;
        return string.Join("\n\n", program.Statements.Select(FormatStatement));
    }

    public string VisitProgram(ProgramNode node) => Format(node);

    public string VisitAssignment(AssignmentNode node)
    {
        var value = node.Value.Accept(this);
        if (!node.IsDeclaration)
            return $"{node.Name} = {value};";

        if (!string.IsNullOrWhiteSpace(node.ExplicitType))
            return $"{node.Name}: {node.ExplicitType} {value} olsun;";

        return $"{node.Name} {value} olsun;";
    }

    public string VisitLiteral(LiteralNode node)
    {
        return node.LiteralType switch
        {
            LiteralType.Integer => Convert.ToString(node.Value, CultureInfo.InvariantCulture) ?? "0",
            LiteralType.Double => Convert.ToDouble(node.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            LiteralType.String => $"\"{node.Value}\"",
            LiteralType.Boolean => (bool)node.Value! ? "doğru" : "yanlış",
            LiteralType.Null => "boş",
            _ => throw new InvalidOperationException($"Bilinmeyen literal tipi: {node.LiteralType}")
        };
    }

    public string VisitIdentifier(IdentifierNode node) => node.Name;

    public string VisitBinaryExpression(BinaryExpressionNode node)
    {
        var left = node.Left.Accept(this);
        var right = node.Right.Accept(this);
        return $"{left} {MapBinaryOperator(node.Operator)} {right}";
    }

    public string VisitUnaryExpression(UnaryExpressionNode node)
    {
        var operand = node.Operand.Accept(this);
        return node.Operator switch
        {
            UnaryOperator.Negate => $"-{operand}",
            UnaryOperator.Degil => $"değil {operand}",
            _ => throw new InvalidOperationException($"Bilinmeyen tekli operatör: {node.Operator}")
        };
    }

    public string VisitPrint(PrintNode node)
    {
        var expression = node.Expression.Accept(this);
        return node.IsWriteLine ? $"{expression} yeniSatıraYazdır;" : $"{expression} yazdır;";
    }

    public string VisitBlock(BlockNode node)
    {
        if (node.Statements.Count == 0)
            return "{\n}";

        var builder = new StringBuilder();
        builder.AppendLine("{");
        _indentLevel++;

        for (var i = 0; i < node.Statements.Count; i++)
        {
            if (i > 0)
                builder.AppendLine();

            builder.Append(Indent());
            builder.Append(FormatStatement(node.Statements[i]));
        }

        _indentLevel--;
        builder.AppendLine();
        builder.Append(Indent());
        builder.Append('}');
        return builder.ToString();
    }

    public string VisitIf(IfNode node)
    {
        var builder = new StringBuilder();
        builder.Append($"eğer {node.Condition.Accept(this)} ");
        builder.Append(node.ThenBlock.Accept(this));

        foreach (var (condition, block) in node.ElseIfClauses)
        {
            builder.AppendLine();
            builder.Append(Indent());
            builder.Append($"yoksa eğer {condition.Accept(this)} ");
            builder.Append(block.Accept(this));
        }

        if (node.ElseBlock is not null)
        {
            builder.AppendLine();
            builder.Append(Indent());
            builder.Append("değilse ");
            builder.Append(node.ElseBlock.Accept(this));
        }

        return builder.ToString();
    }

    public string VisitWhile(WhileNode node)
    {
        return $"döngü {node.Condition.Accept(this)} {node.Body.Accept(this)}";
    }

    public string VisitFor(ForNode node)
    {
        var initializer = node.Initializer?.Accept(this).TrimEnd(';') ?? string.Empty;
        var condition = node.Condition?.Accept(this) ?? string.Empty;
        var step = node.Step?.Accept(this).TrimEnd(';') ?? string.Empty;
        return $"tekrarla ({initializer}; {condition}; {step}) {node.Body.Accept(this)}";
    }

    public string VisitReturn(ReturnNode node)
    {
        return node.Expression is null ? "döndür;" : $"döndür {node.Expression.Accept(this)};";
    }

    public string VisitBreak(BreakNode node) => "kır;";

    public string VisitContinue(ContinueNode node) => "devam;";

    public string VisitFunctionCall(FunctionCallNode node)
    {
        var arguments = string.Join(", ", node.Arguments.Select(argument => argument.Accept(this)));
        return $"{node.FunctionName}({arguments})";
    }

    public string VisitSuffixMethodCall(SuffixMethodCallNode node)
    {
        var arguments = string.Join(" ", node.Arguments.Select(argument => argument.Accept(this)));
        return $"{node.TargetToken} {arguments} {node.Verb};";
    }

    public string VisitSuffixMethodChain(SuffixMethodChainNode node)
    {
        var head = node.Steps[0];
        var segments = new List<string>
        {
            $"{node.TargetToken} {head.Argument.Accept(this)} {head.Verb}"
        };

        for (var i = 1; i < node.Steps.Count; i++)
        {
            var step = node.Steps[i];
            segments.Add($"sonra {step.Argument.Accept(this)} {step.Verb}");
        }

        if (!string.IsNullOrWhiteSpace(node.TailPropertyWord))
        {
            var printKeyword = node.TailIsWriteLine ? "yeniSatıraYazdır" : "yazdır";
            segments.Add($"sonra {node.TailPropertyWord} {printKeyword}");
        }

        return string.Join(" ", segments) + ";";
    }

    public string VisitSuffixPropertyAccess(SuffixPropertyAccessNode node)
    {
        return $"{node.TargetToken} {node.PropertyWord}";
    }

    public string VisitFunctionDefinition(FunctionDefinitionNode node)
    {
        var parameters = string.Join(", ", node.Parameters.Select(parameter => $"{parameter.Name}: {parameter.Type}"));
        var returnType = string.IsNullOrWhiteSpace(node.ReturnType) ? string.Empty : $": {node.ReturnType}";
        return $"fonksiyon {node.Name}({parameters}){returnType} {node.Body.Accept(this)}";
    }

    public string VisitSuffixedExpression(SuffixedExpressionNode node)
    {
        // Format the underlying expression, suffix is semantic only
        return node.Expression.Accept(this);
    }

    private string FormatStatement(AstNode statement)
    {
        var formatted = statement.Accept(this);
        return NormalizeMultiline(formatted);
    }

    private string NormalizeMultiline(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0)
            return string.Empty;

        var builder = new StringBuilder();
        builder.Append(lines[0]);

        for (var i = 1; i < lines.Length; i++)
        {
            builder.AppendLine();
            builder.Append(Indent());
            builder.Append(lines[i]);
        }

        return builder.ToString();
    }

    private string Indent() => new(' ', _indentLevel * 4);

    private static string MapBinaryOperator(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Esittir => "eşittir",
        BinaryOperator.EsitDegildir => "eşitDeğildir",
        BinaryOperator.Buyuktur => "büyüktür",
        BinaryOperator.Kucuktur => "küçüktür",
        BinaryOperator.BuyukEsittir => "büyükEşittir",
        BinaryOperator.KucukEsittir => "küçükEşittir",
        BinaryOperator.Ve => "ve",
        BinaryOperator.Veya => "veya",
        _ => throw new InvalidOperationException($"Bilinmeyen operatör: {op}")
    };
}
