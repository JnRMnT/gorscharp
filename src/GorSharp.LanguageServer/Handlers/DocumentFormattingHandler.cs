using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

public class DocumentFormattingHandler : DocumentFormattingHandlerBase
{
    private readonly DocumentStore _store;
    private readonly FormatterService _formatter;

    public DocumentFormattingHandler(DocumentStore store, FormatterService formatter)
    {
        _store = store;
        _formatter = formatter;
    }

    public override Task<TextEditContainer?> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
    {
        var document = _store.Get(request.TextDocument.Uri.ToString());
        if (document is null)
            return Task.FromResult<TextEditContainer?>(new TextEditContainer());

        var filePath = request.TextDocument.Uri.GetFileSystemPath() ?? request.TextDocument.Uri.ToString();
        var formatted = _formatter.Format(document.Text, filePath);
        if (formatted is null || string.Equals(formatted, document.Text, StringComparison.Ordinal))
            return Task.FromResult<TextEditContainer?>(new TextEditContainer());

        var lines = document.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var lastLineIndex = Math.Max(lines.Length - 1, 0);
        var lastCharacter = lines.Length == 0 ? 0 : lines[lastLineIndex].Length;

        var edits = new TextEditContainer(
            new TextEdit
            {
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(0, 0),
                    new Position(lastLineIndex, lastCharacter)),
                NewText = formatted
            });

        return Task.FromResult<TextEditContainer?>(edits);
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentFormattingCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp")
        };
}
