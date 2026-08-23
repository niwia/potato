using Potato.Domain.ValueObjects;
using Potato.Library.Models;

namespace Potato.Library.Services;

/// <summary>
/// Service that checks whether installed games have newer manifests / build IDs available upstream.
/// </summary>
public interface IGameUpdateChecker
{
    Task<UpdateStatusResult> CheckGameUpdateAsync(
        InstalledGame game,
        string branch = "public",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<AppId, UpdateStatusResult>> CheckAllUpdatesAsync(
        IReadOnlyList<InstalledGame> games,
        string branch = "public",
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
