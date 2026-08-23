namespace Potato.SlsSteam.Config;

/// <summary>
/// Strongly-typed representation of the SLSsteam config.yaml document.
/// </summary>
public sealed class SlsConfigModel
{
    // Scalars
    public bool DisableFamilyShareLock { get; set; } = true;
    public bool UseWhitelist { get; set; } = false;
    public int MaxSchemaTries { get; set; } = 10;
    public bool SafeMode { get; set; } = true;
    public bool WarnHashMissmatch { get; set; } = true;
    public bool NotifyInit { get; set; } = false;
    public bool Api { get; set; } = true;
    public bool DisableCloud { get; set; } = true;
    public bool DisableUpdates { get; set; } = false;
    public string FakeName { get; set; } = string.Empty;
    public string FakeEmail { get; set; } = string.Empty;
    public int FakeWalletBalance { get; set; } = 0;
    public string LogLevels { get; set; } = "0x2";
    public bool DumpClientInterfaces { get; set; } = false;
    public bool ExtendedLogging { get; set; } = false;

    // Lists
    public List<SlsConfigEntry> AdditionalApps { get; set; } = new();
    public List<SlsConfigEntry> AppIds { get; set; } = new();
    public List<SlsConfigEntry> DepotBlacklist { get; set; } = new();

    // Maps
    public Dictionary<string, SlsConfigEntry> AppTokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SlsConfigEntry> FakeAppIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SlsConfigEntry> ManifestIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SlsConfigEntry> GameTitles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SlsConfigEntry> SubscriptionTimestamps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SlsConfigEntry> SteamIdOverride { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Nested Maps
    public Dictionary<string, Dictionary<string, SlsConfigEntry>> DlcData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<SlsConfigEntry>> DenuvoGames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Sub-maps
    public Dictionary<string, string> IdleStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AppId"] = "0",
        ["Title"] = "\"\""
    };

    // Unmapped / Custom Raw Sections
    public Dictionary<string, string> CustomSections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
