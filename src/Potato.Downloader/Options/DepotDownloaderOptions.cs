using Potato.Domain.ValueObjects;

namespace Potato.Downloader.Options;

/// <summary>
/// Execution options for running DepotDownloaderMod as an out-of-process download worker.
/// </summary>
public sealed class DepotDownloaderOptions
{
    public AppId AppId { get; set; }
    public DepotId DepotId { get; set; }
    public ManifestGid ManifestGid { get; set; }
    public string ManifestFilePath { get; set; } = string.Empty;
    public string? DepotKeysFilePath { get; set; }
    public string DownloadDir { get; set; } = string.Empty;
    public int MaxDownloads { get; set; } = 4;
    public bool Validate { get; set; } = true;
    public string Branch { get; set; } = "public";
    public bool UseLanCache { get; set; }
    public int LoginId { get; set; }
    public string? FileListPath { get; set; }
    public string DotnetExecutable { get; set; } = "dotnet";
    public string DepotDownloaderDllPath { get; set; } = string.Empty;

    public DepotDownloaderOptions()
    {
        // Generate random positive 32-bit integer for session isolation
        LoginId = Random.Shared.Next(1, int.MaxValue);
    }

    /// <summary>
    /// Builds the exact command-line arguments to pass to the dotnet process.
    /// </summary>
    public IReadOnlyList<string> BuildCommandLineArgs()
    {
        if (string.IsNullOrWhiteSpace(DepotDownloaderDllPath))
        {
            throw new InvalidOperationException("DepotDownloader DLL path must be specified.");
        }

        var args = new List<string>
        {
            DepotDownloaderDllPath,
            "-app", AppId.ToString(),
            "-depot", DepotId.ToString(),
            "-manifest", ManifestGid.ToString(),
            "-manifestfile", ManifestFilePath
        };

        if (!string.IsNullOrWhiteSpace(DepotKeysFilePath))
        {
            args.Add("-depotkeys");
            args.Add(DepotKeysFilePath);
        }

        args.Add("-max-downloads");
        args.Add(MaxDownloads.ToString());
        args.Add("-dir");
        args.Add(DownloadDir);

        if (Validate)
        {
            args.Add("-validate");
        }

        if (!string.IsNullOrWhiteSpace(Branch) && !string.Equals(Branch, "public", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("-branch");
            args.Add(Branch);
        }

        if (UseLanCache)
        {
            args.Add("-use-lancache");
        }

        int activeLoginId = LoginId > 0 ? LoginId : Random.Shared.Next(1, int.MaxValue);
        args.Add("-loginid");
        args.Add(activeLoginId.ToString());

        if (!string.IsNullOrWhiteSpace(FileListPath))
        {
            args.Add("-filelist");
            args.Add(FileListPath);
        }

        return args;
    }
}
