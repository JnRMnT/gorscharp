using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using GorSharp.LanguageServer.Services;

namespace GorSharp.LanguageServer.Handlers;

public class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly DocumentStore _store;
    private readonly DiagnosticHandler _diagnostics;
    private readonly TranspilationService _transpiler;
    private readonly ILanguageServerFacade _server;

    public TextDocumentSyncHandler(
        DocumentStore store,
        DiagnosticHandler diagnostics,
        TranspilationService transpiler,
        ILanguageServerFacade server)
    {
        _store = store;
        _diagnostics = diagnostics;
        _transpiler = transpiler;
        _server = server;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
        new(uri, "gorsharp");

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var doc = request.TextDocument;
        LanguageServerTrace.Info($"DidOpen received for {doc.Uri}");
        _store.Open(doc.Uri.ToString(), doc.Text, doc.Version ?? 0);

        // Try to discover sozluk.json from the document's workspace
        TryLoadSozluk(doc.Uri);

        PublishDiagnostics(doc.Uri, doc.Text);
        SendMirrorUpdate(doc.Uri, doc.Text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        LanguageServerTrace.Info($"DidChange received for {uri}");
        var text = request.ContentChanges.Last().Text;
        _store.Update(uri, text, request.TextDocument.Version ?? 0);

        PublishDiagnostics(request.TextDocument.Uri, text);
        SendMirrorUpdate(request.TextDocument.Uri, text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken) =>
        Unit.Task;

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        LanguageServerTrace.Info($"DidClose received for {request.TextDocument.Uri}");
        _store.Close(request.TextDocument.Uri.ToString());

        // Clear diagnostics on close
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions { IncludeText = false }
        };

    private void PublishDiagnostics(DocumentUri uri, string text)
    {
        var result = _transpiler.Transpile(text, uri.GetFileSystemPath() ?? uri.ToString());
        LanguageServerTrace.Info($"Publishing {result.Diagnostics.Count} diagnostics for {uri}");
        var diagnostics = result.Diagnostics.Select(d => new Diagnostic
        {
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(d.Line - 1, d.Column),
                new Position(d.Line - 1, d.Column + 1)),
            Severity = d.Severity switch
            {
                Core.Diagnostics.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
                Core.Diagnostics.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                _ => DiagnosticSeverity.Information
            },
            Code = d.Code,
            Source = "gorsharp",
            Message = d.Message
        }).ToList();

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = new Container<Diagnostic>(diagnostics)
        });
    }

    private void SendMirrorUpdate(DocumentUri uri, string text)
    {
        var result = _transpiler.Transpile(text, uri.GetFileSystemPath() ?? uri.ToString());
        if (result.CSharpCode is not null)
        {
            LanguageServerTrace.Info($"Sending mirror update for {uri}");
            _server.SendNotification("gorsharp/mirror", new
            {
                uri = uri.ToString(),
                csharp = result.CSharpCode
            });
        }
    }

    private void TryLoadSozluk(DocumentUri uri)
    {
        if (_transpiler.Sozluk is not null) return;

        var filePath = uri.GetFileSystemPath();
        if (filePath is null) return;

        var dir = Path.GetDirectoryName(filePath);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "dictionaries", "sozluk.json");
            if (File.Exists(candidate))
            {
                LanguageServerTrace.Info($"Loading sozluk from {candidate}");
                _transpiler.LoadSozluk(candidate);
                return;
            }
            dir = Path.GetDirectoryName(dir);
        }
    }
}
