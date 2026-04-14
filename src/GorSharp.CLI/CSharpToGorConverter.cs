using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GorSharp.CLI;

public sealed record CSharpImportExplanation(
    int SourceLine,
    string Title,
    string Message,
    string CSharpSnippet,
    string GorSnippet);

public sealed record CSharpImportResult(
    string GorSource,
    IReadOnlyList<CSharpImportExplanation> Explanations);

public static class CSharpToGorConverter
{
    public static string Convert(string csharpSource)
    {
        return ConvertWithNarration(csharpSource).GorSource;
    }

    public static CSharpImportResult ConvertWithNarration(string csharpSource)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpSource, new CSharpParseOptions(LanguageVersion.Latest));
        var root = tree.GetCompilationUnitRoot();
        var converter = new RoslynCSharpToGorConverter();
        return converter.Convert(root);
    }

    private sealed class RoslynCSharpToGorConverter
    {
        private readonly StringBuilder _output = new();
        private readonly List<CSharpImportExplanation> _explanations = new();
        private int _indentLevel;

        public CSharpImportResult Convert(CompilationUnitSyntax root)
        {
            foreach (var trivia in root.GetLeadingTrivia())
            {
                AppendTrivia(trivia);
            }

            foreach (var member in root.Members)
            {
                AppendMember(member);
            }

            return new CSharpImportResult(
                _output.ToString().TrimEnd() + Environment.NewLine,
                _explanations);
        }

        private void AppendMember(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case GlobalStatementSyntax globalStatement:
                    AppendStatement(globalStatement.Statement);
                    break;
                case MethodDeclarationSyntax method:
                    AppendMethod(method);
                    break;
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    foreach (var nested in namespaceDeclaration.Members)
                    {
                        AppendMember(nested);
                    }
                    break;
                case FileScopedNamespaceDeclarationSyntax fileScopedNamespace:
                    foreach (var nested in fileScopedNamespace.Members)
                    {
                        AppendMember(nested);
                    }
                    break;
                case ClassDeclarationSyntax classDeclaration:
                    AppendClass(classDeclaration);
                    break;
                default:
                    AppendUnsupported($"DESTEKLENMEYEN ÜYE: {member.ToString().Trim()}");
                    break;
            }
        }

        private void AppendClass(ClassDeclarationSyntax classDeclaration)
        {
            if (string.Equals(classDeclaration.Identifier.ValueText, "Program", StringComparison.Ordinal))
            {
                foreach (var member in classDeclaration.Members)
                {
                    switch (member)
                    {
                        case MethodDeclarationSyntax method when string.Equals(method.Identifier.ValueText, "Main", StringComparison.Ordinal):
                            AppendMainMethod(method);
                            break;
                        case MethodDeclarationSyntax method:
                            AppendMethod(method);
                            break;
                        default:
                            AppendUnsupported($"DESTEKLENMEYEN PROGRAM ÜYESİ: {member.ToString().Trim()}");
                            break;
                    }
                }

                return;
            }

            AppendUnsupported($"DESTEKLENMEYEN SINIF: {classDeclaration.Identifier.ValueText}");
        }

        private void AppendMainMethod(MethodDeclarationSyntax method)
        {
            if (method.Body is null)
            {
                AppendUnsupported($"DESTEKLENMEYEN MAIN İMZASI: {method.ToString().Trim()}");
                return;
            }

            foreach (var statement in method.Body.Statements)
            {
                AppendStatement(statement);
            }
        }

        private void AppendMethod(MethodDeclarationSyntax method)
        {
            if (method.Body is null)
            {
                AppendUnsupported($"DESTEKLENMEYEN METOT: {method.ToString().Trim()}");
                return;
            }

            var returnType = MapType(method.ReturnType);
            var parameters = method.ParameterList.Parameters.Select(ConvertParameter).ToList();
            if (parameters.Any(parameter => parameter is null))
            {
                AppendUnsupported($"DESTEKLENMEYEN PARAMETRELER: {method.Identifier.ValueText}");
                return;
            }

            var signature = returnType is null
                ? $"fonksiyon {method.Identifier.ValueText}({string.Join(", ", parameters!)})"
                : $"fonksiyon {method.Identifier.ValueText}({string.Join(", ", parameters!)}): {returnType}";

            WriteNarratedLine(
                method,
                $"{signature} {{",
                "Fonksiyon tanımı",
                $"C# içindeki `{method.Identifier.ValueText}` metodu, Gör# içinde `fonksiyon` olarak gösterildi.");
            _indentLevel++;
            foreach (var statement in method.Body.Statements)
            {
                AppendStatement(statement);
            }
            _indentLevel--;
            WriteLine("}");
        }

        private string? ConvertParameter(ParameterSyntax parameter)
        {
            if (parameter.Type is null)
                return null;

            var type = MapType(parameter.Type);
            return type is null ? null : $"{parameter.Identifier.ValueText}: {type}";
        }

        private void AppendStatement(StatementSyntax statement)
        {
            foreach (var trivia in statement.GetLeadingTrivia())
            {
                AppendTrivia(trivia);
            }

            switch (statement)
            {
                case LocalDeclarationStatementSyntax localDeclaration:
                    AppendLocalDeclaration(localDeclaration);
                    break;
                case ExpressionStatementSyntax expressionStatement:
                    AppendExpressionStatement(expressionStatement.Expression, expressionStatement);
                    break;
                case IfStatementSyntax ifStatement:
                    AppendIfStatement(ifStatement);
                    break;
                case WhileStatementSyntax whileStatement:
                    AppendWhileStatement(whileStatement);
                    break;
                case ForStatementSyntax forStatement:
                    AppendForStatement(forStatement);
                    break;
                case ReturnStatementSyntax returnStatement:
                    AppendReturnStatement(returnStatement);
                    break;
                case BreakStatementSyntax:
                    WriteLine("kır;");
                    break;
                case ContinueStatementSyntax:
                    WriteLine("devam;");
                    break;
                case BlockSyntax block:
                    AppendBlock(block);
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    AppendLocalFunction(localFunction);
                    break;
                case EmptyStatementSyntax:
                    break;
                default:
                    AppendUnsupported($"DESTEKLENMEYEN SATIR: {statement.ToString().Trim()}");
                    break;
            }
        }

        private void AppendLocalFunction(LocalFunctionStatementSyntax localFunction)
        {
            var method = SyntaxFactory.MethodDeclaration(localFunction.ReturnType, localFunction.Identifier)
                .WithParameterList(localFunction.ParameterList)
                .WithBody(localFunction.Body);
            AppendMethod(method);
        }

        private void AppendBlock(BlockSyntax block)
        {
            WriteLine("{");
            _indentLevel++;
            foreach (var statement in block.Statements)
            {
                AppendStatement(statement);
            }
            _indentLevel--;
            WriteLine("}");
        }

        private void AppendLocalDeclaration(LocalDeclarationStatementSyntax localDeclaration)
        {
            if (localDeclaration.Declaration.Variables.Count != 1)
            {
                AppendUnsupported($"DESTEKLENMEYEN BİLDİRİM: {localDeclaration.ToString().Trim()}");
                return;
            }

            var variable = localDeclaration.Declaration.Variables[0];
            if (variable.Initializer is null)
            {
                AppendUnsupported($"DESTEKLENMEYEN BİLDİRİM: {localDeclaration.ToString().Trim()}");
                return;
            }

            var value = ConvertExpression(variable.Initializer.Value);
            if (value is null)
            {
                AppendUnsupported($"DESTEKLENMEYEN BİLDİRİM: {localDeclaration.ToString().Trim()}");
                return;
            }

            var declaredType = localDeclaration.Declaration.Type is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "var"
                ? null
                : MapType(localDeclaration.Declaration.Type);

            if (localDeclaration.Declaration.Type is not IdentifierNameSyntax { Identifier.ValueText: "var" } && declaredType is null)
            {
                AppendUnsupported($"DESTEKLENMEYEN TÜR: {localDeclaration.Declaration.Type}");
                return;
            }

            if (declaredType is null)
            {
                WriteNarratedLine(
                    localDeclaration,
                    $"{variable.Identifier.ValueText} {value} olsun;",
                    "Tür çıkarımlı değişken",
                    $"C# içindeki yerel değişken bildirimi, Gör# içinde ad önce yazılan `olsun` biçimine çevrildi.");
            }
            else
            {
                WriteNarratedLine(
                    localDeclaration,
                    $"{variable.Identifier.ValueText}: {declaredType} {value} olsun;",
                    "Türlü değişken bildirimi",
                    $"C# tipi `{localDeclaration.Declaration.Type}` olan bildirim, Gör# içindeki `{declaredType}` tür adına çevrildi.");
            }
        }

        private void AppendExpressionStatement(ExpressionSyntax expression, StatementSyntax originalStatement)
        {
            if (TryConvertConsoleWrite(expression, out var printStatement))
            {
                WriteNarratedLine(
                    originalStatement,
                    printStatement,
                    "Yazdırma",
                    "`Console.Write` ve `Console.WriteLine` çağrıları Gör# içinde SOV yazdırma biçimine çevrildi.");
                return;
            }

            if (expression is AssignmentExpressionSyntax assignment
                && assignment.Left is IdentifierNameSyntax identifier)
            {
                var value = ConvertExpression(assignment.Right);
                if (value is not null)
                {
                    WriteNarratedLine(
                        originalStatement,
                        $"{identifier.Identifier.ValueText} = {value};",
                        "Atama",
                        "C# yeniden ataması Gör# içinde `=` kullanılarak korundu.");
                    return;
                }
            }

            if (expression is InvocationExpressionSyntax invocation)
            {
                var convertedInvocation = ConvertInvocation(invocation);
                if (convertedInvocation is not null)
                {
                    WriteNarratedLine(
                        originalStatement,
                        $"{convertedInvocation};",
                        "Fonksiyon çağrısı",
                        "C# fonksiyon çağrısı, Gör# içinde çağrı biçimi korunarak aktarıldı.");
                    return;
                }
            }

            AppendUnsupported($"DESTEKLENMEYEN SATIR: {originalStatement.ToString().Trim()}");
        }

        private bool TryConvertConsoleWrite(ExpressionSyntax expression, out string statement)
        {
            statement = string.Empty;

            if (expression is not InvocationExpressionSyntax invocation)
                return false;

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return false;

            if (memberAccess.Expression is not IdentifierNameSyntax { Identifier.ValueText: "Console" })
                return false;

            var arguments = invocation.ArgumentList.Arguments;
            if (arguments.Count != 1)
                return false;

            var value = ConvertExpression(arguments[0].Expression);
            if (value is null)
                return false;

            statement = memberAccess.Name.Identifier.ValueText switch
            {
                "WriteLine" => $"{value} yeniSatıraYazdır;",
                "Write" => $"{value} yazdır;",
                _ => string.Empty
            };

            return statement.Length > 0;
        }

        private void AppendIfStatement(IfStatementSyntax ifStatement)
        {
            var condition = ConvertExpression(ifStatement.Condition);
            if (condition is null)
            {
                AppendUnsupported($"DESTEKLENMEYEN IF: {ifStatement.ToString().Trim()}");
                return;
            }

            WriteNarratedLine(
                ifStatement,
                $"eğer {condition} {{",
                "Koşul",
                "C# `if` koşulu, Gör# içinde doğal Türkçe `eğer` biçimine çevrildi.");
            _indentLevel++;
            AppendEmbeddedStatement(ifStatement.Statement);
            _indentLevel--;

            if (ifStatement.Else is null)
            {
                WriteLine("}");
                return;
            }

            if (ifStatement.Else.Statement is IfStatementSyntax elseIf)
            {
                WriteLine($"}} yoksa eğer {ConvertExpression(elseIf.Condition) ?? elseIf.Condition.ToString()} {{");
                _indentLevel++;
                AppendEmbeddedStatement(elseIf.Statement);
                _indentLevel--;
                AppendElseTail(elseIf.Else);
                return;
            }

            WriteLine("} değilse {");
            _indentLevel++;
            AppendEmbeddedStatement(ifStatement.Else.Statement);
            _indentLevel--;
            WriteLine("}");
        }

        private void AppendElseTail(ElseClauseSyntax? elseClause)
        {
            if (elseClause is null)
            {
                WriteLine("}");
                return;
            }

            if (elseClause.Statement is IfStatementSyntax elseIf)
            {
                WriteLine($"}} yoksa eğer {ConvertExpression(elseIf.Condition) ?? elseIf.Condition.ToString()} {{");
                _indentLevel++;
                AppendEmbeddedStatement(elseIf.Statement);
                _indentLevel--;
                AppendElseTail(elseIf.Else);
                return;
            }

            WriteLine("} değilse {");
            _indentLevel++;
            AppendEmbeddedStatement(elseClause.Statement);
            _indentLevel--;
            WriteLine("}");
        }

        private void AppendWhileStatement(WhileStatementSyntax whileStatement)
        {
            var condition = ConvertExpression(whileStatement.Condition);
            if (condition is null)
            {
                AppendUnsupported($"DESTEKLENMEYEN DÖNGÜ: {whileStatement.ToString().Trim()}");
                return;
            }

            WriteNarratedLine(
                whileStatement,
                $"döngü {condition} {{",
                "While döngüsü",
                "C# `while` yapısı, Gör# içinde `döngü` anahtar sözcüğüyle gösterildi.");
            _indentLevel++;
            AppendEmbeddedStatement(whileStatement.Statement);
            _indentLevel--;
            WriteLine("}");
        }

        private void AppendForStatement(ForStatementSyntax forStatement)
        {
            var initializer = ConvertForInitializer(forStatement);
            var condition = forStatement.Condition is null ? string.Empty : ConvertExpression(forStatement.Condition);
            var incrementors = forStatement.Incrementors.Select(ConvertExpression).ToList();

            if (initializer is null || condition is null || incrementors.Any(item => item is null))
            {
                AppendUnsupported($"DESTEKLENMEYEN FOR: {forStatement.ToString().Trim()}");
                return;
            }

            WriteNarratedLine(
                forStatement,
                $"tekrarla ({initializer}; {condition}; {string.Join(", ", incrementors!)}) {{",
                "For döngüsü",
                "C# `for` döngüsü, Gör# içinde `tekrarla` başlığı altında üç parçalı yapı olarak korundu.");
            _indentLevel++;
            AppendEmbeddedStatement(forStatement.Statement);
            _indentLevel--;
            WriteLine("}");
        }

        private string? ConvertForInitializer(ForStatementSyntax forStatement)
        {
            if (forStatement.Declaration is not null)
            {
                if (forStatement.Declaration.Variables.Count != 1)
                    return null;

                var variable = forStatement.Declaration.Variables[0];
                if (variable.Initializer is null)
                    return null;

                var value = ConvertExpression(variable.Initializer.Value);
                if (value is null)
                    return null;

                var declaredType = forStatement.Declaration.Type is IdentifierNameSyntax { Identifier.ValueText: "var" }
                    ? null
                    : MapType(forStatement.Declaration.Type);

                return declaredType is null
                    ? $"{variable.Identifier.ValueText} {value} olsun"
                    : $"{variable.Identifier.ValueText}: {declaredType} {value} olsun";
            }

            if (forStatement.Initializers.Count == 1)
            {
                var initializer = forStatement.Initializers[0];
                if (initializer is AssignmentExpressionSyntax assignment
                    && assignment.Left is IdentifierNameSyntax identifier)
                {
                    var value = ConvertExpression(assignment.Right);
                    return value is null ? null : $"{identifier.Identifier.ValueText} = {value}";
                }
            }

            return null;
        }

        private void AppendReturnStatement(ReturnStatementSyntax returnStatement)
        {
            if (returnStatement.Expression is null)
            {
                WriteNarratedLine(
                    returnStatement,
                    "döndür;",
                    "Boş dönüş",
                    "C# içindeki `return;` ifadesi Gör# içinde `döndür;` olarak aktarıldı.");
                return;
            }

            var expression = ConvertExpression(returnStatement.Expression);
            if (expression is null)
            {
                AppendUnsupported($"DESTEKLENMEYEN RETURN: {returnStatement.ToString().Trim()}");
                return;
            }

            WriteNarratedLine(
                returnStatement,
                $"döndür {expression};",
                "Değer döndürme",
                "C# içindeki `return` ifadesi Gör# içinde `döndür` olarak gösterildi.");
        }

        private void AppendEmbeddedStatement(StatementSyntax statement)
        {
            if (statement is BlockSyntax block)
            {
                foreach (var child in block.Statements)
                {
                    AppendStatement(child);
                }

                return;
            }

            AppendStatement(statement);
        }

        private string? ConvertExpression(ExpressionSyntax expression)
        {
            return expression switch
            {
                LiteralExpressionSyntax literal => ConvertLiteral(literal),
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                ParenthesizedExpressionSyntax parenthesized => ConvertParenthesized(parenthesized),
                BinaryExpressionSyntax binary => ConvertBinary(binary),
                PrefixUnaryExpressionSyntax prefixUnary => ConvertPrefixUnary(prefixUnary),
                InvocationExpressionSyntax invocation => ConvertInvocation(invocation),
                _ => null
            };
        }

        private string? ConvertLiteral(LiteralExpressionSyntax literal)
        {
            return literal.Kind() switch
            {
                SyntaxKind.StringLiteralExpression => literal.Token.Text,
                SyntaxKind.NumericLiteralExpression => literal.Token.Text,
                SyntaxKind.TrueLiteralExpression => "doğru",
                SyntaxKind.FalseLiteralExpression => "yanlış",
                SyntaxKind.NullLiteralExpression => "boş",
                SyntaxKind.CharacterLiteralExpression => literal.Token.Text,
                _ => null
            };
        }

        private string? ConvertParenthesized(ParenthesizedExpressionSyntax parenthesized)
        {
            var expression = ConvertExpression(parenthesized.Expression);
            return expression is null ? null : $"({expression})";
        }

        private string? ConvertBinary(BinaryExpressionSyntax binary)
        {
            var left = ConvertExpression(binary.Left);
            var right = ConvertExpression(binary.Right);
            if (left is null || right is null)
                return null;

            var op = binary.Kind() switch
            {
                SyntaxKind.AddExpression => "+",
                SyntaxKind.SubtractExpression => "-",
                SyntaxKind.MultiplyExpression => "*",
                SyntaxKind.DivideExpression => "/",
                SyntaxKind.ModuloExpression => "%",
                SyntaxKind.EqualsExpression => "eşittir",
                SyntaxKind.NotEqualsExpression => "eşitDeğildir",
                SyntaxKind.GreaterThanExpression => "büyüktür",
                SyntaxKind.GreaterThanOrEqualExpression => "büyükEşittir",
                SyntaxKind.LessThanExpression => "küçüktür",
                SyntaxKind.LessThanOrEqualExpression => "küçükEşittir",
                SyntaxKind.LogicalAndExpression => "ve",
                SyntaxKind.LogicalOrExpression => "veya",
                _ => null
            };

            return op is null ? null : $"{left} {op} {right}";
        }

        private string? ConvertPrefixUnary(PrefixUnaryExpressionSyntax prefixUnary)
        {
            var operand = ConvertExpression(prefixUnary.Operand);
            if (operand is null)
                return null;

            return prefixUnary.Kind() switch
            {
                SyntaxKind.UnaryMinusExpression => $"-{operand}",
                SyntaxKind.LogicalNotExpression => $"değil {operand}",
                _ => null
            };
        }

        private string? ConvertInvocation(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is not IdentifierNameSyntax identifier)
                return null;

            var arguments = invocation.ArgumentList.Arguments
                .Select(argument => ConvertExpression(argument.Expression))
                .ToList();

            if (arguments.Any(argument => argument is null))
                return null;

            return $"{identifier.Identifier.ValueText}({string.Join(", ", arguments!)})";
        }

        private static string? MapType(TypeSyntax typeSyntax)
        {
            return typeSyntax.ToString() switch
            {
                "int" => "sayı",
                "double" => "ondalık",
                "float" => "ondalık",
                "decimal" => "ondalık",
                "string" => "metin",
                "bool" => "mantık",
                "char" => "karakter",
                "void" => null,
                _ => null
            };
        }

        private void AppendTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                WriteLine(trivia.ToString());
            }
        }

        private void AppendUnsupported(string text)
        {
            var line = $"// {text}";
            WriteLine(line);
            _explanations.Add(new CSharpImportExplanation(
                0,
                "Desteklenmeyen yapı",
                "Bu C# yapısı henüz doğrudan Gör# karşılığına çevrilmiyor; yorum olarak bırakıldı.",
                text,
                line));
        }

        private void WriteNarratedLine(SyntaxNode sourceNode, string text, string title, string message)
        {
            WriteLine(text);
            _explanations.Add(new CSharpImportExplanation(
                GetSourceLine(sourceNode),
                title,
                message,
                sourceNode.ToString().Trim(),
                text.Trim()));
        }

        private void WriteLine(string text)
        {
            _output.Append(' ', _indentLevel * 4);
            _output.AppendLine(text);
        }

        private static int GetSourceLine(SyntaxNode sourceNode)
        {
            return sourceNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }
    }
}
