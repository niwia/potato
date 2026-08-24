using System.Diagnostics;
using System.Text;
using Potato.Downloader.Options;
using Potato.Downloader.Progress;

namespace Potato.Downloader.Process;

/// <summary>
/// Manages the lifecycle, asynchronous raw chunked output streaming, and suspension
/// of the DepotDownloaderMod subprocess.
/// </summary>
public sealed class DepotDownloaderProcess : IDepotDownloaderProcess, IDisposable
{
    private readonly IProcessSuspender _suspender;
    private System.Diagnostics.Process? _process;
    private readonly object _lock = new();

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }
    public int? ProcessId => _process?.Id;

    public DepotDownloaderProcess(IProcessSuspender? suspender = null)
    {
        _suspender = suspender ?? new LinuxProcessSuspender();
    }

    public async Task<int> RunAsync(
        DepotDownloaderOptions options,
        IProgress<DownloadProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        string dotnetExe = ResolveDotnetPath(options.DotnetExecutable);
        string dllPath = ResolveDllPath(options.DepotDownloaderDllPath);

        options.DotnetExecutable = dotnetExe;
        options.DepotDownloaderDllPath = dllPath;

        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string arg in options.BuildCommandLineArgs())
        {
            startInfo.ArgumentList.Add(arg);
        }

        var parser = new DownloadProgressParser();
        if (progress != null)
        {
            parser.ProgressChanged += report => progress.Report(report);
        }

        lock (_lock)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("A download process is already running.");
            }

            _process = new System.Diagnostics.Process { StartInfo = startInfo };
            _process.Start();
            IsRunning = true;
            IsPaused = false;
        }

        using var registration = cancellationToken.Register(() =>
        {
            Stop();
        });

        try
        {
            // Asynchronously read stdout byte stream in chunks
            var stdoutTask = ReadStreamInChunksAsync(_process.StandardOutput.BaseStream, parser, cancellationToken);
            var stderrTask = ReadStreamInChunksAsync(_process.StandardError.BaseStream, parser, cancellationToken);

            await _process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask);

            return _process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            Stop();
            return -1;
        }
        finally
        {
            lock (_lock)
            {
                IsRunning = false;
                IsPaused = false;
            }
        }
    }

    private static async Task ReadStreamInChunksAsync(
        Stream stream,
        DownloadProgressParser parser,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        var lineBuffer = new List<byte>(256);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead <= 0)
                {
                    break;
                }

                for (int i = 0; i < bytesRead; i++)
                {
                    byte b = buffer[i];
                    if (b == (byte)'\r' || b == (byte)'\n')
                    {
                        if (lineBuffer.Count > 0)
                        {
                            string line = Encoding.UTF8.GetString(lineBuffer.ToArray());
                            parser.ProcessLine(line);
                            lineBuffer.Clear();
                        }
                    }
                    else
                    {
                        lineBuffer.Add(b);
                    }
                }
            }

            if (lineBuffer.Count > 0)
            {
                string line = Encoding.UTF8.GetString(lineBuffer.ToArray());
                parser.ProcessLine(line);
                lineBuffer.Clear();
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation expected
        }
    }

    public bool Pause()
    {
        lock (_lock)
        {
            if (!IsRunning || IsPaused || _process == null)
            {
                return false;
            }

            bool success = _suspender.SuspendProcessTree(_process.Id);
            if (success)
            {
                IsPaused = true;
            }

            return success;
        }
    }

    public bool Resume()
    {
        lock (_lock)
        {
            if (!IsRunning || !IsPaused || _process == null)
            {
                return false;
            }

            bool success = _suspender.ResumeProcessTree(_process.Id);
            if (success)
            {
                IsPaused = false;
            }

            return success;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    // If paused, resume first so SIGTERM/SIGKILL can be processed
                    if (IsPaused)
                    {
                        Resume();
                    }

                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore errors during termination
                }
            }

            IsRunning = false;
            IsPaused = false;
        }
    }

    public static string ResolveDotnetPath(string? candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
        {
            return candidate;
        }

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot))
        {
            string path = Path.Combine(dotnetRoot, "dotnet");
            if (File.Exists(path)) return path;
        }

        string homeDotnet = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotnet",
            "dotnet");
        if (File.Exists(homeDotnet))
        {
            return homeDotnet;
        }

        return "dotnet";
    }

    public static string ResolveDllPath(string? candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
        {
            return candidate;
        }

        string? envPath = Environment.GetEnvironmentVariable("POTATO_DEPOTDOWNLOADER_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        string baseDir = AppContext.BaseDirectory;
        string currentDir = Directory.GetCurrentDirectory();
        string[] searchPaths =
        {
            Path.Combine(baseDir, "DepotDownloaderMod.dll"),
            Path.Combine(baseDir, "DepotDownloader.dll"),
            Path.Combine(baseDir, "deps", "DepotDownloader", "DepotDownloaderMod.dll"),
            Path.Combine(baseDir, "deps", "DepotDownloader", "DepotDownloader.dll"),
            Path.Combine(baseDir, "deps", "DepotDownloader.dll"),
            Path.Combine(baseDir, "..", "..", "..", "..", "src", "DepotDownloaderMod", "bin", "Debug", "net9.0", "DepotDownloaderMod.dll"),
            Path.Combine(baseDir, "..", "..", "..", "..", "src", "DepotDownloaderMod", "bin", "Release", "net9.0", "DepotDownloaderMod.dll"),
            Path.Combine(baseDir, "..", "..", "..", "..", "deps", "DepotDownloader", "DepotDownloader.dll"),
            Path.Combine(currentDir, "src", "DepotDownloaderMod", "bin", "Debug", "net9.0", "DepotDownloaderMod.dll"),
            Path.Combine(currentDir, "src", "DepotDownloaderMod", "bin", "Release", "net9.0", "DepotDownloaderMod.dll"),
            Path.Combine(currentDir, "deps", "DepotDownloader", "DepotDownloader.dll"),
            Path.Combine(currentDir, "deps", "DepotDownloader.dll")
        };

        foreach (string p in searchPaths)
        {
            string fullPath = Path.GetFullPath(p);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new FileNotFoundException(
            "DepotDownloader.dll could not be located. Ensure it is placed in deps/DepotDownloader/ or set POTATO_DEPOTDOWNLOADER_PATH.");
    }

    public void Dispose()
    {
        Stop();
        _process?.Dispose();
        _process = null;
    }
}
