using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using GDiagnostic = GorSharp.Core.Diagnostics.Diagnostic;
using GDiagnosticSeverity = GorSharp.Core.Diagnostics.DiagnosticSeverity;
using GDiagnosticCodes = GorSharp.Core.Diagnostics.DiagnosticCodes;

namespace GorSharp.LanguageServer.Services;

public sealed class GeneratedCSharpCompilationService
{
    private readonly ImmutableArray<MetadataReference> _references;

    public GeneratedCSharpCompilationService()
    {
        _references = LoadMetadataReferences();
    }

    public IReadOnlyList<GDiagnostic> Compile(string csharpCode, string fileName, GeneratedCSharpSourceMap sourceMap)
    {
        // Ensure System.Console resolves regardless of host compiler defaults.
        // We inject a deterministic preamble only for compilation and compensate
        // when mapping diagnostics back to Gör# lines.
        const string preamble = "using System;\n";
        var compilableCode = csharpCode.StartsWith("using System;", StringComparison.Ordinal)
            ? csharpCode
            : preamble + csharpCode;

        var preambleLineCount = compilableCode == csharpCode ? 0 : 1;

        var syntaxTree = CSharpSyntaxTree.ParseText(
            compilableCode,
            new CSharpParseOptions(LanguageVersion.Latest),
            path: $"{Path.GetFileNameWithoutExtension(fileName)}.generated.cs");

        var compilation = CSharpCompilation.Create(
            assemblyName: "GorSharp.Generated",
            syntaxTrees: [syntaxTree],
            references: _references,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                nullableContextOptions: NullableContextOptions.Enable));

        return compilation
            .GetDiagnostics()
            .Where(d => d.Location.IsInSource)
            .Where(d => d.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .Select(d => MapDiagnostic(d, fileName, sourceMap, preambleLineCount))
            .ToList();
    }

    private static GDiagnostic MapDiagnostic(
        Microsoft.CodeAnalysis.Diagnostic diagnostic,
        string fileName,
        GeneratedCSharpSourceMap sourceMap,
        int preambleLineCount)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        var generatedLine = lineSpan.StartLinePosition.Line + 1;
        var mapLine = generatedLine - preambleLineCount;

        var gorLine = 1;
        var mapped = mapLine >= 1 && sourceMap.TryMap(mapLine, out gorLine);
        var snippet = mapLine >= 1 ? sourceMap.GetGeneratedLineSnippet(mapLine) : null;

        var severity = diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
            ? GDiagnosticSeverity.Error
            : GDiagnosticSeverity.Warning;

        var code = severity == GDiagnosticSeverity.Error
            ? GDiagnosticCodes.GeneratedCSharpCompilationError
            : GDiagnosticCodes.GeneratedCSharpCompilationWarning;

        var displayLine = mapLine >= 1 ? mapLine : generatedLine;
        var scopeText = mapped
            ? $"Gör satırı {gorLine} ile ilişkili üretilen C# kodunda"
            : $"Üretilen C# kodunun {displayLine}. satırında";

        var message = snippet is null
            ? $"{scopeText} derleme {(severity == GDiagnosticSeverity.Error ? "hatası" : "uyarısı")} oluştu ({diagnostic.Id})."
            : $"{scopeText} derleme {(severity == GDiagnosticSeverity.Error ? "hatası" : "uyarısı")} oluştu ({diagnostic.Id}). İlgili C# satırı: {snippet}";

        return new GDiagnostic(
            severity,
            code,
            message,
            fileName,
            mapped ? gorLine : 1,
            0);
    }

    private static ImmutableArray<MetadataReference> LoadMetadataReferences()
    {
        // Never mix runtime assemblies and reference-pack assemblies in one
        // compilation. Roslyn expects a coherent reference set.
        //
        // Strategy:
        // 1) Try TRUSTED_PLATFORM_ASSEMBLIES when it looks complete.
        // 2) Otherwise use SDK reference-pack assemblies.
        var tpaPaths = LoadTrustedPlatformAssemblyPaths();
        if (LooksLikeCompleteRuntimeSet(tpaPaths))
            return CreateReferences(tpaPaths);

        var fallbackPaths = LoadFallbackReferencePaths().ToArray();
        if (fallbackPaths.Length > 0)
            return CreateReferences(fallbackPaths);

        return CreateReferences(tpaPaths);
    }

    private static string[] LoadTrustedPlatformAssemblyPaths()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
            return [];

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToArray();
    }

    private static bool LooksLikeCompleteRuntimeSet(IEnumerable<string> paths)
    {
        var fileNames = new HashSet<string>(
            paths
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!),
            StringComparer.OrdinalIgnoreCase);

        // Minimum assemblies needed by our generated code + compiler binding.
        return fileNames.Contains("System.Runtime.dll")
            && fileNames.Contains("System.Console.dll")
            && fileNames.Contains("netstandard.dll");
    }

    private static ImmutableArray<MetadataReference> CreateReferences(IEnumerable<string> paths)
    {
        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                try { return (MetadataReference?)MetadataReference.CreateFromFile(path); }
                catch { return null; }
            })
            .OfType<MetadataReference>()
            .ToImmutableArray();
    }

    // Fallback TFM candidates to try, newest first. The reference assemblies are
    // ABI-stable for this usage, so net10 source compiles correctly with net9 refs.
    private static readonly string[] FallbackTfms = ["net10.0", "net9.0", "net8.0"];

    private static IEnumerable<string> LoadFallbackReferencePaths()
    {
        // For self-contained single-file apps on .NET 6+, managed runtime assemblies
        // are loaded from the bundle and may not have file-system locations.
        // Reference assembly packs provide a reliable on-disk set for Roslyn.
        var dotnetRoot = FindDotnetRoot();
        if (dotnetRoot is null)
            return Enumerable.Empty<string>();

        var packsDir = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
        if (!Directory.Exists(packsDir))
            return Enumerable.Empty<string>();

        foreach (var tfm in FallbackTfms)
        {
            var refAssemblyDir = Directory
                .EnumerateDirectories(packsDir)
                .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => Path.Combine(d, "ref", tfm))
                .FirstOrDefault(Directory.Exists);

            if (refAssemblyDir is not null)
                return Directory.EnumerateFiles(refAssemblyDir, "*.dll");
        }

        return Enumerable.Empty<string>();
    }

    private static string? FindDotnetRoot()
    {
        // 1. Explicit env var (set by dotnet itself when it launches child processes)
        var fromEnv = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(fromEnv) && Directory.Exists(fromEnv))
            return fromEnv;

        // 2. Standard Windows default install location
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var defaultPath = Path.Combine(programFiles, "dotnet");
        if (Directory.Exists(defaultPath))
            return defaultPath;

        // 3. x86 fallback (rare but possible)
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var x86Path = Path.Combine(programFilesX86, "dotnet");
        if (Directory.Exists(x86Path))
            return x86Path;

        return null;
    }
}