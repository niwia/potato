using System.Runtime.InteropServices;

namespace Potato.Downloader.Process;

/// <summary>
/// Linux implementation of process-tree suspension using POSIX SIGSTOP (19) and SIGCONT (18).
/// </summary>
public sealed class LinuxProcessSuspender : IProcessSuspender
{
    private const int SIGSTOP = 19;
    private const int SIGCONT = 18;

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);

    public bool SuspendProcessTree(int rootPid)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            var pids = GetAllProcessIdsInTree(rootPid);
            bool allSuccess = true;

            foreach (int pid in pids)
            {
                if (kill(pid, SIGSTOP) != 0)
                {
                    int err = Marshal.GetLastPInvokeError();
                    // ESRCH (3) means process already exited, which is non-fatal
                    if (err != 3)
                    {
                        allSuccess = false;
                    }
                }
            }

            return allSuccess;
        }
        catch
        {
            return false;
        }
    }

    public bool ResumeProcessTree(int rootPid)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            var pids = GetAllProcessIdsInTree(rootPid);
            bool allSuccess = true;

            foreach (int pid in pids)
            {
                if (kill(pid, SIGCONT) != 0)
                {
                    int err = Marshal.GetLastPInvokeError();
                    if (err != 3)
                    {
                        allSuccess = false;
                    }
                }
            }

            return allSuccess;
        }
        catch
        {
            return false;
        }
    }

    public static List<int> GetAllProcessIdsInTree(int rootPid)
    {
        var result = new HashSet<int> { rootPid };
        CollectChildrenRecursive(rootPid, result);
        return result.ToList();
    }

    private static void CollectChildrenRecursive(int parentPid, HashSet<int> accumulator)
    {
        string childrenPath = $"/proc/{parentPid}/task/{parentPid}/children";
        if (File.Exists(childrenPath))
        {
            try
            {
                string text = File.ReadAllText(childrenPath);
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var part in parts)
                {
                    if (int.TryParse(part, out int childPid) && accumulator.Add(childPid))
                    {
                        CollectChildrenRecursive(childPid, accumulator);
                    }
                }
                return;
            }
            catch
            {
                // Fallback to /proc iteration
            }
        }

        // Fallback: Scan /proc directory entries for PPid
        try
        {
            var procDirs = Directory.GetDirectories("/proc");
            foreach (var dir in procDirs)
            {
                string dirName = Path.GetFileName(dir);
                if (int.TryParse(dirName, out int procPid) && procPid != parentPid)
                {
                    string statusFile = Path.Combine(dir, "status");
                    if (File.Exists(statusFile))
                    {
                        try
                        {
                            foreach (var line in File.ReadLines(statusFile))
                            {
                                if (line.StartsWith("PPid:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string ppidStr = line["PPid:".Length..].Trim();
                                    if (int.TryParse(ppidStr, out int ppid) && ppid == parentPid)
                                    {
                                        if (accumulator.Add(procPid))
                                        {
                                            CollectChildrenRecursive(procPid, accumulator);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            // Process might have terminated while reading
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore /proc read errors
        }
    }
}
