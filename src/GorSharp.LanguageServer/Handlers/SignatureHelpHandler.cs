using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

public class SignatureHelpHandler : SignatureHelpHandlerBase
{
    private readonly DocumentStore _store;
    private readonly SymbolAnalysisService _symbols;
    private readonly BuiltInSignaturesService _builtInSignatures;

    public SignatureHelpHandler(DocumentStore store, SymbolAnalysisService symbols, BuiltInSignaturesService builtInSignatures)
    {
        _store = store;
        _symbols = symbols;
        _builtInSignatures = builtInSignatures;
    }

    public override Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
    {
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
            return Task.FromResult<SignatureHelp?>(null);

        var offset = GetOffset(doc.Text, (int)request.Position.Line, (int)request.Position.Character);
        if (!TryGetCallContext(doc.Text, offset, out var functionName, out var activeParameter))
            return Task.FromResult<SignatureHelp?>(null);

        var filePath = request.TextDocument.Uri.GetFileSystemPath() ?? request.TextDocument.Uri.ToString();
        var analysis = _symbols.Analyze(doc.Text, filePath);
        
        // Try user-defined functions first
        var signature = analysis.Symbols.FindFunctionSignature(functionName);
        if (signature is not null)
        {
            return Task.FromResult<SignatureHelp?>(BuildSignatureHelp(signature, activeParameter, returnType: signature.ReturnType));
        }

        // Fall back to built-in functions
        if (_builtInSignatures.TryGetBuiltInSignature(functionName, out var builtInSig))
        {
            return Task.FromResult<SignatureHelp?>(BuildBuiltInSignatureHelp(builtInSig, activeParameter));
        }

        return Task.FromResult<SignatureHelp?>(null);
    }

    private SignatureHelp? BuildSignatureHelp(
        FunctionSignatureRecord signature,
        int activeParameter,
        string? returnType = null)
    {
        var parameters = signature.Parameters
            .Select(p => new ParameterInformation
            {
                Label = p.Name,
                Documentation = new StringOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"Tür: `{p.Type}`"
                })
            })
            .ToList();

        var label = returnType is null
            ? $"{signature.Name}({string.Join(", ", signature.Parameters.Select(p => $"{p.Name}: {p.Type}"))})"
            : $"{signature.Name}({string.Join(", ", signature.Parameters.Select(p => $"{p.Name}: {p.Type}"))}): {returnType}";

        var help = new SignatureHelp
        {
            ActiveSignature = 0,
            ActiveParameter = Math.Min(activeParameter, Math.Max(parameters.Count - 1, 0)),
            Signatures = new Container<SignatureInformation>(new SignatureInformation
            {
                Label = label,
                Parameters = new Container<ParameterInformation>(parameters)
            })
        };

        return help;
    }

    private SignatureHelp? BuildBuiltInSignatureHelp(BuiltInFunctionSignature builtInSig, int activeParameter)
    {
        var parameters = builtInSig.Parameters
            .Select(p => new ParameterInformation
            {
                Label = p.Name,
                Documentation = new StringOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"Tür: `{p.Type}`"
                })
            })
            .ToList();

        var label = string.IsNullOrEmpty(builtInSig.ReturnType)
            ? $"{builtInSig.Name}({string.Join(", ", builtInSig.Parameters.Select(p => $"{p.Name}: {p.Type}"))})"
            : $"{builtInSig.Name}({string.Join(", ", builtInSig.Parameters.Select(p => $"{p.Name}: {p.Type}"))}): {builtInSig.ReturnType}";

        var documentation = string.IsNullOrEmpty(builtInSig.Tooltip)
            ? null
            : new StringOrMarkupContent(new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = builtInSig.Tooltip
            });

        var help = new SignatureHelp
        {
            ActiveSignature = 0,
            ActiveParameter = Math.Min(activeParameter, Math.Max(parameters.Count - 1, 0)),
            Signatures = new Container<SignatureInformation>(new SignatureInformation
            {
                Label = label,
                Documentation = documentation,
                Parameters = new Container<ParameterInformation>(parameters)
            })
        };

        return help;
    }

    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(
        SignatureHelpCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            TriggerCharacters = new Container<string>("(", ",")
        };

    private static int GetOffset(string text, int line, int character)
    {
        if (line <= 0)
            return Math.Min(character, text.Length);

        var currentLine = 0;
        var offset = 0;
        while (offset < text.Length && currentLine < line)
        {
            if (text[offset] == '\n')
                currentLine++;
            offset++;
        }

        return Math.Min(offset + character, text.Length);
    }

    private static bool TryGetCallContext(string text, int cursorOffset, out string functionName, out int activeParameter)
    {
        functionName = string.Empty;
        activeParameter = 0;

        if (cursorOffset < 0 || cursorOffset > text.Length)
            return false;

        var depth = 0;
        var openParen = -1;

        for (var i = cursorOffset - 1; i >= 0; i--)
        {
            var ch = text[i];
            if (ch == ')')
            {
                depth++;
                continue;
            }

            if (ch == '(')
            {
                if (depth == 0)
                {
                    openParen = i;
                    break;
                }

                depth--;
            }
        }

        if (openParen < 0)
            return false;

        // Read function identifier before '('
        var end = openParen - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end])) end--;
        if (end < 0) return false;

        var start = end;
        while (start >= 0 && IsIdentifierChar(text[start])) start--;
        start++;

        if (start > end)
            return false;

        functionName = text[start..(end + 1)];

        // Count commas at depth 0 between '(' and cursor
        var commaCount = 0;
        var nested = 0;
        for (var i = openParen + 1; i < cursorOffset; i++)
        {
            var ch = text[i];
            if (ch == '(') nested++;
            else if (ch == ')' && nested > 0) nested--;
            else if (ch == ',' && nested == 0) commaCount++;
        }

        activeParameter = commaCount;
        return true;
    }

    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c)
               || c == '_'
               || c == 'ö' || c == 'ü' || c == 'ş' || c == 'ç' || c == 'ı' || c == 'ğ'
               || c == 'Ö' || c == 'Ü' || c == 'Ş' || c == 'Ç' || c == 'İ' || c == 'Ğ';
    }
}
