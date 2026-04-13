using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace GorSharp.VisualStudio;

[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("gorsharp")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class GorSharpEditorProbe : IWpfTextViewCreationListener
{
    public void TextViewCreated(IWpfTextView textView)
    {
        var contentType = textView.TextDataModel.DocumentBuffer.ContentType.TypeName;
        var filePath = TryGetFilePath(textView.TextBuffer) ?? "<unknown>";
        var assemblyPath = typeof(GorSharpEditorProbe).Assembly.Location;
        GorSharpVisualStudioLogger.Verbose($"Editor probe attached. ContentType={contentType}, File={filePath}, Assembly={assemblyPath}");
    }

    private static string? TryGetFilePath(ITextBuffer textBuffer)
    {
        return textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)
            ? document.FilePath
            : null;
    }
}
