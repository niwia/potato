namespace Potato.Downloader.Process;

/// <summary>
/// Abstraction for suspending and resuming a process tree at the OS level.
/// </summary>
public interface IProcessSuspender
{
    bool SuspendProcessTree(int rootPid);
    bool ResumeProcessTree(int rootPid);
}
