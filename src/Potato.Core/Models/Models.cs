namespace Potato.Core.Models;

public record SteamApp
{
    public uint AppId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? InstallDir { get; init; }
    public string? HeaderImageUrl { get; init; }
    public uint StateFlags { get; init; } = 4; // 4 = Fully Installed
    public long SizeOnDisk { get; init; }
    public ulong BuildId { get; init; }
    public string LibraryPath { get; init; } = string.Empty;
    public Dictionary<uint, ulong> MountedDepots { get; init; } = new();
    public string UpdateStatus { get; init; } = "up_to_date";
    public bool IsSlssteamManaged { get; init; }
}

public record DepotInfo
{
    public uint DepotId { get; init; }
    public string Name { get; init; } = string.Empty;
    public ulong ManifestId { get; init; }
    public long SizeBytes { get; init; }
    public string OsList { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public bool IsDlc { get; init; }
    public uint DlcAppId { get; init; }
    public bool IsSelected { get; set; } = true;
}

public enum DownloadStatus
{
    Queued,
    Downloading,
    Paused,
    Verifying,
    ProcessingFixes,
    Completed,
    Failed,
    Cancelled
}

public record DownloadProgress
{
    public uint AppId { get; init; }
    public double Percent { get; init; }
    public double SpeedBytesPerSecond { get; init; }
    public long BytesDownloaded { get; init; }
    public long TotalBytes { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public TimeSpan Eta { get; init; }
}

public record AppSettings
{
    public string? CustomSteamPath { get; set; }
    public string? CustomSlssteamConfigPath { get; set; }
    public bool SlssteamModeEnabled { get; set; } = true;
    public bool SlssteamConfigManagementEnabled { get; set; } = true;
    public bool AutoGenerateAcf { get; set; } = true;
    public bool AutoApplyEosFix { get; set; } = true;
    public int MaxConcurrentDownloads { get; set; } = 1;
    public string? HubcapApiKey { get; set; }
    public bool IspBypassEnabled { get; set; } = false;
    public string? CustomDepotDownloaderPath { get; set; }
}
