using System;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace GorSharp.VisualStudio;

[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("gorsharp")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class GorSharpLanguageClientDiscoveryProbe : IWpfTextViewCreationListener
{
    public void TextViewCreated(IWpfTextView textView)
    {
        try
        {
            var componentModel = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            if (componentModel is null)
            {
                GorSharpVisualStudioLogger.Verbose("MEF probe: IComponentModel is null.");
                return;
            }

            var languageClients = componentModel.GetExtensions<ILanguageClient>().ToList();
            GorSharpVisualStudioLogger.Verbose($"MEF probe: ILanguageClient exports discovered = {languageClients.Count}");
            foreach (var client in languageClients)
            {
                GorSharpVisualStudioLogger.Verbose($"MEF ILanguageClient instance: {client.GetType().FullName}");
            }
        }
        catch (Exception ex)
        {
            GorSharpVisualStudioLogger.Error("MEF probe failed while reading ILanguageClient exports.", ex);
        }
    }
}
