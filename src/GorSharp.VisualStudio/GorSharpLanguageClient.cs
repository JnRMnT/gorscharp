using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace GorSharp.VisualStudio;

/// <summary>
/// LSP client that launches the gorsharp-lsp language server
/// and connects it to Visual Studio's LSP infrastructure.
/// </summary>
[ContentType("gorsharp")]
[Name("gorsharp-language-client")]
[Export(typeof(ILanguageClient))]
[RunOnContext(RunningContext.RunOnHost)]
public class GorSharpLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
{
    private const string BundledExecutableName = "gorsharp-lsp.exe";
    private readonly TraceMiddleLayer _traceMiddleLayer = new();

    public GorSharpLanguageClient()
    {
        GorSharpVisualStudioLogger.Important("Language client constructed.");
    }

    public string Name => "Gör# Dil Sunucusu";

    public IEnumerable<string>? ConfigurationSections => new[] { "gorsharp" };

    public object? InitializationOptions => null;

    public IEnumerable<string>? FilesToWatch => new[] { "**/*.gör", "**/sozluk.json" };

    public bool ShowNotificationOnInitializeFailed => true;

    public object MiddleLayer => _traceMiddleLayer;

    public object? CustomMessageTarget => null;

    public event AsyncEventHandler<EventArgs>? StartAsync;
    public event AsyncEventHandler<EventArgs>? StopAsync;

