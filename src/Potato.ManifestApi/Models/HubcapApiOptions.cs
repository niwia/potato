namespace Potato.ManifestApi.Models;

/// <summary>
/// Configuration options for the Hubcap / Morrenus Manifest API client.
/// </summary>
public sealed class HubcapApiOptions
{
    public string BaseUrl { get; set; } = "https://hubcapmanifest.com/api/v1";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}
