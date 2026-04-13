using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;

namespace GorSharp.VisualStudio;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("GorSharp", "GorSharp Visual Studio integration", "0.1.0")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(PackageGuidString)]
public sealed class GorSharpPackage : AsyncPackage
{
    public const string PackageGuidString = "f2a8c0fd-fec7-41f5-8f6b-a8ac2f5e6508";
    private static int globalHandlersRegistered;

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        GorSharpVisualStudioLogger.Important("Starting extension package initialization.");
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        GorSharpVisualStudioLogger.Important("Initializing logger output pane.");
        await GorSharpVisualStudioLogger.InitializeAsync(this, cancellationToken);
        GorSharpVisualStudioLogger.Important($"Logger initialized. Instance={GorSharpVisualStudioLogger.CurrentInstanceTag}, Verbose={GorSharpVisualStudioLogger.IsVerboseEnabled}.");

        GorSharpVisualStudioLogger.Important("Registering global exception handlers.");
        RegisterGlobalExceptionHandlers();

        GorSharpVisualStudioLogger.Important("Initializing commands.");
        await ConvertCsToGorCommand.InitializeAsync(this);
        GorSharpVisualStudioLogger.Important("Package initialization completed.");
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        if (Interlocked.Exchange(ref globalHandlersRegistered, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (Application.Current is not null)
        {
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        GorSharpVisualStudioLogger.Important("Global exception handlers registered.");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            GorSharpVisualStudioLogger.Error($"Unhandled exception captured (terminating={e.IsTerminating}).", exception);
            return;
        }

        GorSharpVisualStudioLogger.Error(
            $"Unhandled non-exception object captured (terminating={e.IsTerminating}): {e.ExceptionObject?.ToString() ?? "<null>"}.");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        GorSharpVisualStudioLogger.Error("Unobserved task exception captured.", e.Exception);
        e.SetObserved();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        GorSharpVisualStudioLogger.Error("Dispatcher unhandled exception captured.", e.Exception);
        // Keep Visual Studio default handling behavior by not setting e.Handled.
    }
}
