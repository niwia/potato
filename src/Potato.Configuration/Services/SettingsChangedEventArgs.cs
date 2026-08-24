using Potato.Configuration.Models;

namespace Potato.Configuration.Services;

/// <summary>
/// Event arguments delivered whenever application settings are updated.
/// </summary>
public sealed class SettingsChangedEventArgs : EventArgs
{
    public PotatoSettings OldSettings { get; }
    public PotatoSettings NewSettings { get; }

    public SettingsChangedEventArgs(PotatoSettings oldSettings, PotatoSettings newSettings)
    {
        OldSettings = oldSettings;
        NewSettings = newSettings;
    }
}
