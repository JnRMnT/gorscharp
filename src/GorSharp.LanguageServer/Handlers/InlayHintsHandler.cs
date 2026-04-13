using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

/// <summary>
/// Provides inline inferred-type hints for variable declarations.
/// Example: x 5 olsun; -> x: sayı
/// </summary>
public class InlayHintsHandler : InlayHintsHandlerBase
{
    private readonly DocumentStore _store;
    private readonly SymbolAnalysisService _symbols;

    public InlayHintsHandler(DocumentStore store, SymbolAnalysisService symbols)
    {
        _store = store;
        _symbols = symbols;
        LanguageServerTrace.Info("★★★ InlayHintsHandler CONSTRUCTOR CALLED ★★★");
    }

    public override Task<InlayHintContainer?> Handle(InlayHintParams request, CancellationToken cancellationToken)
    {
        LanguageServerTrace.Info($"▶ InlayHints.Handle() invoked");
        LanguageServerTrace.Info($"  File: {request.TextDocument.Uri}");
        LanguageServerTrace.Info($"  Range: {(request.Range is null ? "NONE (full document)" : $"({request.Range.Start.Line},{request.Range.Start.Character}) to ({request.Range.End.Line},{request.Range.End.Character})")}");
        
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
        {
            LanguageServerTrace.Warning($"  ✗ Document not found in store");
            return Task.FromResult<InlayHintContainer?>(null);
        }
        LanguageServerTrace.Info($"  ✓ Document loaded, {doc.Text.Length} chars");

        var analysis = _symbols.Analyze(doc.Text, doc.Uri.ToString());
        var hints = new List<InlayHint>();

        LanguageServerTrace.Info($"  Analysis: {analysis.Symbols.Declarations.Count} declarations found");

        int variablesProcessed = 0;
        int variablesWithType = 0;
        int variablesInRange = 0;
        int hintsAdded = 0;

        foreach (var declaration in analysis.Symbols.Declarations)
        {
            if (declaration.Kind != GSymbolKind.Variable)
                continue;

            variablesProcessed++;
            var inferredType = declaration.InferredType;
            
            LanguageServerTrace.Info($"    [{variablesProcessed}] {declaration.Name} at ({declaration.Location.Line},{declaration.Location.Column})");

            if (string.IsNullOrWhiteSpace(inferredType))
            {
                LanguageServerTrace.Info($"      → no inferred type");
                continue;
            }
            
            variablesWithType++;
            LanguageServerTrace.Info($"      → type: {inferredType}");

            var line = declaration.Location.Line - 1;
            var character = declaration.Location.Column + declaration.Name.Length;

            if (!IsWithinRange(request.Range, line, character))
            {
                var rangeInfo = request.Range is null 
                    ? "null range should include all" 
                    : $"outside range {request.Range.Start.Line}-{request.Range.End.Line}";
                LanguageServerTrace.Info($"      → FILTERED OUT ({rangeInfo})");
                continue;
            }

            variablesInRange++;

            LanguageServerTrace.Info($"      → HINT ADDED at ({line},{character})");

            hints.Add(new InlayHint
            {
                Position = new Position(line, character),
                Label = new StringOrInlayHintLabelParts($": {inferredType}"),
                Kind = InlayHintKind.Type,
                PaddingLeft = true,
                PaddingRight = false,
            });
            
            hintsAdded++;
        }

        LanguageServerTrace.Info($"  Summary: variables={variablesProcessed}, withType={variablesWithType}, inRange={variablesInRange}, hints={hintsAdded}");
        LanguageServerTrace.Info($"◀ InlayHints.Handle() returning {hints.Count} hints");
        
        return Task.FromResult<InlayHintContainer?>(hints.Count == 0 ? null : new InlayHintContainer(hints));
    }

    public override Task<InlayHint> Handle(InlayHint request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request);
    }

    protected override InlayHintRegistrationOptions CreateRegistrationOptions(
        InlayHintClientCapabilities capability,
        ClientCapabilities clientCapabilities)
    {
        LanguageServerTrace.Info($"InlayHints: CreateRegistrationOptions called");
        if (capability is not null)
        {
            LanguageServerTrace.Info($"  InlayHint client capability: DynamicRegistration={capability.DynamicRegistration}");
        }
        else
        {
            LanguageServerTrace.Info($"  InlayHint client capability: NULL");
        }
        
        return new InlayHintRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            ResolveProvider = false,
        };
    }

    private static bool IsWithinRange(OmniSharp.Extensions.LanguageServer.Protocol.Models.Range? range, int line, int character)
    {
        // If no range specified, include all positions
        if (range is null)
            return true;

        if (line < range.Start.Line || line > range.End.Line)
            return false;

        if (line == range.Start.Line && character < range.Start.Character)
            return false;

        if (line == range.End.Line && character > range.End.Character)
            return false;

        return true;
    }
}