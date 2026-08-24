namespace Potato.Configuration.Models;

/// <summary>
/// Settings for Morrenus Hubcap API authentication and cloud proxy endpoints.
/// </summary>
public sealed class ApiSettings
{
    /// <summary>
    /// The user's Hubcap API key for authenticated manifest retrieval.
    /// </summary>
    public string? HubcapApiKey { get; set; }

    /// <summary>
    /// Whether to route Hubcap API requests through Cloudflare worker ISP bypass.
    /// </summary>
    public bool UseIspBypass { get; set; } = true;

    /// <summary>
    /// Custom proxy or Wirecutter worker URL (defaults to standard Hubcap endpoint if empty).
    /// </summary>
    public string? CustomWirecutterUrl { get; set; }

    /// <summary>
    /// Request timeout in seconds for API network operations.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
