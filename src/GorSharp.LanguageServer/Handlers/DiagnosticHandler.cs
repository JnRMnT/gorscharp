using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using GorSharp.LanguageServer.Services;

namespace GorSharp.LanguageServer.Handlers;

/// <summary>
/// Diagnostic handler — registered as a handler but actual diagnostics
/// are pushed from <see cref="TextDocumentSyncHandler"/> via publish.
/// This handler serves pull-based diagnostic requests if the client supports them.
/// </summary>
public class DiagnosticHandler : DocumentDiagnosticHandlerBase
{
    private readonly Services.DocumentStore _store;
    private readonly Services.TranspilationService _transpiler;

    public DiagnosticHandler(Services.DocumentStore store, Services.TranspilationService transpiler)
    {
        _store = store;
        _transpiler = transpiler;
    }

    public override Task<RelatedDocumentDiagnosticReport> Handle(
        DocumentDiagnosticParams request,
        CancellationToken cancellationToken)
    {
        LanguageServerTrace.Info($"DocumentDiagnostic requested for {request.TextDocument.Uri}");
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
        {
            LanguageServerTrace.Warning($"DocumentDiagnostic skipped because document was not found in store: {request.TextDocument.Uri}");
            return Task.FromResult<RelatedDocumentDiagnosticReport>(
                new RelatedFullDocumentDiagnosticReport
                {
                    Items = new Container<Diagnostic>()
                });
        }

        var result = _transpiler.Transpile(doc.Text, request.TextDocument.Uri.GetFileSystemPath() ?? "input.gör");
        LanguageServerTrace.Info($"DocumentDiagnostic returning {result.Diagnostics.Count} diagnostics for {request.TextDocument.Uri}");
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

        return Task.FromResult<RelatedDocumentDiagnosticReport>(
            new RelatedFullDocumentDiagnosticReport
            {
                Items = new Container<Diagnostic>(diagnostics)
            });
    }

    protected override DiagnosticsRegistrationOptions CreateRegistrationOptions(
        DiagnosticClientCapabilities capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            Identifier = "gorsharp",
            InterFileDependencies = false,
            WorkspaceDiagnostics = false
        };
}
