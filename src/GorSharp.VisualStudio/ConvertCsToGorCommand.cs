using System;
using System.ComponentModel.Design;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GorSharp.VisualStudio;

internal sealed class ConvertCsToGorCommand
{
    private const int ImportCommandId = 0x0100;
    private const int RefreshGeneratedCommandId = 0x0101;
    private const int OpenGeneratedCommandId = 0x0102;
    private static readonly Guid CommandSet = new("b4a74652-5176-4843-94d7-4150185a3f48");

    private readonly AsyncPackage package;
    private readonly DTE2 dte;

    private ConvertCsToGorCommand(AsyncPackage package, DTE2 dte, OleMenuCommandService commandService)
    {
        this.package = package;
        this.dte = dte;

        AddCommand(
            commandService,
            ImportCommandId,
            (_, __) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                InvokeAsyncCommand(ExecuteImportAsync);
            },
            command => UpdateCommandVisibility(command, IsCSharpFile));

        AddCommand(
            commandService,
            RefreshGeneratedCommandId,
            (_, __) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                InvokeAsyncCommand(ExecuteRefreshGeneratedAsync);
            },
            command => UpdateCommandVisibility(command, IsGorSharpFile));

        AddCommand(
            commandService,
            OpenGeneratedCommandId,
            (_, __) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                InvokeAsyncCommand(ExecuteOpenGeneratedAsync);
            },
            command => UpdateCommandVisibility(command, IsGorSharpFile));
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;
        if (commandService is null || dte is null)
        {
            GorSharpVisualStudioLogger.Warning("Convert command initialization skipped because required Visual Studio services are unavailable.");
            return;
        }

        _ = new ConvertCsToGorCommand(package, dte, commandService);
        GorSharpVisualStudioLogger.Important("Convert command initialized.");
    }

    private static void AddCommand(
        OleMenuCommandService commandService,
        int commandId,
        EventHandler invokeHandler,
        Action<OleMenuCommand> beforeQueryStatus)
    {
        var menuCommandId = new CommandID(CommandSet, commandId);
        var menuItem = new OleMenuCommand(invokeHandler, menuCommandId);
        menuItem.BeforeQueryStatus += (_, __) => beforeQueryStatus(menuItem);
        commandService.AddCommand(menuItem);
    }

    private void InvokeAsyncCommand(Func<Task> action)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        package.JoinableTaskFactory.Run(action);
    }

    private void UpdateCommandVisibility(OleMenuCommand menuCommand, Func<string?, bool> predicate)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var selectedPath = GetSelectedOrActiveFilePath();
        menuCommand.Visible = predicate(selectedPath);
        menuCommand.Enabled = menuCommand.Visible;
    }

    private async Task ExecuteImportAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var inputPath = GetSelectedOrActiveFilePath();
        GorSharpVisualStudioLogger.Verbose($"Convert command invoked. Selected path: {inputPath ?? "<null>"}");
        if (!IsCSharpFile(inputPath))
        {
            GorSharpVisualStudioLogger.Warning("Convert command canceled because selected item is not a .cs file.");
            VsShellUtilities.ShowMessageBox(
                package,
                "Lutfen bir .cs dosyasi secin.",
                "GorSharp",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        var outputPath = Path.ChangeExtension(inputPath, ".gör");
        var result = await ConvertWithCliAsync(inputPath!, outputPath, includeExplain: true);
        if (!result.Success)
        {
            GorSharpVisualStudioLogger.Error($"C# to Gor# conversion failed for {inputPath}. Details: {result.ErrorMessage}");
            VsShellUtilities.ShowMessageBox(
                package,
                result.ErrorMessage,
                "GorSharp Donusum Hatasi",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        GorSharpVisualStudioLogger.Important($"C# to Gor# conversion completed. Output={outputPath}");
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            GorSharpVisualStudioLogger.Info("C# -> Gor# aciklama ozeti:");
            GorSharpVisualStudioLogger.Info(result.StandardOutput);
        }
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            GorSharpVisualStudioLogger.Warning(result.StandardError);
        }

        VsShellUtilities.ShowMessageBox(
            package,
            string.Format(CultureInfo.CurrentCulture, "Gor# dosyasi olusturuldu: {0}", outputPath),
            "GorSharp",
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

        await OpenFileInEditorAsync(outputPath);
    }

    private async Task ExecuteRefreshGeneratedAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var inputPath = GetSelectedOrActiveFilePath();
        if (!IsGorSharpFile(inputPath))
        {
            VsShellUtilities.ShowMessageBox(
                package,
                "Lutfen bir .gör dosyasi secin.",
                "GorSharp",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        var outputPath = GetGeneratedOutputPath(inputPath!);
        var result = await TranspileWithCliAsync(inputPath!, outputPath);
        if (!result.Success)
        {
            GorSharpVisualStudioLogger.Error($"Gor# transpile failed for {inputPath}. Details: {result.ErrorMessage}");
            VsShellUtilities.ShowMessageBox(
                package,
                result.ErrorMessage,
                "GorSharp Ayna Yenileme Hatasi",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        GorSharpVisualStudioLogger.Important($"Generated C# refreshed. Input={inputPath}, Output={outputPath}");

        VsShellUtilities.ShowMessageBox(
            package,
            string.Format(CultureInfo.CurrentCulture, "Uretilen C# guncellendi: {0}", outputPath),
            "GorSharp",
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

        await OpenFileInEditorAsync(outputPath);
    }

    private async Task ExecuteOpenGeneratedAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var inputPath = GetSelectedOrActiveFilePath();
        if (!IsGorSharpFile(inputPath))
        {
            VsShellUtilities.ShowMessageBox(
                package,
                "Lutfen bir .gör dosyasi secin.",
                "GorSharp",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        var outputPath = GetGeneratedOutputPath(inputPath!);
        if (!File.Exists(outputPath))
        {
            var result = await TranspileWithCliAsync(inputPath!, outputPath);
            if (!result.Success)
            {
                VsShellUtilities.ShowMessageBox(
                    package,
                    result.ErrorMessage,
                    "GorSharp Ayna Acma Hatasi",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }
        }

        await OpenFileInEditorAsync(outputPath);
    }

    private async Task OpenFileInEditorAsync(string outputPath)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var item = dte.Solution.FindProjectItem(outputPath);
        if (item is not null)
        {
            item.Open().Activate();
            return;
        }

        VsShellUtilities.OpenDocument(package, outputPath);
    }

    private string? GetSelectedOrActiveFilePath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var selectedItems = dte.ToolWindows.SolutionExplorer.SelectedItems as Array;
        if (selectedItems is not null && selectedItems.Length == 1)
        {
            var selectedItem = selectedItems.GetValue(0) as UIHierarchyItem;
            var projectItem = selectedItem?.Object as ProjectItem;
            var fileCount = projectItem?.FileCount ?? 0;
            if (fileCount > 0)
            {
                return projectItem?.FileNames[1];
            }
        }

        return dte.ActiveDocument?.FullName;
    }

    private static bool IsCSharpFile(string? filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath) &&
               string.Equals(Path.GetExtension(filePath), ".cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGorSharpFile(string? filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath) &&
               string.Equals(Path.GetExtension(filePath), ".gör", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetGeneratedOutputPath(string gorPath)
    {
        return Path.Combine(
            Path.GetDirectoryName(gorPath)!,
            $"{Path.GetFileNameWithoutExtension(gorPath)}.uretilenkod.cs");
    }

    private static async Task<(bool Success, string ErrorMessage, string StandardOutput, string StandardError)> ConvertWithCliAsync(string inputPath, string outputPath, bool includeExplain)
    {
        var explainFlag = includeExplain ? " --explain" : string.Empty;
        var bundledCli = GetBundledCliPath();
        if (!string.IsNullOrWhiteSpace(bundledCli))
        {
            var bundled = await RunProcessAsync(bundledCli!, $"fromcs {Quote(inputPath)} -o {Quote(outputPath)}{explainFlag}");
            if (bundled.Success)
            {
                return (true, string.Empty, bundled.StandardOutput, bundled.StandardError);
            }

            return (false,
                "VSIX icindeki donusum araci calistirilamadi.\n\n" +
                "Lutfen uzantiyi yeniden yukleyin veya VSIX paketini yeniden olusturun.\n\n" +
                bundled.ErrorMessage,
                bundled.StandardOutput,
                bundled.StandardError);
        }

        var inArg = Quote(inputPath);
        var outArg = Quote(outputPath);

        var direct = await RunProcessAsync("gorsharp", $"fromcs {inArg} -o {outArg}{explainFlag}");
        if (direct.Success)
        {
            return (true, string.Empty, direct.StandardOutput, direct.StandardError);
        }

        var localCliProject = FindCliProjectNear(inputPath);
        if (!string.IsNullOrWhiteSpace(localCliProject))
        {
            var fallback = await RunProcessAsync("dotnet", $"run --project {Quote(localCliProject!)} -- fromcs {inArg} -o {outArg}{explainFlag}");
            if (fallback.Success)
            {
                return (true, string.Empty, fallback.StandardOutput, fallback.StandardError);
            }

            return (false, fallback.ErrorMessage, fallback.StandardOutput, fallback.StandardError);
        }

        return (false,
            "Donusum araci bulunamadi.\n\n" +
            "Cozum:\n" +
            "1) VSIX paketini yeniden olusturun (tools klasoru eksik olabilir), veya\n" +
            "2) gelistirme ortaminda gorsharp aracini PATH'e ekleyin.\n\n" +
            direct.ErrorMessage,
            direct.StandardOutput,
            direct.StandardError);
    }

    private static async Task<(bool Success, string ErrorMessage, string StandardOutput, string StandardError)> TranspileWithCliAsync(string inputPath, string outputPath)
    {
        var bundledCli = GetBundledCliPath();
        var inArg = Quote(inputPath);
        var outArg = Quote(outputPath);
        const string modeArg = "--mode natural";

        if (!string.IsNullOrWhiteSpace(bundledCli))
        {
            var bundled = await RunProcessAsync(bundledCli!, $"transpile {inArg} -o {outArg} {modeArg}");
            if (bundled.Success)
            {
                return (true, string.Empty, bundled.StandardOutput, bundled.StandardError);
            }

            return (false,
                "VSIX icindeki transpile araci calistirilamadi.\n\n" +
                "Lutfen uzantiyi yeniden yukleyin veya VSIX paketini yeniden olusturun.\n\n" +
                bundled.ErrorMessage,
                bundled.StandardOutput,
                bundled.StandardError);
        }

        var direct = await RunProcessAsync("gorsharp", $"transpile {inArg} -o {outArg} {modeArg}");
        if (direct.Success)
        {
            return (true, string.Empty, direct.StandardOutput, direct.StandardError);
        }

        var localCliProject = FindCliProjectNear(inputPath);
        if (!string.IsNullOrWhiteSpace(localCliProject))
        {
            var fallback = await RunProcessAsync("dotnet", $"run --project {Quote(localCliProject!)} -- transpile {inArg} -o {outArg} {modeArg}");
            if (fallback.Success)
            {
                return (true, string.Empty, fallback.StandardOutput, fallback.StandardError);
            }

            return (false, fallback.ErrorMessage, fallback.StandardOutput, fallback.StandardError);
        }

        return (false,
            "Transpile araci bulunamadi.\n\n" +
            "Cozum:\n" +
            "1) VSIX paketini yeniden olusturun (tools klasoru eksik olabilir), veya\n" +
            "2) gelistirme ortaminda gorsharp aracini PATH'e ekleyin.\n\n" +
            direct.ErrorMessage,
            direct.StandardOutput,
            direct.StandardError);
    }

    private static string? GetBundledCliPath()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(ConvertCsToGorCommand).Assembly.Location);
        if (assemblyDir is null)
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(assemblyDir, "tools", "gorsharp.exe"),
            Path.Combine(assemblyDir, "tools", "GorSharp.CLI.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindCliProjectNear(string inputPath)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(inputPath)!);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "GorSharp.CLI", "GorSharp.CLI.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static async Task<(bool Success, string ErrorMessage, string StandardOutput, string StandardError)> RunProcessAsync(string fileName, string arguments)
    {
        try
        {
            GorSharpVisualStudioLogger.Verbose($"Starting process: {fileName} {arguments}");
            using var process = new DiagnosticsProcess
            {
                StartInfo = new DiagnosticsProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                GorSharpVisualStudioLogger.Error($"Process could not be started: {fileName}");
                return (false, $"Komut baslatilamadi: {fileName}", string.Empty, string.Empty);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await Task.Run(() => process.WaitForExit());

            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();
            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(error) ? output : error;
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = $"Komut hata kodu ile bitti: {process.ExitCode}";
                }

                GorSharpVisualStudioLogger.Warning($"Process exited with code {process.ExitCode}: {fileName}");
                GorSharpVisualStudioLogger.Verbose($"Process stderr/stdout: {message}");

                return (false, message, output, error);
            }

            GorSharpVisualStudioLogger.Verbose($"Process completed successfully: {fileName}");

            return (true, string.Empty, output, error);
        }
        catch (Exception ex)
        {
            GorSharpVisualStudioLogger.Error($"Process execution failed: {fileName}", ex);
            return (false, ex.Message, string.Empty, string.Empty);
        }
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
