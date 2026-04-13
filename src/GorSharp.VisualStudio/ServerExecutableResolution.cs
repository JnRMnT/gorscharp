namespace GorSharp.VisualStudio;

internal sealed class ServerExecutableResolution
{
    public ServerExecutableResolution(string? executablePath, string resolutionLog, bool isBundledExecutable)
    {
        ExecutablePath = executablePath;
        ResolutionLog = resolutionLog;
        IsBundledExecutable = isBundledExecutable;
    }

    public string? ExecutablePath { get; }

    public string ResolutionLog { get; }

    public bool IsBundledExecutable { get; }
}