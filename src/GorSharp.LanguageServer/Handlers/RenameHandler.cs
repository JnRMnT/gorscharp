using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

public class RenameHandler : RenameHandlerBase
{
    private readonly DocumentStore _store;
    private readonly SymbolAnalysisService _symbols;

    public RenameHandler(DocumentStore store, SymbolAnalysisService symbols)
    {
        _store = store;
        _symbols = symbols;
    }

    public override Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
    {
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
            return Task.FromResult<WorkspaceEdit?>(new WorkspaceEdit());

        var filePath = request.TextDocument.Uri.GetFileSystemPath() ?? request.TextDocument.Uri.ToString();
        var analysis = _symbols.Analyze(doc.Text, filePath);

        var occurrence = analysis.Symbols.FindOccurrenceAt((int)request.Position.Line, (int)request.Position.Character);
        if (occurrence?.ResolvedDeclarationId is null)
            return Task.FromResult<WorkspaceEdit?>(new WorkspaceEdit());

        var all = analysis.Symbols.FindAllOccurrencesForDeclaration(occurrence.ResolvedDeclarationId.Value);

        var edits = all.Select(o => new TextEdit
        {
            Range = ToRange(o.Location, o.Name),
            NewText = request.NewName
        }).ToList();

        var changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
        {
            [request.TextDocument.Uri] = edits
        };

        return Task.FromResult<WorkspaceEdit?>(new WorkspaceEdit { Changes = changes });
    }

    protected override RenameRegistrationOptions CreateRegistrationOptions(
        RenameCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            PrepareProvider = false
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
