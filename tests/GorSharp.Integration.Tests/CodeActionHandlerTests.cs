using GorSharp.LanguageServer.Handlers;
using GorSharp.LanguageServer.Services;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GorSharp.Tests.Integration;

public class CodeActionHandlerTests
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

        throw new FileNotFoundException("dictionaries/sozluk.json bulunamadi.");
    }

    private static CodeActionHandler CreateHandler(DocumentStore store)
    {
        var transpiler = new TranspilationService(new ParsingModeService());
        transpiler.LoadSozluk(ResolveSozlukPath());
        return new CodeActionHandler(store, transpiler, new SymbolAnalysisService());
    }

    private static async Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(CodeActionHandler handler, DocumentUri uri)
    {
        var result = await handler.Handle(new CodeActionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(new Position(0, 0), new Position(200, 0))
        }, CancellationToken.None);

        Assert.NotNull(result);

        return result!
            .Where(action => action.IsCodeAction)
            .Select(action => action.CodeAction)
            .Where(action => action is not null)
            .ToList()!;
    }

    [Fact]
    public async Task Handle_WhenGOR2007Present_ReturnsArgumentTypeQuickFix()
    {
        var source = "fonksiyon topla(a: sayı): sayı { döndür a; } topla(\"metin\") yazdır;";
        var store = new DocumentStore();
        var uri = (DocumentUri)"file:///code-action-gor2007.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = CreateHandler(store);
        var actions = await GetCodeActionsAsync(handler, uri);

        Assert.Contains(actions, action => action.Title.Contains("Argüman türünü parametre türü ile eşleştir", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_WhenGOR2001Present_ReturnsTurkishCreateVariableAction()
    {
        var source = "bilinmeyen yazdır;";
        var store = new DocumentStore();
        var uri = (DocumentUri)"file:///code-action-gor2001.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = CreateHandler(store);
        var actions = await GetCodeActionsAsync(handler, uri);

        Assert.Contains(actions, action => action.Title.Contains("Değişken oluştur", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_WhenGOR2002Present_ReturnsTurkishRenameAction()
    {
        var source = "x 1 olsun; x 2 olsun;";
        var store = new DocumentStore();
        var uri = (DocumentUri)"file:///code-action-gor2002.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = CreateHandler(store);
        var actions = await GetCodeActionsAsync(handler, uri);

        Assert.Contains(actions, action => action.Title.Contains("Yeniden adlandır", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_WhenGOR2003Present_ReturnsTurkishArityAction()
    {
        var source = "fonksiyon topla(a: sayı): sayı { döndür a; } topla() yazdır;";
        var store = new DocumentStore();
        var uri = (DocumentUri)"file:///code-action-gor2003.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = CreateHandler(store);
        var actions = await GetCodeActionsAsync(handler, uri);

        Assert.Contains(actions, action => action.Title.Contains("argüman ekle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_WhenGOR2008Present_ReturnsOperandCompatibilityQuickFix()
    {
        var source = "x doğru + 1 olsun;";
        var store = new DocumentStore();
        var uri = (DocumentUri)"file:///code-action-gor2008.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = CreateHandler(store);
        var actions = await GetCodeActionsAsync(handler, uri);

        Assert.Contains(actions, action => action.Title.Contains("İşlemdeki iki tarafın türünü uyumlu hale getir", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_WhenGOR2010Present_ReturnsUnaryRemovalFixWithEdit()
    {
        var source = "x - doğru olsun;";
        var store = new DocumentStore();
        var uri = (DocumentUri)"file:///code-action-gor2010.gor";
        store.Open(uri.ToString(), source, 1);

        var handler = CreateHandler(store);
        var actions = await GetCodeActionsAsync(handler, uri);

        var action = Assert.Single(actions, a => a.Title.Contains("Tekli '-' işlemini kaldır", StringComparison.Ordinal));
        Assert.NotNull(action.Edit);
        Assert.NotNull(action.Edit!.Changes);
        Assert.True(action.Edit.Changes!.TryGetValue(uri, out var edits));
        var edit = Assert.Single(edits!);
        Assert.Equal(string.Empty, edit.NewText);
    }
}
