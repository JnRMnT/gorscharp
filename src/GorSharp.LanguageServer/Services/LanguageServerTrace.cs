using System.Globalization;

namespace GorSharp.LanguageServer.Services;

internal static class LanguageServerTrace
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GorSharp",
        "Logs");
    private static readonly string LegacyLogPath = Path.Combine(LogDirectory, "lsp-server.log");
    private static readonly string LogPath = Path.Combine(LogDirectory, $"lsp-server-{ResolveInstanceTag()}.log");

    public static string CurrentLogPath => LogPath;

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Warning(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null
            ? message
            : string.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", message, Environment.NewLine, exception);

        Write("ERROR", fullMessage);
    }

    private static void Write(string level, string message)
    {
        var line = string.Format(
            CultureInfo.InvariantCulture,
            "[{0:O}] [{1}] {2}",
            DateTimeOffset.Now,
            level,
            message);

        lock (SyncRoot)
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LegacyLogPath, line + Environment.NewLine);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
    }

    private static string ResolveInstanceTag()
    {
        var baseDirectory = AppContext.BaseDirectory;
        if (baseDirectory.IndexOf("VisualStudio", StringComparison.OrdinalIgnoreCase) >= 0 &&
            baseDirectory.IndexOf("Exp", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "exp";
        }

        return "main";
    }
}