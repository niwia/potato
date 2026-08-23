using Potato.Domain.ValueObjects;

namespace Potato.Pipeline.Models;

/// <summary>
/// Parameters defining a game download and installation operation.
/// </summary>
public sealed record InstallRequest
{
    public AppId AppId { get; init; }
    public string DestinationPath { get; init; }
    public string Branch { get; init; }
    public IReadOnlyList<DepotId>? SelectedDepots { get; init; }
    public int MaxDownloads { get; init; }
    public bool Validate { get; init; }
    public bool UseLanCache { get; init; }
    public bool UnlockSls { get; init; }

    public InstallRequest(
        AppId appId,
        string destinationPath,
        string branch = "public",
        IReadOnlyList<DepotId>? selectedDepots = null,
        int maxDownloads = 4,
        bool validate = true,
        bool useLanCache = false,
        bool unlockSls = false)
    {
        AppId = appId;
        DestinationPath = destinationPath;
        Branch = string.IsNullOrWhiteSpace(branch) ? "public" : branch.Trim();
        SelectedDepots = selectedDepots;
        MaxDownloads = maxDownloads > 0 ? maxDownloads : 4;
        Validate = validate;
        UseLanCache = useLanCache;
        UnlockSls = unlockSls;
    }
}
