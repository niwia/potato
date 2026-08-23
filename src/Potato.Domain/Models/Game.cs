using Potato.Domain.ValueObjects;

namespace Potato.Domain.Models;

/// <summary>
/// Core domain entity representing an installed or managed Steam Game.
/// </summary>
public sealed record Game
{
    public AppId AppId { get; init; }
    public string Name { get; init; }
    public string InstallDir { get; init; }
    public string BuildId { get; init; }
    public string Branch { get; init; }
    public IReadOnlyList<Depot> InstalledDepots { get; init; }

    public Game(
        AppId appId,
        string name,
        string installDir,
        string buildId = "",
        string branch = "public",
        IReadOnlyList<Depot>? installedDepots = null)
    {
        AppId = appId;
        Name = name ?? string.Empty;
        InstallDir = installDir ?? string.Empty;
        BuildId = buildId ?? string.Empty;
        Branch = string.IsNullOrWhiteSpace(branch) ? "public" : branch;
        InstalledDepots = installedDepots ?? Array.Empty<Depot>();
    }
}
