namespace Potato.SteamMetadata.Models;

/// <summary>
/// Branch metadata retrieved from Steam depots.
/// </summary>
public sealed record SteamBranchInfo
{
    public string Name { get; init; }
    public string? BuildId { get; init; }
    public string? TimeUpdated { get; init; }
    public bool PwdRequired { get; init; }

    public SteamBranchInfo(string name, string? buildId = null, string? timeUpdated = null, bool pwdRequired = false)
    {
        Name = name;
        BuildId = buildId;
        TimeUpdated = timeUpdated;
        PwdRequired = pwdRequired;
    }
}
