namespace Potato.Configuration.Models;

/// <summary>
/// Settings for UI theme, accent colors, typography, and display density.
/// </summary>
public sealed class AppearanceSettings
{
    /// <summary>
    /// UI theme mode ("Dark", "Light", "System").
    /// </summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// Hex color code for the UI accent color (e.g. "#7C4DFF" or "#A1C9FD").
    /// </summary>
    public string AccentColor { get; set; } = "#7C4DFF";

    /// <summary>
    /// Theme preset name ("Ocean", "Sunset", "Forest", "Cyberpunk", "Violet", "Custom").
    /// </summary>
    public string PresetName { get; set; } = "Violet";

    /// <summary>
    /// Base font family name (null for system default).
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// Base UI font size (default 10).
    /// </summary>
    public int FontSize { get; set; } = 10;

    /// <summary>
    /// Whether to enable "Nerd Mode" showing technical details (AppIDs, Depot GIDs, Build IDs, and raw logs).
    /// </summary>
    public bool NerdMode { get; set; } = false;
}
