using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using GorSharp.LanguageServer.Services;
using System.Threading;
using System.Threading.Tasks;

namespace GorSharp.LanguageServer.Handlers;

/// <summary>
/// Prepare Rename handler — validates that the symbol at the cursor can be renamed.
/// Called by F2 (or Ctrl+H rename) before sending the actual rename request.
/// Returns the range of the symbol that will be renamed, or null if not renameable.
/// </summary>
public class PrepareRenameHandler : PrepareRenameHandlerBase
{
    private readonly DocumentStore _documentStore;
    private readonly SymbolAnalysisService _symbolAnalysis;

    // Reserved keywords that cannot be renamed
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
    {
        // Control flow
        "eğer", "yoksa", "döngü", "tekrarla", "her",
        // Keywords
        "olsun", "döndür", "kır", "devam", "fonksiyon",
        "sınıf", "kurucu", "miras", "bu",
        "dene", "hata_varsa", "sonunda",
        // Operators (in Turkish)
        "ve", "veya", "değil", 
        "eşittir", "büyüktür", "küçüktür",
        "büyükEşittir", "küçükEşittir", "eşitDeğildir",
        // Built-in types
        "sayı", "metin", "mantık", "onlu",
        // Built-in functions
        "yazdır", "yeniSatıraYazdır", "oku", "okuSatır"
    };

    public PrepareRenameHandler(DocumentStore documentStore, SymbolAnalysisService symbolAnalysis)
    {
        _documentStore = documentStore;
        _symbolAnalysis = symbolAnalysis;
    }

    public override Task<RangeOrPlaceholderRange?> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri;
        var content = _documentStore.Get(uri.ToString());
        if (content?.Text == null) 
            return Task.FromResult<RangeOrPlaceholderRange?>(null);

        var filePath = uri.GetFileSystemPath() ?? uri.ToString();
        var analysis = _symbolAnalysis.Analyze(content.Text, filePath);

        // Find the symbol at the cursor position
        var occurrence = analysis.Symbols.FindOccurrenceAt((int)request.Position.Line, (int)request.Position.Character);
        if (occurrence == null)
            return Task.FromResult<RangeOrPlaceholderRange?>(null);

        var symbolName = occurrence.Name;

        // Check if the symbol is renameable (not a reserved keyword)
        if (ReservedKeywords.Contains(symbolName))
        {
            // Cannot rename reserved keywords — return null to disable rename
            return Task.FromResult<RangeOrPlaceholderRange?>(null);
        }

        // Check if this is a reference (not a declaration) — still renameable
        // Both declarations and references can be renamed (all occurrences updated)

        // Return the range of the symbol name for visual feedback in the editor
        var location = occurrence.Location;
        var line = Math.Max(location.Line - 1, 0);
        var start = Math.Max(location.Column, 0);
        var length = location.Length > 0 ? location.Length : Math.Max(symbolName.Length, 1);

        var range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
            new Position(line, start),
            new Position(line, start + length));

        // PlaceholderRange provides both a range and placeholder text for inline editing
        var result = new RangeOrPlaceholderRange(new PlaceholderRange
        {
            Range = range,
            Placeholder = symbolName
        });

        return Task.FromResult<RangeOrPlaceholderRange?>(result);
    }

    protected override RenameRegistrationOptions CreateRegistrationOptions(
        RenameCapability? capability,
        ClientCapabilities clientCapabilities)
    {
        return new RenameRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp"),
            PrepareProvider = true
        };
    }
}
