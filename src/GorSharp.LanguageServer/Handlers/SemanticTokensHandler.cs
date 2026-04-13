using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.LanguageServer.Handlers;

/// <summary>
/// Provides semantic token full-document classification for .gör files.
/// This gives Visual Studio and VS Code full syntax highlighting for Gör# keywords.
/// </summary>
public class SemanticTokensHandler : SemanticTokensHandlerBase
{
    // Index must match the order in SemanticTokensLegend.TokenTypes below.
    private static readonly string[] TokenTypeNames =
    [
        "keyword",    // 0
        "type",       // 1
        "function",   // 2
        "variable",   // 3
        "parameter",  // 4
        "string",     // 5
        "number",     // 6
        "operator",   // 7
        "comment",    // 8
        "enumMember", // 9
    ];

    private static int KindToIndex(SemanticTokenKind kind) => kind switch
    {
        SemanticTokenKind.Keyword    => 0,
        SemanticTokenKind.Type       => 1,
        SemanticTokenKind.Function   => 2,
        SemanticTokenKind.Variable   => 3,
        SemanticTokenKind.Parameter  => 4,
        SemanticTokenKind.String     => 5,
        SemanticTokenKind.Number     => 6,
        SemanticTokenKind.Operator   => 7,
        SemanticTokenKind.Comment    => 8,
        SemanticTokenKind.EnumMember => 9,
        _                            => 3,
    };

    private readonly DocumentStore _documents;

    public SemanticTokensHandler(DocumentStore documents)
    {
        _documents = documents;
    }

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new SemanticTokensRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(
                TextDocumentFilter.ForPattern("**/*.gör"),
                TextDocumentFilter.ForLanguage("gorsharp")),
            Legend = new SemanticTokensLegend
            {
                TokenTypes = new Container<SemanticTokenType>(
                    TokenTypeNames.Select(t => new SemanticTokenType(t))),
                TokenModifiers = new Container<SemanticTokenModifier>(),
            },
            Full = true,
            Range = false,
        };
    }

    protected override Task Tokenize(
        SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier,
        CancellationToken cancellationToken)
    {
        LanguageServerTrace.Info($"Semantic tokens requested for {identifier.TextDocument.Uri}");
        var doc = _documents.Get(identifier.TextDocument.Uri.ToString());
        if (doc is null)
        {
            LanguageServerTrace.Warning($"Semantic tokens skipped because document was not found in store: {identifier.TextDocument.Uri}");
            return Task.CompletedTask;
        }

        var spans = GorSharpTokenClassifier.Classify(doc.Text);
        LanguageServerTrace.Info($"Semantic token classifier produced {spans.Count} spans for {identifier.TextDocument.Uri}");
        var lines = doc.Text.Split('\n');

        // Build line/column lookup from character offset.
        var lineStartOffsets = new int[lines.Length];
        int offset = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            lineStartOffsets[i] = offset;
            offset += lines[i].Length + 1; // +1 for '\n'
        }

        foreach (var span in spans)
        {
            // Find which line this span starts on.
            int line = FindLine(lineStartOffsets, span.StartOffset);
            int character = span.StartOffset - lineStartOffsets[line];

            // Multi-line tokens (e.g. block comments) must be split per line.
            var tokenText = doc.Text.Substring(span.StartOffset, span.Length);
            var tokenLines = tokenText.Split('\n');

            for (int tl = 0; tl < tokenLines.Length; tl++)
            {
                int tokenLen = tokenLines[tl].TrimEnd('\r').Length;
                if (tokenLen == 0) continue;

                builder.Push(
                    line + tl,
                    tl == 0 ? character : 0,
                    tokenLen,
                    KindToIndex(span.Kind),
                    0);
            }
        }

        return Task.CompletedTask;
    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params,
        CancellationToken cancellationToken)
    {
        // Retrieve the legend from the cached registration options.
        var legend = new SemanticTokensLegend
        {
            TokenTypes = new Container<SemanticTokenType>(
                TokenTypeNames.Select(t => new SemanticTokenType(t))),
            TokenModifiers = new Container<SemanticTokenModifier>(),
        };
        return Task.FromResult(new SemanticTokensDocument(legend));
    }

    private static int FindLine(int[] lineStartOffsets, int targetOffset)
    {
        int lo = 0, hi = lineStartOffsets.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (lineStartOffsets[mid] <= targetOffset)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo;
    }
}
