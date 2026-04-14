using GorSharp.LanguageServer.Handlers;
using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.Tests.Integration;

public class SuffixHoverCompletionTests
{
    private static string ResolveSozlukPath()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var path = Path.Combine(dir, "dictionaries", "sozluk.json");
            if (File.Exists(path))
                return path;

            dir = Path.GetDirectoryName(dir)!;
        }

        throw new FileNotFoundException("dictionaries/sozluk.json bulunamadı.");
    }

    private static (DocumentStore Store, TranspilationService Transpiler, SuffixExplanationService Explanations) CreateServices()
    {
        var store = new DocumentStore();
        var transpiler = new TranspilationService(new ParsingModeService());
        transpiler.LoadSozluk(ResolveSozlukPath());
        var explanations = new SuffixExplanationService(transpiler);
        return (store, transpiler, explanations);
    }

    [Fact]
    public async Task HoverHandler_ReturnsHover_ForSuffixedToken()
    {
        var source = "liste'ye 10 ekle;";
        var (store, transpiler, explanations) = CreateServices();
        var uri = (DocumentUri)"file:///hover-test.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = new HoverHandler(
            store,
            transpiler,
            new BuiltInSignaturesService(),
            new SymbolAnalysisService(),
            explanations);

        var hover = await handler.Handle(new HoverParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position(0, 3)
        }, CancellationToken.None);

        Assert.NotNull(hover);
    }

    [Fact]
    public async Task CompletionHandler_SuggestsCaseAwareVerbs_AfterSuffixedToken()
    {
        var source = "liste'ye ";
        var (store, transpiler, explanations) = CreateServices();
        var uri = (DocumentUri)"file:///completion-verb-test.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = new CompletionHandler(
            store,
            transpiler,
            new SymbolAnalysisService(),
            explanations);

        var completion = await handler.Handle(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position(0, source.Length)
        }, CancellationToken.None);

        var labels = completion.Items.Select(item => item.Label).ToList();
        Assert.Contains("ekle", labels);
        Assert.Contains("ata", labels);
        Assert.Contains("gönder", labels);
    }

    [Fact]
    public async Task CompletionHandler_SuggestsCaseAwareProperties_AfterGenitiveToken()
    {
        var source = "liste'nin ";
        var (store, transpiler, explanations) = CreateServices();
        var uri = (DocumentUri)"file:///completion-property-test.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = new CompletionHandler(
            store,
            transpiler,
            new SymbolAnalysisService(),
            explanations);

        var completion = await handler.Handle(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position(0, source.Length)
        }, CancellationToken.None);

        var labels = completion.Items.Select(item => item.Label).ToList();
        Assert.Contains("uzunluğu", labels);
        Assert.Contains("boyutu", labels);
        Assert.Contains("türü", labels);
    }
}