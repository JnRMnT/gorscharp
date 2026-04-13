using GorSharp.Core.Ast;
using GorSharp.Core.Diagnostics;

namespace GorSharp.Morphology.Normalization;

public sealed record MorphologyNormalizationResult(
    ProgramNode Ast,
    IReadOnlyList<Diagnostic> Diagnostics);
