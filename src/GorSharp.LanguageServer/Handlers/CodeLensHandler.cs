using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using GorSharp.LanguageServer.Services;
using Newtonsoft.Json.Linq;

namespace GorSharp.LanguageServer.Handlers;

/// <summary>
/// Provides Code Lens to show reference counts for functions and variables.
/// Displays "X references" above definitions that are referenced in the code.
/// </summary>
public class CodeLensHandler : CodeLensHandlerBase
{
    private readonly DocumentStore _store;
    private readonly SymbolAnalysisService _symbols;

    public CodeLensHandler(DocumentStore store, SymbolAnalysisService symbols)
    {
        _store = store;
        _symbols = symbols;
    }

    public override Task<CodeLensContainer?> Handle(CodeLensParams request, CancellationToken cancellationToken)
    {
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
            return Task.FromResult<CodeLensContainer?>(null);

        var analysis = _symbols.Analyze(doc.Text, doc.Uri.ToString());
        var lenses = new List<CodeLens>();

        // For each declaration, count its references and create a lens
        foreach (var declaration in analysis.Symbols.Declarations)
        {
            // Only show lenses for functions (optionally variables too)
            if (declaration.Kind != GSymbolKind.Function)
                continue;

            // Count how many references this declaration has
            var references = analysis.Symbols.FindAllOccurrencesForDeclaration(declaration.Id);
            var referenceCount = references.Count(r => !r.IsDeclaration); // exclude the declaration itself

            if (referenceCount == 0)
                continue; // Don't show lens for unused declarations

            var lens = new CodeLens
            {
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new Position(declaration.Location.Line - 1, declaration.Location.Column),
                    End = new Position(
                        declaration.Location.Line - 1,
                        declaration.Location.Column + declaration.Name.Length)
                },
                Command = new Command
                {
                    Name = "editor.action.showReferences",
                    Title = referenceCount switch
                    {
                        1 => "1 referans",
                        _ => $"{referenceCount} referans"
                    },
                    Arguments = JArray.FromObject(new object?[]
                    {
                        request.TextDocument.Uri.ToString(),
                        new { Line = declaration.Location.Line - 1, Character = declaration.Location.Column },
                        new[] { new { Line = declaration.Location.Line - 1, Character = declaration.Location.Column } }
                    })
                }
            };

            lenses.Add(lens);
        }

        return Task.FromResult<CodeLensContainer?>(lenses.Any() ? new CodeLensContainer(lenses) : null);
    }

    // This method is called to resolve any additional information for a code lens
    public override Task<CodeLens> Handle(CodeLens request, CancellationToken cancellationToken)
    {
        // The code lens is already fully populated in Handle(CodeLensParams)
        return Task.FromResult(request);
    }

    protected override CodeLensRegistrationOptions CreateRegistrationOptions(
        CodeLensCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            ResolveProvider = false // We fully populate lenses in Handle(CodeLensParams)
        };
}
