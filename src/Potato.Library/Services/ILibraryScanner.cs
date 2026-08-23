using Potato.Library.Models;

namespace Potato.Library.Services;

/// <summary>
/// Service that discovers and scans Steam libraries for installed games and ACF manifests.
/// </summary>
public interface ILibraryScanner
{
    Task<LibraryScanResult> ScanLibrariesAsync(
        IReadOnlyList<string>? customLibraryPaths = null,
        CancellationToken cancellationToken = default);
}
