namespace GorSharp.Core.Diagnostics;

/// <summary>
/// Shared diagnostic code definitions.
/// </summary>
public static class DiagnosticCodes
{
    public const string SyntaxError = "GOR0001";
    public const string StrictModeNaturalParticle = "GOR1001";

    public const string UndefinedSymbol = "GOR2001";
    public const string DuplicateDeclaration = "GOR2002";
    public const string FunctionArityMismatch = "GOR2003";
    public const string AssignmentMismatch = "GOR2004";
    public const string NonBooleanCondition = "GOR2005";
    public const string MissingReturnValue = "GOR2006";
    public const string FunctionArgumentTypeMismatch = "GOR2007";
    public const string BinaryOperandTypeMismatch = "GOR2008";
    public const string MissingReturnInTypedFunction = "GOR2009";
    public const string UnaryOperandTypeMismatch = "GOR2010";
    public const string VariableShadowing = "GOR2011";
    public const string BreakContinueOutsideLoop = "GOR2012";
    public const string UnusedVariable = "GOR2013";
    public const string UnreachableCode = "GOR2014";
    public const string ConditionlessForLoop = "GOR2015";
    public const string UnusedFunction = "GOR2016";
    public const string UnusedParameter = "GOR2017";
    public const string AssignmentToUndefinedVariable = "GOR2018";
    public const string NoOpAssignment = "GOR2019";
    public const string ConstantCondition = "GOR2020";
    public const string RedundantBranch = "GOR2021";
    public const string LoopProgressMismatch = "GOR2022";

    public const string GeneratedCSharpCompilationError = "GOR2100";
    public const string GeneratedCSharpCompilationWarning = "GOR2101";

    public const string MorphologyCandidateDetected = "GOR3001";
    public const string MorphologyAmbiguous = "GOR3002";
    public const string MorphologyMappingMissing = "GOR3003";
    public const string MorphologyInconclusive = "GOR3004";
    public const string MorphologyRuntimeUnavailable = "GOR3005";
}
