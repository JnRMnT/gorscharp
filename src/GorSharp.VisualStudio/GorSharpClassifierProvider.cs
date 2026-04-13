using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace GorSharp.VisualStudio;

[Export(typeof(IClassifierProvider))]
[ContentType("gorsharp")]
[ContentType("code")]
internal sealed class GorSharpClassifierProvider : IClassifierProvider
{
    [Import]
    internal IClassificationTypeRegistryService ClassificationRegistry = null!;

    public IClassifier GetClassifier(Microsoft.VisualStudio.Text.ITextBuffer textBuffer)
    {
        GorSharpVisualStudioLogger.Verbose("Classifier provider invoked for gorsharp buffer.");
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new GorSharpClassifier(ClassificationRegistry));
    }
}