    public async Task<Connection?> ActivateAsync(CancellationToken token)
    {
        GorSharpVisualStudioLogger.Important($"{Name} activation started.");

        try
        {
            await Task.Yield();

            var resolution = ResolveServerExecutable();
            GorSharpVisualStudioLogger.Important("Resolving language server executable.");
            GorSharpVisualStudioLogger.Verbose($"Server resolution details:{Environment.NewLine}{resolution.ResolutionLog}");

            if (resolution.ExecutablePath is null)
            {
                GorSharpVisualStudioLogger.Error("Server executable not found.");
                throw new FileNotFoundException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "gorsharp-lsp bulunamadi. Aranan konumlar:{0}{1}{0}Detayli gunluk: {2}",
                        Environment.NewLine,
                        resolution.ResolutionLog,
                        GorSharpVisualStudioLogger.CurrentLogPath));
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = resolution.ExecutablePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            GorSharpVisualStudioLogger.Important("Language server executable resolved.");
            GorSharpVisualStudioLogger.Verbose($"Executable path: {resolution.ExecutablePath}");
            GorSharpVisualStudioLogger.Verbose($"Executable exists: {File.Exists(resolution.ExecutablePath)}");
            GorSharpVisualStudioLogger.Verbose($"Creating process with: {startInfo.FileName}");

            var process = Process.Start(startInfo);
            if (process is null)
            {
                GorSharpVisualStudioLogger.Error("Language server process creation returned null.");
                throw new InvalidOperationException("gorsharp-lsp baslatilamadi.");
            }

            GorSharpVisualStudioLogger.Important($"Language server process started. PID={process.Id}");
            _ = Task.Run(() => PumpStandardErrorAsync(process));
            GorSharpVisualStudioLogger.Important("Language server stderr pump started.");

            GorSharpVisualStudioLogger.Important($"{Name} activation completed.");
            return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
        }
        catch (Exception ex)
        {
            GorSharpVisualStudioLogger.Error($"{Name} activation failed.", ex);
            throw;
        }
    }

    public async Task OnLoadedAsync()
    {
        GorSharpVisualStudioLogger.Important($"{Name} OnLoadedAsync invoked.");
        GorSharpVisualStudioLogger.Verbose($"StartAsync handler present: {StartAsync is not null}");
        
        if (StartAsync is not null)
        {
            GorSharpVisualStudioLogger.Important("Invoking StartAsync handler.");
            try
            {
                await StartAsync.InvokeAsync(this, EventArgs.Empty);
                GorSharpVisualStudioLogger.Important("StartAsync completed successfully.");
            }
            catch (Exception ex)
            {
                GorSharpVisualStudioLogger.Error($"StartAsync threw exception: {ex.Message}", ex);
                throw;
            }
        }
        else
        {
            GorSharpVisualStudioLogger.Warning("StartAsync is null. Language server will not start.");
        }
    }

    public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
    {
        GorSharpVisualStudioLogger.Error($"{Name} server initialization failed. See log: {GorSharpVisualStudioLogger.CurrentLogPath}");
        GorSharpVisualStudioLogger.Verbose($"InitializationState: {initializationState?.ToString() ?? "NULL"}");
        return Task.FromResult<InitializationFailureContext?>(null);
    }

    public Task OnServerInitializedAsync()
    {
        GorSharpVisualStudioLogger.Important($"{Name} server initialized successfully.");
        return Task.CompletedTask;
    }

    public Task AttachForCustomMessageAsync(JsonRpc rpc)
    {
        GorSharpVisualStudioLogger.Info($"{Name} AttachForCustomMessageAsync invoked.");
        return Task.CompletedTask;
    }

    private static ServerExecutableResolution ResolveServerExecutable()
    {
        var log = new StringBuilder();
        var assemblyDir = Path.GetDirectoryName(typeof(GorSharpLanguageClient).Assembly.Location);
        log.AppendLine($"[ResolveServerExecutable]");
        log.AppendLine($"  Assembly: {typeof(GorSharpLanguageClient).Assembly.Location}");
        log.AppendLine($"  AssemblyDir: {assemblyDir ?? "NULL"}");

        if (assemblyDir is not null)
        {
            var bundled = Path.Combine(assemblyDir, "server", BundledExecutableName);
            log.AppendLine($"  [1] Bundled with .exe: {bundled}");
            if (File.Exists(bundled))
            {
                log.AppendLine($"      ✓ FOUND");
                return new ServerExecutableResolution(bundled, log.ToString().TrimEnd(), true);
            }
            log.AppendLine($"      ✗ not found");

            var bundledNoExt = Path.Combine(assemblyDir, "server", "gorsharp-lsp");
            log.AppendLine($"  [2] Bundled without extension: {bundledNoExt}");
            if (File.Exists(bundledNoExt))
            {
                log.AppendLine($"      ✓ FOUND");
                return new ServerExecutableResolution(bundledNoExt, log.ToString().TrimEnd(), true);
            }
            log.AppendLine($"      ✗ not found");

            // Debug: list what's in the server directory
            var serverDir = Path.Combine(assemblyDir, "server");
            if (Directory.Exists(serverDir))
            {
                log.AppendLine($"  [DEBUG] Contents of {serverDir}:");
                var files = Directory.GetFiles(serverDir).Take(10);
                foreach (var f in files)
                {
                    log.AppendLine($"           - {Path.GetFileName(f)}");
                }
            }
            else
            {
                log.AppendLine($"  [DEBUG] Directory does not exist: {serverDir}");
            }
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        log.AppendLine($"  [3] Searching PATH ({pathValue.Split(Path.PathSeparator).Length} directories)...");
        foreach (var segment in pathValue.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(segment.Trim(), BundledExecutableName);
            if (File.Exists(candidate))
            {
                log.AppendLine($"      ✓ Found in PATH: {candidate}");
                return new ServerExecutableResolution(candidate, log.ToString().TrimEnd(), false);
            }
        }

        log.AppendLine($"      ✗ Not found in any PATH directory");
        log.AppendLine($"\n✗✗✗ SERVER NOT FOUND - all search methods failed ✗✗✗");

        return new ServerExecutableResolution(null, log.ToString().TrimEnd(), false);
    }

    private static async Task PumpStandardErrorAsync(Process process)
    {
        try
        {
            while (!process.HasExited)
            {
                var line = await process.StandardError.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                GorSharpVisualStudioLogger.Verbose($"Language server stderr: {line}");
            }
        }
        catch (Exception ex)
        {
            GorSharpVisualStudioLogger.Error("Failed while reading language server stderr.", ex);
        }
    }

    private sealed class TraceMiddleLayer : ILanguageClientMiddleLayer
    {
        private static long _requestCounter;

        private static readonly string[] TracedMethods =
        {
            "textDocument/definition"
        };

        public bool CanHandle(string methodName)
        {
            return TracedMethods.Contains(methodName, StringComparer.Ordinal);
        }

        public async Task<JToken?> HandleRequestAsync(
            string methodName,
            JToken methodParam,
            Func<JToken, Task<JToken?>> sendRequest)
        {
            var requestId = Interlocked.Increment(ref _requestCounter);
            var stopwatch = Stopwatch.StartNew();
            var requestSummary = SummarizeToken(methodParam);
            GorSharpVisualStudioLogger.Info($"MiddleLayer request #{requestId} -> {methodName}: {requestSummary}");

            try
            {
                var result = await sendRequest(methodParam).ConfigureAwait(false);
                stopwatch.Stop();

                GorSharpVisualStudioLogger.Info($"MiddleLayer result #{requestId} <- {methodName} in {stopwatch.ElapsedMilliseconds} ms: {SummarizeToken(result)}");
                if (string.Equals(methodName, "textDocument/definition", StringComparison.Ordinal))
                {
                    var fullPayload = result?.ToString(Formatting.None) ?? "<null>";
                    GorSharpVisualStudioLogger.Info($"MiddleLayer definition full payload #{requestId}: {fullPayload}");
                }

                return result;
            }
            catch (OperationCanceledException ex)
            {
                stopwatch.Stop();
                GorSharpVisualStudioLogger.Warning($"MiddleLayer request #{requestId} canceled for {methodName} after {stopwatch.ElapsedMilliseconds} ms: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                GorSharpVisualStudioLogger.Error($"MiddleLayer request #{requestId} failed for {methodName} after {stopwatch.ElapsedMilliseconds} ms.", ex);
                throw;
            }
        }

        public async Task HandleNotificationAsync(
            string methodName,
            JToken methodParam,
            Func<JToken, Task> sendNotification)
        {
            GorSharpVisualStudioLogger.Info($"MiddleLayer notification -> {methodName}: {SummarizeToken(methodParam)}");

            try
            {
                await sendNotification(methodParam).ConfigureAwait(false);
                GorSharpVisualStudioLogger.Info($"MiddleLayer notification completed <- {methodName}");
            }
            catch (OperationCanceledException ex)
            {
                GorSharpVisualStudioLogger.Warning($"MiddleLayer notification canceled for {methodName}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                GorSharpVisualStudioLogger.Error($"MiddleLayer notification failed for {methodName}.", ex);
                throw;
            }
        }

        private static string SummarizeToken(JToken? token)
        {
            if (token is null)
            {
                return "<null>";
            }

            var text = token.ToString();
            return text.Length <= 400 ? text : text.Substring(0, 400) + "...";
        }
    }

}
