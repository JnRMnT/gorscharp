using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GorSharp.CLI;

internal static class CSharpToGorConverter
{
    private static readonly Regex WriteLineRegex = new(@"^Console\.WriteLine\((.*)\);$", RegexOptions.Compiled);
    private static readonly Regex WriteRegex = new(@"^Console\.Write\((.*)\);$", RegexOptions.Compiled);
    private static readonly Regex TypedDeclarationRegex = new(@"^(var|int|double|string|bool)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+);$", RegexOptions.Compiled);
    private static readonly Regex AssignmentRegex = new(@"^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+);$", RegexOptions.Compiled);

    public static string Convert(string csharpSource)
    {
        var output = new StringBuilder();
        var lines = csharpSource.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                output.AppendLine();
                continue;
            }

            if (line.StartsWith("//", StringComparison.Ordinal))
            {
                output.AppendLine(rawLine);
                continue;
            }

            var writeLineMatch = WriteLineRegex.Match(line);
            if (writeLineMatch.Success)
            {
                output.AppendLine($"{writeLineMatch.Groups[1].Value} yeniSatıraYazdır;");
                continue;
            }

            var writeMatch = WriteRegex.Match(line);
            if (writeMatch.Success)
            {
                output.AppendLine($"{writeMatch.Groups[1].Value} yazdır;");
                continue;
            }

            var typedDeclMatch = TypedDeclarationRegex.Match(line);
            if (typedDeclMatch.Success)
            {
                var csharpType = typedDeclMatch.Groups[1].Value;
                var name = typedDeclMatch.Groups[2].Value;
                var value = typedDeclMatch.Groups[3].Value;
                var gorType = MapType(csharpType, value);

                if (gorType is null)
                {
                    output.AppendLine($"// DESTEKLENMEYEN SATIR: {rawLine}");
                }
                else if (csharpType == "var")
                {
                    output.AppendLine($"{name} {value} olsun;");
                }
                else
                {
                    output.AppendLine($"{name}: {gorType} {value} olsun;");
                }
                continue;
            }

            var assignmentMatch = AssignmentRegex.Match(line);
            if (assignmentMatch.Success)
            {
                var name = assignmentMatch.Groups[1].Value;
                var value = assignmentMatch.Groups[2].Value;
                output.AppendLine($"{name} {value} olsun;");
                continue;
            }

            output.AppendLine($"// DESTEKLENMEYEN SATIR: {rawLine}");
        }

        return output.ToString();
    }

    private static string? MapType(string csharpType, string value)
    {
        return csharpType switch
        {
            "int" => "sayı",
            "double" => "ondalıklı",
            "string" => "metin",
            "bool" => "mantıksal",
            "var" => InferType(value),
            _ => null
        };
    }

    private static string? InferType(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
            return "metin";

        if (trimmed is "true" or "false")
            return "mantıksal";

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return "sayı";

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return "ondalıklı";

        return null;
    }
}
