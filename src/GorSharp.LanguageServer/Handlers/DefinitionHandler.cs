using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

public class DefinitionHandler : DefinitionHandlerBase
{
    private readonly DocumentStore _store;
    private readonly SymbolAnalysisService _symbols;

    public DefinitionHandler(DocumentStore store, SymbolAnalysisService symbols)
    {
        _store = store;
        _symbols = symbols;
    }

    public override Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
    {
        LanguageServerTrace.Info($"Definition requested for {request.TextDocument.Uri} at {request.Position.Line}:{request.Position.Character}");
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
        {
            LanguageServerTrace.Warning($"Definition skipped because document was not found in store: {request.TextDocument.Uri}");
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        var filePath = request.TextDocument.Uri.GetFileSystemPath() ?? request.TextDocument.Uri.ToString();
        var analysis = _symbols.Analyze(doc.Text, filePath);

        var occurrence = analysis.Symbols.FindOccurrenceAt((int)request.Position.Line, (int)request.Position.Character);
        if (occurrence is null)
        {
            var word = ExtractWordAtPosition(doc.Text, (int)request.Position.Line, (int)request.Position.Character);
            if (string.IsNullOrWhiteSpace(word))
            {
                LanguageServerTrace.Warning("Definition found no symbol occurrence at the requested position.");
                return Task.FromResult<LocationOrLocationLinks?>(null);
            }

            var fallbackDeclaration = analysis.Symbols.Declarations
                .Where(d => d.Name == word)
                .OrderByDescending(d => d.Kind == GSymbolKind.Function)
                .ThenBy(d => d.Location.Line)
                .FirstOrDefault();

            if (fallbackDeclaration is null)
            {
                LanguageServerTrace.Warning($"Definition fallback found no declaration for '{word}'.");
                return Task.FromResult<LocationOrLocationLinks?>(null);
            }

            LanguageServerTrace.Info($"Definition fallback resolved '{word}' to line {fallbackDeclaration.Location.Line}.");
            var fallbackRange = ToLineRange(doc.Text, fallbackDeclaration.Location);
            var fallbackLocation = new Location
            {
                Uri = request.TextDocument.Uri,
                Range = fallbackRange
            };
            LanguageServerTrace.Info($"Definition returning Location uri={fallbackLocation.Uri} range={FormatRange(fallbackRange)}");
            return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(
                new[] { ToLocationOrLocationLink(fallbackLocation) }));
        }

        var declarationId = occurrence.ResolvedDeclarationId;
        if (declarationId is null)
        {
            LanguageServerTrace.Warning($"Definition found no declaration for '{occurrence.Name}'.");
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        var declaration = analysis.Symbols.FindDeclarationById(declarationId.Value);
        if (declaration is null)
        {
            LanguageServerTrace.Warning($"Definition could not resolve declaration id {declarationId.Value}.");
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        LanguageServerTrace.Info($"Definition resolved '{occurrence.Name}' to line {declaration.Location.Line}.");

        var range = ToLineRange(doc.Text, declaration.Location);
        var location = new Location
        {
            Uri = request.TextDocument.Uri,
            Range = range
        };
        LanguageServerTrace.Info($"Definition returning Location uri={location.Uri} range={FormatRange(range)}");
        return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(
            new[] { ToLocationOrLocationLink(location) }));
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(
        DefinitionCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp")
        };

    private static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range ToLineRange(
        string sourceText,
        Core.Ast.SourceLocation location)
    {
        var lines = sourceText.Replace("\r\n", "\n").Split('\n');
        var line = Math.Max(location.Line - 1, 0);
        if (line >= lines.Length)
        {
            line = Math.Max(lines.Length - 1, 0);
        }

        var lineLength = lines.Length == 0 ? 1 : Math.Max(lines[line].Length, 1);
        var start = 0;
        var length = lineLength;
        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            new Position(line, start),
            new Position(line, start + length));
    }

    private static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range ToSymbolRange(
        string sourceText,
        Core.Ast.SourceLocation location,
        string symbolName)
    {
        var lines = sourceText.Replace("\r\n", "\n").Split('\n');
        var line = Math.Max(location.Line - 1, 0);
        if (line >= lines.Length)
        {
            line = Math.Max(lines.Length - 1, 0);
        }

        var lineLength = lines.Length == 0 ? 1 : Math.Max(lines[line].Length, 1);
        var start = Math.Clamp(location.Column, 0, lineLength - 1);
        var end = Math.Clamp(start + Math.Max(symbolName.Length, 1), start + 1, lineLength);

        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            new Position(line, start),
            new Position(line, end));
    }

    private static string FormatRange(OmniSharp.Extensions.LanguageServer.Protocol.Models.Range range)
    {
        return $"({range.Start.Line},{range.Start.Character})-({range.End.Line},{range.End.Character})";
    }

    private static LocationOrLocationLink ToLocationOrLocationLink(Location location)
    {
        return new LocationOrLocationLink(location);
    }

    private static string ExtractWordAtPosition(string text, int line, int character)
    {
        var lines = text.Split('\n');
        if (line < 0 || line >= lines.Length)
            return string.Empty;

        var lineText = lines[line];
        if (character < 0 || character >= lineText.Length)
            return string.Empty;

        var wordIndex = GetNearestWordCharIndex(lineText, character);
        if (wordIndex < 0)
            return string.Empty;

        int start = wordIndex;
        int end = wordIndex;

        while (start > 0 && IsWordChar(lineText[start - 1]))
            start--;
        while (end < lineText.Length - 1 && IsWordChar(lineText[end + 1]))
            end++;

        return lineText[start..(end + 1)];
    }

    private static int GetNearestWordCharIndex(string lineText, int character)
    {
        if (lineText.Length == 0)
            return -1;

        if (character >= 0 && character < lineText.Length && IsWordChar(lineText[character]))
            return character;

        if (character - 1 >= 0 && character - 1 < lineText.Length && IsWordChar(lineText[character - 1]))
            return character - 1;

        if (character + 1 >= 0 && character + 1 < lineText.Length && IsWordChar(lineText[character + 1]))
            return character + 1;

        return -1;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' ||
               c == 'ö' || c == 'ü' || c == 'ş' || c == 'ç' || c == 'ı' || c == 'ğ' ||
               c == 'Ö' || c == 'Ü' || c == 'Ş' || c == 'Ç' || c == 'İ' || c == 'Ğ';
    }
}
