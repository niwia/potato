using Potato.Domain.ValueObjects;

namespace Potato.ManifestApi.Models;

/// <summary>
/// Model representing a game search result returned by the Hubcap / Morrenus API (/search or /library).
/// </summary>
public sealed record HubcapSearchResult(
    AppId AppId,
    string Name,
    ulong ManifestSize = 0,
    bool ManifestAvailable = true,
    string? HeaderImageUrl = null,
    string? DenuvoStatus = null,
    string? ProtonDbTier = null)
{
    public string FormattedSize => FormatBytes(ManifestSize);

    private static string FormatBytes(ulong bytes)
    {
        if (bytes == 0) return "N/A";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int idx = 0;
        double dBytes = bytes;
        while (dBytes >= 1024 && idx < suffixes.Length - 1)
        {
            dBytes /= 1024;
            idx++;
        }
        return $"{dBytes:0.##} {suffixes[idx]}";
    }
}
