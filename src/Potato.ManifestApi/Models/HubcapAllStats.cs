namespace Potato.ManifestApi.Models;

/// <summary>
/// User stats returned by Hubcap API (/user/stats).
/// </summary>
public sealed record HubcapUserStats(
    int DailyManifestDownloads = 0,
    int DailyManifestLimit = 55,
    string? ExpiresAt = null,
    int? DaysRemaining = null,
    string? Plan = "Standard");

/// <summary>
/// Cloud generation quotas and limits returned by Hubcap API (/generate/usage).
/// </summary>
public sealed record HubcapGenerateUsage(
    int AppBundleUsage = 0,
    int AppBundleLimit = 100,
    int SingleDepotUsage = 0,
    int SingleDepotLimit = 1500);

/// <summary>
/// Aggregated Hubcap statistics and formatted quota display strings for UI Visor.
/// </summary>
public sealed record HubcapAllStats(
    HubcapUserStats UserStats,
    HubcapGenerateUsage GenerateUsage,
    bool IsHealthy = true)
{
    public string FormattedQuotaString
    {
        get
        {
            string expiry = UserStats.DaysRemaining.HasValue
                ? (UserStats.DaysRemaining.Value == 0 ? "Expires today" : $"{UserStats.DaysRemaining.Value}d")
                : "Never";

            return $"api: {UserStats.DailyManifestDownloads}/{UserStats.DailyManifestLimit}, " +
                   $"bundle: {GenerateUsage.AppBundleUsage}/{GenerateUsage.AppBundleLimit}, " +
                   $"single: {GenerateUsage.SingleDepotUsage}/{GenerateUsage.SingleDepotLimit} [{expiry}]";
        }
    }

    public string TooltipString
    {
        get
        {
            string expiry = UserStats.DaysRemaining.HasValue ? $"{UserStats.DaysRemaining.Value} days" : "Never";
            return $"Hubcap API Quotas & Limits:\n" +
                   $"• API Manifests: {UserStats.DailyManifestDownloads} / {UserStats.DailyManifestLimit}\n" +
                   $"• Bundle Generation: {GenerateUsage.AppBundleUsage} / {GenerateUsage.AppBundleLimit}\n" +
                   $"• Single Depot Generation: {GenerateUsage.SingleDepotUsage} / {GenerateUsage.SingleDepotLimit}\n" +
                   $"• Key Expiry: {expiry}\n" +
                   $"• Plan: {UserStats.Plan ?? "Standard"}";
        }
    }
}
