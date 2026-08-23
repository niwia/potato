namespace Potato.Library.Models;

/// <summary>
/// Status indicating whether an installed game has updates available.
/// </summary>
public enum UpdateStatus
{
    Unknown,
    Checking,
    UpToDate,
    UpdateAvailable,
    CannotDetermine
}
