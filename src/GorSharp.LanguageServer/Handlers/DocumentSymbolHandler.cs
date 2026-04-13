using GorSharp.Core.Ast;
using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

public class DocumentSymbolHandler : DocumentSymbolHandlerBase
{
    private readonly DocumentStore _store;
    private readonly SymbolAnalysisService _symbols;

    public DocumentSymbolHandler(DocumentStore store, SymbolAnalysisService symbols)
    {
        _store = store;
        _symbols = symbols;
    }

    public override Task<SymbolInformationOrDocumentSymbolContainer?> Handle(
        DocumentSymbolParams request,
        CancellationToken cancellationToken)
    {
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
            return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(new SymbolInformationOrDocumentSymbolContainer());

        var filePath = request.TextDocument.Uri.GetFileSystemPath() ?? request.TextDocument.Uri.ToString();
        var analysis = _symbols.Analyze(doc.Text, filePath);

        var items = new List<SymbolInformationOrDocumentSymbol>();
        foreach (var statement in analysis.Ast.Statements)
        {
            if (statement is FunctionDefinitionNode fn)
            {
                var parameterSymbols = new List<DocumentSymbol>();

                foreach (var parameter in fn.Parameters)
                {
                    parameterSymbols.Add(new DocumentSymbol
                    {
                        Name = parameter.Name,
                        Kind = SymbolKind.Variable,
                        Range = ToRange(fn.Location, parameter.Name),
                        SelectionRange = ToRange(fn.Location, parameter.Name),
                        Detail = $"parametre: {parameter.Type}"
                    });
                }

                var symbol = new DocumentSymbol
                {
                    Name = fn.Name,
                    Kind = SymbolKind.Function,
                    Range = ToRange(fn.Location, fn.Name),
                    SelectionRange = ToRange(fn.Location, fn.Name),
                    Detail = fn.ReturnType is null ? "fonksiyon" : $"fonksiyon: {fn.ReturnType}",
                    Children = new Container<DocumentSymbol>(parameterSymbols)
                };

                items.Add(new SymbolInformationOrDocumentSymbol(symbol));
                continue;
            }

            if (statement is AssignmentNode a && a.IsDeclaration)
            {
                var declaration = analysis.Symbols.Declarations.FirstOrDefault(d =>
                    d.Kind == GSymbolKind.Variable &&
                    d.Name == a.Name &&
                    d.Location == a.Location);

                var typeDetail = a.ExplicitType ?? declaration?.InferredType;
                var symbol = new DocumentSymbol
                {
                    Name = a.Name,
                    Kind = SymbolKind.Variable,
                    Range = ToRange(a.Location, a.Name),
                    SelectionRange = ToRange(a.Location, a.Name),
                    Detail = string.IsNullOrWhiteSpace(typeDetail) ? "değişken" : $"değişken: {typeDetail}"
                };

                items.Add(new SymbolInformationOrDocumentSymbol(symbol));
            }
        }

        return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(new SymbolInformationOrDocumentSymbolContainer(items));
    }

    protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(
        DocumentSymbolCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp")
        };

    private static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range ToRange(
        SourceLocation location,
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
