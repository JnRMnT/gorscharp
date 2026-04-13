using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;

namespace GorSharp.VisualStudio;

/// <summary>
/// Registers the .gör content type and file extension for Visual Studio.
/// </summary>
public static class GorSharpContentType
{
    [Export]
    [Name("gorsharp")]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition? GorSharpContentTypeDefinition;

    [Export]
    [FileExtension(".gör")]
    [ContentType("gorsharp")]
    internal static FileExtensionToContentTypeDefinition? GorSharpFileExtensionDefinition;

    [Export]
    [FileExtension(".gor")]
    [ContentType("gorsharp")]
    internal static FileExtensionToContentTypeDefinition? GorSharpAsciiFileExtensionDefinition;
}
