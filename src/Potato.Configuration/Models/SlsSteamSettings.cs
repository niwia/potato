namespace Potato.Configuration.Models;

/// <summary>
/// Settings for Steam integration, SLSsteam config synchronization, and emulators.
/// </summary>
public sealed class SlsSteamSettings
{
    /// <summary>
    /// Whether to automatically synchronize installed AppIDs to SLSsteam config.yaml and trigger IPC notifications.
    /// </summary>
    public bool EnableSlsIntegration { get; set; } = true;

    /// <summary>
    /// Custom Steam installation directory override (null for auto-detection).
    /// </summary>
    public string? CustomSteamPath { get; set; }

    /// <summary>
    /// Custom SLSsteam config.yaml file path override (null for auto-detection).
    /// </summary>
    public string? CustomSlsConfigPath { get; set; }

    /// <summary>
    /// Whether to prompt the user to restart Steam when IPC pipe is unavailable.
    /// </summary>
    public bool PromptSteamRestart { get; set; } = false;

    /// <summary>
    /// Whether to automatically install Goldberg emulator steam_api DLLs when available.
    /// </summary>
    public bool AutoInstallGoldberg { get; set; } = true;

    /// <summary>
    /// Whether to automatically apply Goldberg emulator configurations.
    /// </summary>
    public bool AutoApplyGoldberg { get; set; } = false;
}
