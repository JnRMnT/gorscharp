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
    private const int CommandId = 0x0100;
    private static readonly Guid CommandSet = new("b4a74652-5176-4843-94d7-4150185a3f48");

    private readonly AsyncPackage package;
    private readonly DTE2 dte;

    private ConvertCsToGorCommand(AsyncPackage package, DTE2 dte, OleMenuCommandService commandService)
    {
        this.package = package;
        this.dte = dte;

        var menuCommandId = new CommandID(CommandSet, CommandId);
        var menuItem = new OleMenuCommand((_, __) => _ = ExecuteAsync(), menuCommandId);
        menuItem.BeforeQueryStatus += (_, __) => UpdateCommandVisibility(menuItem);
        commandService.AddCommand(menuItem);
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

    private void UpdateCommandVisibility(OleMenuCommand menuCommand)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var selectedPath = GetSelectedFilePath();
        menuCommand.Visible = IsCSharpFile(selectedPath);
        menuCommand.Enabled = menuCommand.Visible;
    }

    private async Task ExecuteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        var inputPath = GetSelectedFilePath();
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
        var result = await ConvertWithCliAsync(inputPath!, outputPath);
        if (!result.Success)
        {
            GorSharpVisualStudioLogger.Error($"C# to Gor# conversion failed for {inputPath}. Details: {result.Message}");
            VsShellUtilities.ShowMessageBox(
                package,
                result.Message,
                "GorSharp Donusum Hatasi",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return;
        }

        GorSharpVisualStudioLogger.Important($"C# to Gor# conversion completed. Output={outputPath}");

        VsShellUtilities.ShowMessageBox(
            package,
            string.Format(CultureInfo.CurrentCulture, "Gor# dosyasi olusturuldu: {0}", outputPath),
            "GorSharp",
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

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

    private string? GetSelectedFilePath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var selectedItems = dte.ToolWindows.SolutionExplorer.SelectedItems as Array;
        if (selectedItems is null || selectedItems.Length != 1)
        {
            return null;
        }

        var selectedItem = selectedItems.GetValue(0) as UIHierarchyItem;
        var projectItem = selectedItem?.Object as ProjectItem;
        var fileCount = projectItem?.FileCount ?? 0;
        if (fileCount == 0)
        {
            return null;
        }

        return projectItem?.FileNames[1];
    }

    private static bool IsCSharpFile(string? filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath) &&
               string.Equals(Path.GetExtension(filePath), ".cs", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(bool Success, string Message)> ConvertWithCliAsync(string inputPath, string outputPath)
    {
        var bundledCli = GetBundledCliPath();
        if (!string.IsNullOrWhiteSpace(bundledCli))
        {
            var bundled = await RunProcessAsync(bundledCli!, $"fromcs {Quote(inputPath)} -o {Quote(outputPath)}");
            if (bundled.Success)
            {
                return (true, string.Empty);
            }

            return (false,
                "VSIX icindeki donusum araci calistirilamadi.\n\n" +
                "Lutfen uzantiyi yeniden yukleyin veya VSIX paketini yeniden olusturun.\n\n" +
                bundled.Message);
        }

        var inArg = Quote(inputPath);
        var outArg = Quote(outputPath);

        var direct = await RunProcessAsync("gorsharp", $"fromcs {inArg} -o {outArg}");
        if (direct.Success)
        {
            return (true, string.Empty);
        }

        var localCliProject = FindCliProjectNear(inputPath);
        if (!string.IsNullOrWhiteSpace(localCliProject))
        {
            var fallback = await RunProcessAsync("dotnet", $"run --project {Quote(localCliProject!)} -- fromcs {inArg} -o {outArg}");
            if (fallback.Success)
            {
                return (true, string.Empty);
            }

            return (false, fallback.Message);
        }

        return (false,
            "Donusum araci bulunamadi.\n\n" +
            "Cozum:\n" +
            "1) VSIX paketini yeniden olusturun (tools klasoru eksik olabilir), veya\n" +
            "2) gelistirme ortaminda gorsharp aracini PATH'e ekleyin.\n\n" +
            direct.Message);
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

    private static async Task<(bool Success, string Message)> RunProcessAsync(string fileName, string arguments)
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
                return (false, $"Komut baslatilamadi: {fileName}");
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

                return (false, message);
            }

            GorSharpVisualStudioLogger.Verbose($"Process completed successfully: {fileName}");

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            GorSharpVisualStudioLogger.Error($"Process execution failed: {fileName}", ex);
            return (false, ex.Message);
        }
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
