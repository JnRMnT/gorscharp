using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using GorSharp.LanguageServer.Services;

namespace GorSharp.LanguageServer.Handlers;

/// <summary>
/// Provides hover information for symbols, showing type hints, definitions, and keyword documentation.
/// </summary>
public class HoverHandler : HoverHandlerBase
{
    private readonly DocumentStore _store;
    private readonly TranspilationService _transpiler;
    private readonly BuiltInSignaturesService _builtInSignatures;
    private readonly SymbolAnalysisService _symbols;

    public HoverHandler(
        DocumentStore store,
        TranspilationService transpiler,
        BuiltInSignaturesService builtInSignatures,
        SymbolAnalysisService symbols)
    {
        _store = store;
        _transpiler = transpiler;
        _builtInSignatures = builtInSignatures;
        _symbols = symbols;
    }

    public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var pos = $"{request.Position.Line}:{request.Position.Character}";
        LanguageServerTrace.Info($"Hover requested for {request.TextDocument.Uri} at {pos}");

        LanguageServerTrace.Info($"Hover [{pos}] stage: store lookup");
        var doc = _store.Get(request.TextDocument.Uri.ToString());
        if (doc is null)
        {
            LanguageServerTrace.Warning($"Hover skipped because document was not found in store: {request.TextDocument.Uri}");
            return Task.FromResult<Hover?>(null);
        }

        // First try symbol analysis (user-defined symbols with inferred types)
        LanguageServerTrace.Info($"Hover [{pos}] stage: symbol analysis start");
        var analysis = _symbols.Analyze(doc.Text, doc.Uri.ToString());
        LanguageServerTrace.Info($"Hover [{pos}] stage: symbol analysis done");

        var occurrence = analysis.Symbols.FindOccurrenceAt(
            (int)request.Position.Line,
            (int)request.Position.Character);
        LanguageServerTrace.Info($"Hover [{pos}] stage: FindOccurrenceAt done, found={occurrence is not null}");

        if (occurrence is not null)
        {
            var hover = BuildSymbolHover(occurrence, analysis.Symbols);
            if (hover is not null)
            {
                LanguageServerTrace.Info($"Hover resolved from symbol analysis for '{occurrence.Name}'.");
                return Task.FromResult<Hover?>(hover);
            }
        }

        // Fallback: resolve by extracted word for function signatures first
        LanguageServerTrace.Info($"Hover [{pos}] stage: ExtractWordAtPosition");
        var word = ExtractWordAtPosition(doc.Text, (int)request.Position.Line, (int)request.Position.Character);
        LanguageServerTrace.Info($"Hover [{pos}] stage: ExtractWordAtPosition done, word='{word}'");
        if (string.IsNullOrEmpty(word))
        {
            LanguageServerTrace.Warning("Hover found no word at the requested position.");
            return Task.FromResult<Hover?>(null);
        }

