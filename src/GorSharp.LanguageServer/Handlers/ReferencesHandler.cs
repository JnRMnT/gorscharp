using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

public class ReferencesHandler : ReferencesHandlerBase
{
    private readonly DocumentStore _store;
    private readonly SymbolAnalysisService _symbols;

    public ReferencesHandler(DocumentStore store, SymbolAnalysisService symbols)
    {
        _store = store;
        _symbols = symbols;
    }

    public override Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken)
    {
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
            return Task.FromResult<LocationContainer?>(new LocationContainer());

        var filePath = request.TextDocument.Uri.GetFileSystemPath() ?? request.TextDocument.Uri.ToString();
        var analysis = _symbols.Analyze(doc.Text, filePath);

        var occurrence = analysis.Symbols.FindOccurrenceAt((int)request.Position.Line, (int)request.Position.Character);
        if (occurrence?.ResolvedDeclarationId is null)
            return Task.FromResult<LocationContainer?>(new LocationContainer());

        var all = analysis.Symbols.FindAllOccurrencesForDeclaration(occurrence.ResolvedDeclarationId.Value);
        var includeDeclaration = request.Context.IncludeDeclaration;

        var locations = all
            .Where(o => includeDeclaration || !o.IsDeclaration)
            .Select(o => new Location
            {
                Uri = request.TextDocument.Uri,
                Range = ToRange(o.Location, o.Name)
            })
            .ToList();

        return Task.FromResult<LocationContainer?>(new LocationContainer(locations));
    }

    protected override ReferenceRegistrationOptions CreateRegistrationOptions(
        ReferenceCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp")
        };

    private static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range ToRange(
        Core.Ast.SourceLocation location,
        string name)
    {
        var line = Math.Max(location.Line - 1, 0);
        var start = Math.Max(location.Column, 0);
        var length = location.Length > 0 ? location.Length : Math.Max(name.Length, 1);

        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            new Position(line, start),
            new Position(line, start + length));
    }
}
