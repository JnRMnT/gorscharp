using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using GorSharp.LanguageServer.Services;

namespace GorSharp.LanguageServer.Handlers;

public class CompletionHandler : CompletionHandlerBase
{
    private readonly DocumentStore _store;
    private readonly TranspilationService _transpiler;
    private readonly SymbolAnalysisService _symbols;

    public CompletionHandler(
        DocumentStore store,
        TranspilationService transpiler,
        SymbolAnalysisService symbols)
    {
        _store = store;
        _transpiler = transpiler;
        _symbols = symbols;
    }

    public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        LanguageServerTrace.Info($"Completion requested for {request.TextDocument.Uri} at {request.Position.Line}:{request.Position.Character}");
        var items = new List<CompletionItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // 1) User-defined symbols from current document (functions, variables, parameters).
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is not null)
        {
            var filePath = request.TextDocument.Uri.GetFileSystemPath() ?? request.TextDocument.Uri.ToString();
            var analysis = _symbols.Analyze(doc.Text, filePath);
            var candidates = analysis.Symbols.GetCompletionCandidatesAt(
                (int)request.Position.Line,
                (int)request.Position.Character);

            foreach (var declaration in candidates)
            {
                if (!seen.Add(declaration.Name))
                    continue;

                var kind = declaration.Kind switch
                {
                    GSymbolKind.Function => CompletionItemKind.Function,
                    GSymbolKind.Parameter => CompletionItemKind.Variable,
                    _ => CompletionItemKind.Variable
                };

                var detail = declaration.Kind switch
                {
                    GSymbolKind.Function => BuildFunctionDetail(analysis.Symbols, declaration.Name),
                    GSymbolKind.Parameter => BuildValueDetail("Parametre", declaration.InferredType),
                    _ => BuildValueDetail("Kullanıcı değişkeni", declaration.InferredType)
                };

                items.Add(new CompletionItem
                {
                    Label = declaration.Name,
                    Kind = kind,
                    Detail = detail,
                    InsertText = declaration.Name
                });
            }
        }

        // 2) Turkish keywords from sozluk.json
        var sozluk = _transpiler.Sozluk;
        if (sozluk is not null)
        {
            foreach (var (key, entry) in sozluk.Keywords)
            {
                if (!seen.Add(key))
                    continue;

                items.Add(new CompletionItem
                {
                    Label = key,
                    Kind = CompletionItemKind.Keyword,
                    Detail = $"→ {entry.CSharp}",
                    Documentation = entry.Tooltip is not null
                        ? new MarkupContent
                        {
                            Kind = MarkupKind.Markdown,
                            Value = FormatTooltip(entry.Tooltip)
                        }
                        : null,
                    InsertText = key
                });
            }

            foreach (var (key, entry) in sozluk.Types)
            {
                if (!seen.Add(key))
                    continue;

                items.Add(new CompletionItem
                {
                    Label = key,
                    Kind = CompletionItemKind.TypeParameter,
                    Detail = $"→ {entry.CSharp}",
                    Documentation = entry.Tooltip is not null
                        ? new MarkupContent
                        {
                            Kind = MarkupKind.Markdown,
                            Value = FormatTooltip(entry.Tooltip)
                        }
                        : null,
                    InsertText = key
                });
            }

            foreach (var (key, entry) in sozluk.Operators)
            {
                if (!seen.Add(key))
                    continue;

                items.Add(new CompletionItem
                {
                    Label = key,
                    Kind = CompletionItemKind.Operator,
                    Detail = $"→ {entry.CSharp}",
                    InsertText = key
                });
            }
        }
        else
        {
            // Fallback: hardcoded minimal set if sozluk not loaded
            var fallback = new[]
            {
                ("yazdır", "Console.Write"),
                ("yeniSatıraYazdır", "Console.WriteLine"),
                ("oku", "Console.Read"),
                ("okuSatır", "Console.ReadLine"),
                ("eğer", "if"),
                ("değilse", "else"),
                ("yoksa eğer", "else if"),
                ("döngü", "while"),
                ("tekrarla", "for"),
                ("fonksiyon", "method"),
                ("döndür", "return"),
                ("olsun", "= (assignment)"),
                ("doğru", "true"),
                ("yanlış", "false"),
                ("boş", "null"),
                ("kır", "break"),
                ("devam", "continue"),
            };

            foreach (var (turkish, csharp) in fallback)
            {
                if (!seen.Add(turkish))
                    continue;

                items.Add(new CompletionItem
                {
                    Label = turkish,
                    Kind = CompletionItemKind.Keyword,
                    Detail = $"→ {csharp}",
                    InsertText = turkish
                });
            }
        }

        LanguageServerTrace.Info($"Completion produced {items.Count} items for {request.TextDocument.Uri}");
        return Task.FromResult(new CompletionList(items));
    }

    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken) =>
        Task.FromResult(request);

    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            TriggerCharacters = new Container<string>("'", " "),
            ResolveProvider = false
        };

    private static string FormatTooltip(Core.Sozluk.TooltipData tooltip)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(tooltip.Title))
            parts.Add($"**{tooltip.Title}**");

        if (!string.IsNullOrEmpty(tooltip.Description))
            parts.Add(tooltip.Description);

        if (tooltip.Example is not null)
        {
            parts.Add($"```gorsharp\n{tooltip.Example.Gor}\n```");
            parts.Add($"→ C#:\n```csharp\n{tooltip.Example.CSharp}\n```");
        }

        return string.Join("\n\n", parts);
    }

    private static string BuildFunctionDetail(SymbolTable symbols, string functionName)
    {
        var signature = symbols.FindFunctionSignature(functionName);
        if (signature is null)
            return "Kullanıcı fonksiyonu";

        var returnType = string.IsNullOrWhiteSpace(signature.ReturnType) ? "boş" : signature.ReturnType;
        return $"Kullanıcı fonksiyonu: {returnType}";
    }

    private static string BuildValueDetail(string prefix, string? inferredType)
    {
        return string.IsNullOrWhiteSpace(inferredType)
            ? prefix
            : $"{prefix}: {inferredType}";
    }
}