        LanguageServerTrace.Info($"Hover [{pos}] stage: FindFunctionSignature for '{word}'");
        var functionSig = analysis.Symbols.FindFunctionSignature(word);
        LanguageServerTrace.Info($"Hover [{pos}] stage: FindFunctionSignature done, found={functionSig is not null}");
        if (functionSig is not null)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"**fonksiyon** `{word}`\n\n");
            if (functionSig.Parameters.Any())
            {
                sb.Append("**Parametreler:**\n");
                foreach (var param in functionSig.Parameters)
                {
                    sb.Append($"- `{param.Name}`: {param.Type}\n");
                }
                sb.Append("\n");
            }

            if (!string.IsNullOrWhiteSpace(functionSig.ReturnType))
            {
                sb.Append($"**Geri Dönüş:** {functionSig.ReturnType}");
            }

            return Task.FromResult<Hover?>(new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = sb.ToString()
                })
            });
        }

        // Fallback: search sozlük for keywords and types
        LanguageServerTrace.Info($"Hover [{pos}] stage: sozluk lookup for '{word}'");
        var sozluk = _transpiler.Sozluk;
        if (sozluk is null)
        {
            LanguageServerTrace.Warning("Hover skipped because sozluk is not loaded.");
            return Task.FromResult<Hover?>(null);
        }

        string? markdown = null;

        if (sozluk.Keywords.TryGetValue(word, out var kwEntry))
        {
            markdown = FormatKeywordHover(word, kwEntry);
        }
        else if (sozluk.Types.TryGetValue(word, out var typeEntry))
        {
            markdown = FormatTypeHover(word, typeEntry);
        }
        else if (sozluk.Operators.TryGetValue(word, out var opEntry))
        {
            markdown = $"**{word}** → `{opEntry.CSharp}`";
        }
        else if (sozluk.Literals.TryGetValue(word, out var litEntry))
        {
            markdown = $"**{word}** → `{litEntry.CSharp}` (tür: `{litEntry.Type}`)";
        }

        if (markdown is null)
        {
            LanguageServerTrace.Warning($"Hover found no documentation for '{word}'.");
            return Task.FromResult<Hover?>(null);
        }

        LanguageServerTrace.Info($"Hover resolved from sozluk for '{word}'.");

        return Task.FromResult<Hover?>(new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(
                new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = markdown
                })
        });
    }

    private Hover? BuildSymbolHover(SymbolOccurrence occurrence, SymbolTable symbols)
    {
        var sb = new System.Text.StringBuilder();

        // For declarations and resolved references
        if (occurrence.ResolvedDeclarationId is not null)
        {
            var declaration = symbols.FindDeclarationById(occurrence.ResolvedDeclarationId.Value);
            if (declaration is not null)
            {
                // Show variable/parameter with inferred type
                if (declaration.Kind == GSymbolKind.Variable || declaration.Kind == GSymbolKind.Parameter)
                {
                    sb.Append($"**{occurrence.Name}**: ");
                    if (declaration.InferredType is not null)
                    {
                        sb.Append(declaration.InferredType);
                    }
                    else
                    {
                        sb.Append("T");
                    }
                    sb.Append("\n\n");
                    sb.Append($"_Declared at line {declaration.Location.Line}_");
                }
                // Show function with signature
                else if (declaration.Kind == GSymbolKind.Function)
                {
                    var sig = symbols.FindFunctionSignature(occurrence.Name);
                    if (sig is not null)
                    {
                        sb.Append($"**fonksiyon** `{occurrence.Name}`\n\n");
                        if (sig.Parameters.Any())
                        {
                            sb.Append("**Parametreler:**\n");
                            foreach (var param in sig.Parameters)
                            {
                                sb.Append($"- `{param.Name}`: {param.Type}\n");
                            }
                            sb.Append("\n");
                        }
                        if (sig.ReturnType is not null)
                        {
                            sb.Append($"**Geri Dönüş:** {sig.ReturnType}");
                        }
                    }
                }

                var contents = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = sb.ToString()
                };

                return new Hover
                {
                    Contents = new MarkedStringsOrMarkupContent(contents),
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    {
                        Start = new Position(occurrence.Location.Line - 1, occurrence.Location.Column),
                        End = new Position(
                            occurrence.Location.Line - 1,
                            occurrence.Location.Column + occurrence.Name.Length)
                    }
                };
            }
        }

        // Check if it's a built-in function
        if (_builtInSignatures.TryGetBuiltInSignature(occurrence.Name, out var builtInSig))
        {
            sb.Append($"**{occurrence.Name}**\n\n");
            sb.Append(builtInSig.Tooltip);

            var contents = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = sb.ToString()
            };

            return new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(contents),
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new Position(occurrence.Location.Line - 1, occurrence.Location.Column),
                    End = new Position(
                        occurrence.Location.Line - 1,
                        occurrence.Location.Column + occurrence.Name.Length)
                }
            };
        }

        return null;
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability,
        ClientCapabilities clientCapabilities) =>
        new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("gorsharp")
        };

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

        // Expand left and right to find word boundary (supporting Turkish chars)
        int start = wordIndex, end = wordIndex;

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

    private static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' ||
        // Turkish-specific characters
        c == 'ö' || c == 'ü' || c == 'ş' || c == 'ç' || c == 'ı' || c == 'ğ' ||
        c == 'Ö' || c == 'Ü' || c == 'Ş' || c == 'Ç' || c == 'İ' || c == 'Ğ';

    private static string FormatKeywordHover(string word, Core.Sozluk.KeywordEntry entry)
    {
        var parts = new List<string>
        {
            $"**{word}** → `{entry.CSharp}`",
            $"Kategori: `{entry.Category}`"
        };

        if (entry.Tooltip is not null)
        {
            if (!string.IsNullOrEmpty(entry.Tooltip.Description))
                parts.Add(entry.Tooltip.Description);
            if (entry.Tooltip.Example is not null)
            {
                parts.Add($"```gorsharp\n{entry.Tooltip.Example.Gor}\n```");
                parts.Add($"↓ C#\n```csharp\n{entry.Tooltip.Example.CSharp}\n```");
            }
        }

        return string.Join("\n\n", parts);
    }

    private static string FormatTypeHover(string word, Core.Sozluk.TypeEntry entry)
    {
        var parts = new List<string>
        {
            $"**{word}** → `{entry.CSharp}`"
        };

        if (entry.Tooltip is not null)
        {
            if (!string.IsNullOrEmpty(entry.Tooltip.Description))
                parts.Add(entry.Tooltip.Description);
            if (entry.Tooltip.Example is not null)
            {
                parts.Add($"```gorsharp\n{entry.Tooltip.Example.Gor}\n```");
                parts.Add($"↓ C#\n```csharp\n{entry.Tooltip.Example.CSharp}\n```");
            }
        }

        return string.Join("\n\n", parts);
    }
}
