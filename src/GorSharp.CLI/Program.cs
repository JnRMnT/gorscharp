using System.Diagnostics;
using System.Reflection;
using GorSharp.Core.Ast;
using GorSharp.Core.Sozluk;
using GorSharp.Morphology;
using GorSharp.Morphology.Normalization;
using GorSharp.Parser;
using GorSharp.Transpiler;

namespace GorSharp.CLI;

public class Program
{
    private static SozlukData? _sozluk;

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        LoadSozluk();

        return args[0] switch
        {
            "transpile" => Transpile(args[1..]),
            "run" => RunCommand(args[1..]),
            "diff" => Diff(args[1..]),
            "fromcs" => FromCs(args[1..]),
            _ => UnknownCommand(args[0])
        };
    }

    private static void LoadSozluk()
    {
        // Look for sozluk.json relative to the executable, then workspace root
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "dictionaries", "sozluk.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "dictionaries", "sozluk.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dictionaries", "sozluk.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "dictionaries", "sozluk.json"),
        };

        foreach (var path in candidates)
        {
            var resolved = Path.GetFullPath(path);
            if (File.Exists(resolved))
            {
                var service = new SozlukService();
                service.Load(resolved);
                _sozluk = service.Data;
                return;
            }
        }
    }

    private static int Transpile(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Hata: Dosya yolu belirtilmedi.");
            Console.Error.WriteLine("Kullanim: gorsharp transpile <dosya.gör> [--mode strict|natural]");
            return 1;
        }

        if (!TryParseCommonArgs(args, out var filePath, out var options, out var parseError))
        {
            Console.Error.WriteLine($"Hata: {parseError}");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Hata: Dosya bulunamadi: {filePath}");
            return 1;
        }

        var source = File.ReadAllText(filePath);
        var parser = new GorSharpParserService(options: options);
        var (ast, diagnostics) = parser.Parse(source, filePath);
        var normalization = NormalizeMorphology(ast, source, filePath);
        ast = normalization.Ast;
        diagnostics = diagnostics.Concat(normalization.Diagnostics).ToList();

        foreach (var diag in diagnostics)
        {
            Console.Error.WriteLine(diag);
        }

        if (diagnostics.Any(d => d.Severity == Core.Diagnostics.DiagnosticSeverity.Error))
        {
            return 1;
        }

        var emitter = new CSharpEmitter(_sozluk);
        var csharp = emitter.Emit(ast);

        if (args.Length >= 3 && args[1] == "-o")
        {
            File.WriteAllText(args[2], csharp);
            Console.WriteLine($"�ikti yazildi: {args[2]}");
        }
        else
        {
            Console.Write(csharp);
        }

        return 0;
    }

    private static int RunCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Hata: Dosya yolu belirtilmedi.");
            Console.Error.WriteLine("Kullanim: gorsharp run <dosya.gör> [--mode strict|natural]");
            return 1;
        }

        if (!TryParseCommonArgs(args, out var filePath, out var options, out var parseError))
        {
            Console.Error.WriteLine($"Hata: {parseError}");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Hata: Dosya bulunamadi: {filePath}");
            return 1;
        }

        var source = File.ReadAllText(filePath);
        var parser = new GorSharpParserService(options: options);
        var (ast, diagnostics) = parser.Parse(source, filePath);
        var normalization = NormalizeMorphology(ast, source, filePath);
        ast = normalization.Ast;
        diagnostics = diagnostics.Concat(normalization.Diagnostics).ToList();

        foreach (var diag in diagnostics)
            Console.Error.WriteLine(diag);

        if (diagnostics.Any(d => d.Severity == Core.Diagnostics.DiagnosticSeverity.Error))
            return 1;

        var emitter = new CSharpEmitter(_sozluk);
        var csharp = emitter.Emit(ast);

        // Write to temp .cs file, compile & run via dotnet-script or csc
        var tempDir = Path.Combine(Path.GetTempPath(), "gorsharp-run");
        Directory.CreateDirectory(tempDir);
        var csFile = Path.Combine(tempDir, "Program.cs");
        var csprojFile = Path.Combine(tempDir, "GorSharpRun.csproj");

        // Strip source comments for cleaner execution
        var cleanCs = string.Join('\n', csharp.Split('\n')
            .Select(line =>
            {
                var idx = line.IndexOf("/* g�r:");
                return idx >= 0 ? line[..idx].TrimEnd() : line;
            }));

        File.WriteAllText(csFile, cleanCs);
        File.WriteAllText(csprojFile, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);

        var psi = new ProcessStartInfo("dotnet", $"run --project \"{tempDir}\"")
        {
            UseShellExecute = false,
            WorkingDirectory = tempDir
        };

        try
        {
            var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode ?? 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Hata: �alistirma basarisiz: {ex.Message}");
            return 1;
        }
    }

    private static int Diff(string[] args)
    {
        Console.Error.WriteLine("'diff' komutu hen�z uygulanmadi.");
        return 1;
    }

    private static int FromCs(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Hata: C# dosya yolu belirtilmedi.");
            Console.Error.WriteLine("Kullanim: gorsharp fromcs <dosya.cs> [-o cikti.g�r]");
            return 1;
        }

        var inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Hata: Dosya bulunamadi: {inputPath}");
            return 1;
        }

        var outputPath = args.Length >= 3 && args[1] == "-o"
            ? args[2]
            : Path.ChangeExtension(inputPath, ".g�r");

        var source = File.ReadAllText(inputPath);
        var converted = CSharpToGorConverter.Convert(source);
        File.WriteAllText(outputPath, converted);

        Console.WriteLine($"�ikti yazildi: {outputPath}");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Bilinmeyen komut: {command}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("G�r# � T�rk�e C# Aktarici");
        Console.WriteLine();
        Console.WriteLine("Kullanim: gorsharp <komut> [se�enekler]");
        Console.WriteLine();
        Console.WriteLine("Komutlar:");
        Console.WriteLine("  transpile <dosya.gör> [--mode strict|natural]   Gör# dosyasini C#'a çevir");
        Console.WriteLine("  run <dosya.gör> [--mode strict|natural]         Çevir ve çalıştır");
        Console.WriteLine("  diff <dosya.g�r>              G�r# ve C# yanyana g�ster");
        Console.WriteLine("  fromcs <dosya.cs>             Basit C# kodunu G�r# s�zdizimine d�n�st�r");
    }

    private static MorphologyNormalizationResult NormalizeMorphology(ProgramNode ast, string source, string filePath)
    {
        var resolver = new SuffixResolver(_sozluk ?? new SozlukData());
        var pass = new ZemberekMorphologyNormalizationPass(
            resolver,
            new MorphologyNormalizationOptions
            {
                Enabled = true,
                EmitCandidateDiagnostics = true
            });

        return pass.Normalize(ast, source, filePath);
    }

    private static bool TryParseCommonArgs(
        string[] args,
        out string filePath,
        out GorSharpParsingOptions options,
        out string? error)
    {
        filePath = string.Empty;
        options = new GorSharpParsingOptions();
        error = null;

        var modeIndex = Array.FindIndex(args, a => string.Equals(a, "--mode", StringComparison.OrdinalIgnoreCase));
        if (modeIndex >= 0)
        {
            if (modeIndex + 1 >= args.Length)
            {
                error = "--mode için strict veya natural değeri verilmelidir.";
                return false;
            }

            var mode = args[modeIndex + 1];
            var normalizedMode = mode.ToLowerInvariant();
            if (normalizedMode is not ("natural" or "strict"))
            {
                error = $"Geçersiz mode: {mode}. strict veya natural kullanın.";
                return false;
            }

            options = normalizedMode switch
            {
                "natural" => new GorSharpParsingOptions { NaturalMode = true },
                "strict" => new GorSharpParsingOptions { NaturalMode = false },
                _ => new GorSharpParsingOptions()
            };
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (i == modeIndex)
                continue;
            if (modeIndex >= 0 && i == modeIndex + 1)
                continue;

            if (!args[i].StartsWith("-", StringComparison.Ordinal))
            {
                filePath = args[i];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            error = "Dosya yolu belirtilmedi.";
            return false;
        }

        return true;
    }
}

