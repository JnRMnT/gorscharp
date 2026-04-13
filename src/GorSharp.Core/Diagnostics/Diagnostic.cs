namespace GorSharp.Core.Diagnostics;

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// A diagnostic message (error/warning) from any stage of the pipeline.
/// All messages are in Turkish.
/// </summary>
public record Diagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string File,
    int Line,
    int Column)
{
    public override string ToString() =>
        $"{File}({Line},{Column}): {Severity} {Code}: {Message}";
}
