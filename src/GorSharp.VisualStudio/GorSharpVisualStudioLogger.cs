using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GorSharp.VisualStudio;

internal static class GorSharpVisualStudioLogger
{
    private const string Source = "GorSharp.VisualStudio";
    private const string OutputPaneTitle = "Gör#";
    private static readonly Guid OutputPaneGuid = new("6f4c5984-17f2-4d08-a86d-44f32744b8d8");
    private static readonly string InstanceTag = ResolveInstanceTag();
    private static readonly object SyncRoot = new();
    private static IVsOutputWindowPane? outputPane;

    public static string CurrentLogPath => BuildLogPath("visualstudio", InstanceTag);

    public static bool IsVerboseEnabled => string.Equals(InstanceTag, "exp", StringComparison.OrdinalIgnoreCase);

    public static string CurrentInstanceTag => InstanceTag;

    public static async Task InitializeAsync(AsyncPackage package, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        try
        {
            var outputWindow = await package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow is null)
            {
                Warning("Visual Studio Output window service unavailable. File logging will continue.");
                return;
            }

            var paneGuid = OutputPaneGuid;
            outputWindow.CreatePane(ref paneGuid, OutputPaneTitle, 1, 1);
            outputWindow.GetPane(ref paneGuid, out outputPane);

            if (outputPane is null)
            {
                Warning("GorSharp output pane could not be created. File logging will continue.");
                return;
            }

            WriteToOutput("INFO", "GorSharp output pane initialized.");
        }
        catch (Exception ex)
        {
            Error("Failed to initialize Visual Studio output pane.", ex);
        }
    }

    public static void Verbose(string message)
    {
        Write("VERBOSE", message, ActivityLog.LogInformation, verboseOnly: true);
    }

    public static void Info(string message)
    {
        Important(message);
    }

    public static void Important(string message)
    {
        Write("INFO", message, ActivityLog.LogInformation, verboseOnly: false);
    }

    public static void Warning(string message)
    {
        Write("WARN", message, ActivityLog.LogWarning, verboseOnly: false);
    }

    public static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null
            ? message
            : string.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", message, Environment.NewLine, exception);

        Write("ERROR", fullMessage, ActivityLog.LogError, verboseOnly: false);
    }

    private static void Write(string level, string message, Action<string, string> activityLogWriter, bool verboseOnly)
    {
        if (verboseOnly && !IsVerboseEnabled)
        {
            return;
        }

        var logDirectory = BuildLogDirectory();
        var legacyLogPath = Path.Combine(logDirectory, "visualstudio.log");
        var logPath = BuildLogPath("visualstudio", InstanceTag);

        var line = string.Format(
            CultureInfo.InvariantCulture,
            "[{0:O}] [{1}] {2}",
            DateTimeOffset.Now,
            level,
            message);

        lock (SyncRoot)
        {
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(legacyLogPath, line + Environment.NewLine);
            File.AppendAllText(logPath, line + Environment.NewLine);
        }

        WriteToOutput(level, message);

        try
        {
            activityLogWriter(Source, message);
        }
        catch
        {
            // ActivityLog may be unavailable early in startup. Keep the file log as the reliable sink.
        }
    }

    private static void WriteToOutput(string level, string message)
    {
        try
        {
            TryEnsureOutputPane();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                outputPane?.OutputString(
                    string.Format(CultureInfo.InvariantCulture, "[{0}] {1}{2}", level, message, Environment.NewLine));
            });
        }
        catch
        {
            // Output pane availability should not block file logging.
        }
    }

    private static void TryEnsureOutputPane()
    {
        if (outputPane is not null)
        {
            return;
        }

        try
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var outputWindow = ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow is null)
                {
                    return;
                }

                var paneGuid = OutputPaneGuid;
                outputWindow.CreatePane(ref paneGuid, OutputPaneTitle, 1, 1);
                outputWindow.GetPane(ref paneGuid, out outputPane);
            });
        }
        catch
        {
            // File logging remains the fallback if VS services are not ready.
        }
    }

    private static string ResolveInstanceTag()
    {
        string assemblyPath;
        try
        {
            assemblyPath = typeof(GorSharpVisualStudioLogger).Assembly.Location;
        }
        catch
        {
            return "main";
        }

        if (assemblyPath.IndexOf("VisualStudio", StringComparison.OrdinalIgnoreCase) >= 0 &&
            assemblyPath.IndexOf("Exp", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "exp";
        }

        return "main";
    }

    private static string BuildLogDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GorSharp",
            "Logs");
    }

    private static string BuildLogPath(string baseName, string instanceTag)
    {
        return Path.Combine(BuildLogDirectory(), $"{baseName}-{instanceTag}.log");
    }
}