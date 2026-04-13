namespace GorSharp.Morphology.Normalization;

public sealed class MorphologyNormalizationOptions
{
    public bool Enabled { get; init; } = true;

    // During skeleton phase, emit candidate diagnostics to validate pipeline wiring.
    public bool EmitCandidateDiagnostics { get; init; } = true;
}
