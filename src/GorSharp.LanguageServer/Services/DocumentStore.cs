namespace GorSharp.LanguageServer.Services;

/// <summary>
/// In-memory store for open .gör documents.
/// </summary>
public class DocumentStore
{
    private readonly Dictionary<string, DocumentState> _documents = new();

    public void Open(string uri, string text, int version)
    {
        _documents[uri] = new DocumentState(uri, text, version);
    }

    public void Update(string uri, string text, int version)
    {
        _documents[uri] = new DocumentState(uri, text, version);
    }

    public void Close(string uri)
    {
        _documents.Remove(uri);
    }

    public DocumentState? Get(string uri)
    {
        return _documents.TryGetValue(uri, out var doc) ? doc : null;
    }

    public IEnumerable<DocumentState> All => _documents.Values;
}

public record DocumentState(string Uri, string Text, int Version);
