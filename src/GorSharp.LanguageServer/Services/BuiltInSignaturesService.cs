using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GorSharp.LanguageServer.Services;

/// <summary>
/// Service that loads and provides access to built-in function signatures from sözlük.json.
/// Used for Signature Help and IntelliSense with built-in functions like yazdır, oku, etc.
/// </summary>
public class BuiltInSignaturesService
{
    private readonly Dictionary<string, BuiltInFunctionSignature> _signatures;

    public BuiltInSignaturesService()
    {
        _signatures = new Dictionary<string, BuiltInFunctionSignature>(StringComparer.Ordinal);
        LoadSignatures();
    }

    public bool TryGetBuiltInSignature(string functionName, out BuiltInFunctionSignature signature)
    {
        return _signatures.TryGetValue(functionName, out signature!);
    }

    private void LoadSignatures()
    {
        try
        {
            var sozlukPath = Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "dictionaries",
                "sozluk.json");

            if (!File.Exists(sozlukPath))
                return;

            var json = File.ReadAllText(sozlukPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("signatures", out var signaturesElement) &&
                signaturesElement.TryGetProperty("functions", out var functionsElement))
            {
                foreach (var funcProp in functionsElement.EnumerateObject())
                {
                    var funcName = funcProp.Name;
                    var funcValue = funcProp.Value;

                    var parameters = new List<FunctionParameter>();
                    if (funcValue.TryGetProperty("parameters", out var paramsElement))
                    {
                        foreach (var param in paramsElement.EnumerateArray())
                        {
                            var paramName = param.TryGetProperty("name", out var pn) ? pn.GetString() ?? "" : "";
                            var paramType = param.TryGetProperty("type", out var pt) ? pt.GetString() ?? "" : "";
                            parameters.Add(new FunctionParameter { Name = paramName, Type = paramType });
                        }
                    }

                    var returnType = "";
                    if (funcValue.TryGetProperty("returnType", out var rtElement))
                        returnType = rtElement.GetString() ?? "";

                    var tooltip = "";
                    if (funcValue.TryGetProperty("tooltip", out var ttElement))
                        tooltip = ttElement.GetString() ?? "";

                    _signatures[funcName] = new BuiltInFunctionSignature
                    {
                        Name = funcName,
                        Parameters = parameters,
                        ReturnType = returnType,
                        Tooltip = tooltip
                    };
                }
            }
        }
        catch
        {
            // Silent failure — signatures are optional for functionality
        }
    }
}

public record FunctionParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public record BuiltInFunctionSignature
{
    public string Name { get; set; } = "";
    public List<FunctionParameter> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = "";
    public string Tooltip { get; set; } = "";
}
