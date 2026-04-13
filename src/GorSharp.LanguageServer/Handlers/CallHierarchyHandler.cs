using GorSharp.Core.Ast;
using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

public class CallHierarchyHandler : CallHierarchyHandlerBase
{
    private readonly DocumentStore _store;
    private readonly SymbolAnalysisService _symbols;

    public CallHierarchyHandler(DocumentStore store, SymbolAnalysisService symbols)
    {
        _store = store;
        _symbols = symbols;
    }

    public override Task<Container<CallHierarchyItem>?> Handle(
        CallHierarchyPrepareParams request,
        CancellationToken cancellationToken)
    {
        var (analysis, occurrence, uri) = GetContext(request.TextDocument.Uri, request.Position.Line, request.Position.Character);
        if (analysis is null || occurrence?.ResolvedDeclarationId is null)
            return Task.FromResult<Container<CallHierarchyItem>?>(new Container<CallHierarchyItem>());

        var declaration = analysis.Symbols.FindDeclarationById(occurrence.ResolvedDeclarationId.Value);
        if (declaration is null || declaration.Kind != GSymbolKind.Function)
            return Task.FromResult<Container<CallHierarchyItem>?>(new Container<CallHierarchyItem>());

        var item = ToCallHierarchyItem(uri, declaration, analysis.Symbols);
        return Task.FromResult<Container<CallHierarchyItem>?>(new Container<CallHierarchyItem>(item));
    }

    public override Task<Container<CallHierarchyIncomingCall>?> Handle(
        CallHierarchyIncomingCallsParams request,
        CancellationToken cancellationToken)
    {
        var (analysis, declaration) = GetDeclarationForItem(request.Item);
        if (analysis is null || declaration is null)
            return Task.FromResult<Container<CallHierarchyIncomingCall>?>(new Container<CallHierarchyIncomingCall>());

        var incomingCalls = analysis.Symbols.CallEdges
            .Where(edge => edge.CalleeDeclarationId == declaration.Id)
            .GroupBy(edge => edge.CallerDeclarationId)
            .Select(group =>
            {
                var caller = analysis.Symbols.FindDeclarationById(group.Key);
                if (caller is null)
                    return null;

                return new CallHierarchyIncomingCall
                {
                    From = ToCallHierarchyItem(request.Item.Uri, caller, analysis.Symbols),
                    FromRanges = new Container<OmniSharp.Extensions.LanguageServer.Protocol.Models.Range>(
                        group.Select(edge => ToRange(edge.CallLocation, caller.Name)).ToArray())
                };
            })
            .Where(call => call is not null)
            .Cast<CallHierarchyIncomingCall>()
            .ToArray();

        return Task.FromResult<Container<CallHierarchyIncomingCall>?>(new Container<CallHierarchyIncomingCall>(incomingCalls));
    }

    public override Task<Container<CallHierarchyOutgoingCall>?> Handle(
        CallHierarchyOutgoingCallsParams request,
        CancellationToken cancellationToken)
    {
        var (analysis, declaration) = GetDeclarationForItem(request.Item);
        if (analysis is null || declaration is null)
            return Task.FromResult<Container<CallHierarchyOutgoingCall>?>(new Container<CallHierarchyOutgoingCall>());

        var outgoingCalls = analysis.Symbols.CallEdges
            .Where(edge => edge.CallerDeclarationId == declaration.Id)
            .GroupBy(edge => edge.CalleeDeclarationId)
            .Select(group =>
            {
                var callee = analysis.Symbols.FindDeclarationById(group.Key);
                if (callee is null)
                    return null;

                return new CallHierarchyOutgoingCall
                {
                    To = ToCallHierarchyItem(request.Item.Uri, callee, analysis.Symbols),
                    FromRanges = new Container<OmniSharp.Extensions.LanguageServer.Protocol.Models.Range>(
                        group.Select(edge => ToRange(edge.CallLocation, callee.Name)).ToArray())
                };
            })
            .Where(call => call is not null)
            .Cast<CallHierarchyOutgoingCall>()
            .ToArray();

        return Task.FromResult<Container<CallHierarchyOutgoingCall>?>(new Container<CallHierarchyOutgoingCall>(outgoingCalls));
    }

    protected override CallHierarchyRegistrationOptions CreateRegistrationOptions(
        CallHierarchyCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp")
        };

    private (SymbolAnalysisResult? Analysis, SymbolOccurrence? Occurrence, DocumentUri Uri) GetContext(
        DocumentUri uri,
        int line,
        int character)
    {
        var document = _store.Get(uri.ToString());
        if (document is null)
            return (null, null, uri);

        var filePath = uri.GetFileSystemPath() ?? uri.ToString();
        var analysis = _symbols.Analyze(document.Text, filePath);
        var occurrence = analysis.Symbols.FindOccurrenceAt(line, character);
        return (analysis, occurrence, uri);
    }

    private (SymbolAnalysisResult? Analysis, SymbolRecord? Declaration) GetDeclarationForItem(CallHierarchyItem item)
    {
        var document = _store.Get(item.Uri.ToString());
        if (document is null)
            return (null, null);

        var filePath = item.Uri.GetFileSystemPath() ?? item.Uri.ToString();
        var analysis = _symbols.Analyze(document.Text, filePath);
        var declaration = analysis.Symbols.Declarations.FirstOrDefault(d =>
            d.Kind == GSymbolKind.Function &&
            d.Name == item.Name &&
            d.Location.Line == item.SelectionRange.Start.Line + 1 &&
            d.Location.Column == item.SelectionRange.Start.Character);

        return (analysis, declaration);
    }

    private static CallHierarchyItem ToCallHierarchyItem(DocumentUri uri, SymbolRecord declaration, SymbolTable symbols)
    {
        var detail = symbols.FindFunctionSignature(declaration.Name)?.ReturnType;

        return new CallHierarchyItem
        {
            Name = declaration.Name,
            Kind = SymbolKind.Function,
            Detail = string.IsNullOrWhiteSpace(detail) ? "fonksiyon" : $"fonksiyon: {detail}",
            Uri = uri,
            Range = ToRange(declaration.Location, declaration.Name),
            SelectionRange = ToRange(declaration.Location, declaration.Name)
        };
    }

    private static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range ToRange(SourceLocation location, string name)
    {
        var line = Math.Max(location.Line - 1, 0);
        var start = Math.Max(location.Column, 0);
        var length = location.Length > 0 ? location.Length : Math.Max(name.Length, 1);

        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            new Position(line, start),
            new Position(line, start + length));
    }
}