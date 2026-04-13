using GorSharp.Core.Ast;

namespace GorSharp.Morphology.Normalization;

public interface IMorphologyNormalizationPass
{
    MorphologyNormalizationResult Normalize(ProgramNode ast, string sourceCode, string fileName);
}
