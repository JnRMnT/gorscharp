namespace GorSharp.Parser;

/// <summary>
/// Controls how permissive the parser is for natural-language particles.
/// </summary>
public sealed class GorSharpParsingOptions
{
    /// <summary>
    /// Enables natural-language particles like "değişkeni", "olarak", "şayet".
    /// </summary>
    public bool NaturalMode { get; init; } = true;
}
